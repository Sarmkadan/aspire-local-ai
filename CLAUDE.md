# aspire-local-ai

.NET Aspire hosting integration for local LLM servers (Ollama, vLLM) as container resources.

## Build / test

```bash
dotnet restore
dotnet build -c Release                       # TreatWarningsAsErrors is on; fix warnings, do not suppress
dotnet test -c Release --no-build
dotnet pack src/aspire-local-ai -c Release -o artifacts
```

Target: net9.0. Local machine may only have the .NET 10 SDK; the test project has `RollForward=Major` so tests run on a newer runtime.

## Layout

- `src/aspire-local-ai/` - the NuGet package `AspireLocalAI` (namespace `AspireLocalAI`)
  - `OllamaResource.cs`, `VllmResource.cs` - `ContainerResource` subclasses implementing `IResourceWithConnectionString` + `ILocalModelServer`
  - `ILocalModelServer.cs` - backend-agnostic contract (models list, primary endpoint)
  - `LocalAIResourceBuilderExtensions.cs` - all `Add*` / `With*` public extension methods
  - `LocalAIHealthCheckOptions.cs` - health check tuning
- `tests/aspire-local-ai.Tests/` - xUnit
- `Directory.Build.props` - shared compiler settings (nullable, latest lang, warnings as errors, analyzers)

## Architecture

- Depends on `Aspire.Hosting` only. No client-side (Microsoft.Extensions.AI) dependency in this package; consumers pick their own client.
- Resources follow the standard Aspire container pattern: `builder.AddResource(new XResource(name)).WithImage(...).WithHttpEndpoint(...)`.
- Model pulling for Ollama is an eventing hook (`OnResourceReady` / `AfterEndpointsAllocatedEvent`) that calls the Ollama REST API, publishes `ResourceStateSnapshot` updates and delays health until done.
- Health checks go through `Microsoft.Extensions.Diagnostics.HealthChecks` and are attached with `WithHealthCheck(key)`.
- Telemetry: OTLP env vars injected into the container; where the server does not emit OTLP natively, a sidecar/poller exports metrics.

## Conventions

- Public API gets XML docs (`GenerateDocumentationFile` is on, missing docs are errors).
- Stubs currently `throw new NotImplementedException()`; replace body, keep signature unless there is a reason.
- Tests: one class per public type, `Method_Scenario_Expectation` naming, no Docker required for unit tests (use `DistributedApplication.CreateBuilder` + inspect the model). Integration tests that need Docker live in a separate `[Trait("Category","Integration")]` set.
- Git: conventional commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). No Co-Authored-By trailers.
