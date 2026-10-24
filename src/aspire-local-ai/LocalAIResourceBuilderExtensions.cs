using System.Globalization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AspireLocalAI;

/// <summary>
/// Extension methods for adding and configuring local LLM resources in an Aspire AppHost.
/// </summary>
public static class LocalAIResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Ollama container resource to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="port">Optional host port to bind the HTTP endpoint to; a random port is used when <see langword="null"/>.</param>
    /// <returns>A resource builder for further configuration.</returns>
    public static IResourceBuilder<OllamaResource> AddOllama(
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (port is { } p)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p, nameof(port));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(p, 65535, nameof(port));
        }

        var resource = new OllamaResource(name);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(LocalAIContainerImageTags.OllamaImage)
            .WithImageRegistry(LocalAIContainerImageTags.OllamaRegistry)
            .WithImageTag(LocalAIContainerImageTags.OllamaTag)
            .WithHttpEndpoint(
                port: port,
                targetPort: OllamaResource.DefaultPort,
                name: OllamaResource.PrimaryEndpointName);

        builder.Eventing.Subscribe<ResourceReadyEvent>(resource, async (evt, ct) =>
        {
            var notifications = evt.Services.GetRequiredService<ResourceNotificationService>();

            var value = await resource.PrimaryEndpoint.GetValueAsync(ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Endpoint of Ollama resource '{resource.Name}' is not allocated.");

            // Status updates are fire-and-forget from the puller's sync callback; the last one is awaited below.
            var lastPublish = Task.CompletedTask;
            void ReportStatus(string text) =>
                lastPublish = notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = new ResourceStateSnapshot(text, KnownResourceStateStyles.Info),
                });

            await OllamaModelPuller.PullMissingAsync(
                new Uri(value),
                resource.Models,
                PullClient,
                ReportStatus,
                ct).ConfigureAwait(false);

            await lastPublish.ConfigureAwait(false);
        });

        return resourceBuilder;
    }

    // Pulls can run for many minutes on large models, so no request timeout; cancellation comes from the event token.
    private static readonly HttpClient PullClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// Adds a vLLM container resource serving a single model to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="modelName">Hugging Face model id, e.g. <c>Qwen/Qwen2.5-7B-Instruct</c>.</param>
    /// <param name="port">Optional host port to bind the HTTP endpoint to; must be in the range 1–65535 when specified.</param>
    /// <returns>A resource builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="modelName"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is outside the range 1–65535.</exception>
    public static IResourceBuilder<VllmResource> AddVllm(
        this IDistributedApplicationBuilder builder,
        string name,
        string modelName,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (port is { } p)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(p, nameof(port));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(p, 65535, nameof(port));
        }

        var resource = new VllmResource(name, modelName);

        return builder.AddResource(resource)
            .WithImage(LocalAIContainerImageTags.VllmImage)
            .WithImageRegistry(LocalAIContainerImageTags.VllmRegistry)
            .WithImageTag(LocalAIContainerImageTags.VllmTag)
            .WithHttpEndpoint(
                port: port,
                targetPort: VllmResource.DefaultPort,
                name: VllmResource.PrimaryEndpointName)
            .WithArgs("--model", modelName);
    }

    /// <summary>
    /// Declares a model that must be present on the server. For Ollama the model is pulled on startup
    /// and the resource is not reported healthy until the pull completes.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="modelName">Model tag.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown for <see cref="VllmResource"/>, which serves exactly the model passed to <see cref="AddVllm"/>.
    /// </exception>
    public static IResourceBuilder<T> WithModel<T>(this IResourceBuilder<T> builder, string modelName)
        where T : ContainerResource, ILocalModelServer
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        if (builder.Resource is OllamaResource ollama)
        {
            ollama.AddModel(modelName);
        }
        else if (builder.Resource is VllmResource)
        {
            throw new InvalidOperationException(
                $"vLLM resource '{builder.Resource.Name}' serves exactly the model passed to AddVllm; " +
                $"WithModel('{modelName}') is not supported. Create a separate vLLM resource for another model.");
        }

        return builder.WithAnnotation(new ModelAnnotation(modelName));
    }

    /// <summary>
    /// Mounts a persistent volume for downloaded model weights so they survive container restarts.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">Optional volume name; derived from the resource name when <see langword="null"/>.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is non-null but empty or whitespace.</exception>
    public static IResourceBuilder<T> WithDataVolume<T>(this IResourceBuilder<T> builder, string? name = null)
        where T : ContainerResource, ILocalModelServer
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }

        var targetPath = builder.Resource switch
        {
            OllamaResource => OllamaResource.DataMountPath,
            VllmResource => VllmResource.DataMountPath,
            _ => throw new InvalidOperationException(
                $"No data mount path is known for resource type '{builder.Resource.GetType().Name}'."),
        };

        return builder.WithVolume(name ?? $"{builder.Resource.Name}-data", targetPath, isReadOnly: false);
    }

    /// <summary>
    /// Requests GPU passthrough for the container (equivalent to <c>--gpus</c> in Docker).
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="vendor">GPU vendor to target.</param>
    /// <param name="count">Number of devices, or <see langword="null"/> for all.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IResourceBuilder<T> WithGpu<T>(
        this IResourceBuilder<T> builder,
        GpuVendor vendor = GpuVendor.Nvidia,
        int? count = null)
        where T : ContainerResource, ILocalModelServer
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!Enum.IsDefined(vendor))
        {
            throw new ArgumentOutOfRangeException(nameof(vendor), vendor, "Unknown GPU vendor.");
        }

        if (count is { } c)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(c, 1, nameof(count));
        }

        string[] runtimeArgs = vendor switch
        {
            GpuVendor.Nvidia => ["--gpus", count is null ? "all" : count.Value.ToString(CultureInfo.InvariantCulture)],
            GpuVendor.Amd => ["--device", "/dev/kfd", "--device", "/dev/dri"],
            _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, "Unknown GPU vendor."),
        };

        builder.WithAnnotation(new GpuAnnotation(vendor, count));

        if (vendor == GpuVendor.Amd && builder.Resource is OllamaResource)
        {
            builder.WithImageTag(LocalAIContainerImageTags.OllamaRocmTag);
        }

        return builder.WithContainerRuntimeArgs(runtimeArgs);
    }

    /// <summary>
    /// Registers a health check that verifies the server responds and all declared models are loaded.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="options">Optional health check tuning.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IResourceBuilder<T> WithModelHealthCheck<T>(
        this IResourceBuilder<T> builder,
        LocalAIHealthCheckOptions? options = null)
        where T : ContainerResource, ILocalModelServer
    {
        ArgumentNullException.ThrowIfNull(builder);

        options ??= new();

        var key = $"{builder.Resource.Name}_model_check";
        var isOllama = builder.Resource is OllamaResource;

        // Resolved at check time so the endpoint and model list reflect the final application model.
        async Task<Uri?> ResolveEndpointAsync(CancellationToken ct)
        {
            var value = await builder.Resource.PrimaryEndpoint.GetValueAsync(ct).ConfigureAwait(false);
            return value is null ? null : new Uri(value);
        }

        // Single instance per registration: the warm-up flag must outlive individual check runs.
        var check = new Lazy<LocalModelHealthCheck>(() => new LocalModelHealthCheck(
            ResolveEndpointAsync,
            builder.Resource.Models,
            options,
            SharedProbeClient,
            isOllama));

        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            key,
            _ => check.Value,
            failureStatus: null,
            tags: null));

        return builder.WithHealthCheck(key);
    }

    private static readonly HttpClient SharedProbeClient = new();

    /// <summary>
    /// Enables OpenTelemetry export from the server into the Aspire dashboard (metrics: tokens/sec,
    /// queue depth, loaded models; traces: per-request spans where the backend supports them).
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IResourceBuilder<T> WithOpenTelemetry<T>(this IResourceBuilder<T> builder)
        where T : ContainerResource, ILocalModelServer
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Idempotent: a second call would otherwise add duplicate OTLP env vars and CLI args.
        if (builder.Resource.Annotations.OfType<OpenTelemetryAnnotation>().Any())
        {
            return builder;
        }

        builder.WithAnnotation(new OpenTelemetryAnnotation());
        builder.WithOtlpExporter();
        builder.WithEnvironment("OTEL_SERVICE_NAME", builder.Resource.Name);

        if (builder.Resource is VllmResource)
        {
            // vLLM takes its trace endpoint from --otlp-traces-endpoint, so the OTLP endpoint injected
            // by WithOtlpExporter is mirrored into a variable the arg callback can read.
            object? tracesEndpoint = null;

            builder.WithEnvironment("VLLM_OTEL_TRACES_ENABLED", "1");
            builder.WithEnvironment(context =>
            {
                if (context.EnvironmentVariables.TryGetValue("OTEL_EXPORTER_OTLP_ENDPOINT", out var endpoint))
                {
                    tracesEndpoint = endpoint;
                    context.EnvironmentVariables["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"] = endpoint;
                }
            });
            builder.WithArgs(context =>
            {
                if (tracesEndpoint is not null)
                {
                    context.Args.Add("--otlp-traces-endpoint");
                    context.Args.Add(tracesEndpoint);
                }
            });
        }

        return builder;
    }
}

/// <summary>GPU vendors supported by <see cref="LocalAIResourceBuilderExtensions.WithGpu{T}"/>.</summary>
public enum GpuVendor
{
    /// <summary>NVIDIA via the NVIDIA Container Toolkit.</summary>
    Nvidia,

    /// <summary>AMD via ROCm.</summary>
    Amd,
}
