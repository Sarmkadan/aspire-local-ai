# aspire-local-ai

[![CI](https://github.com/Sarmkadan/aspire-local-ai/actions/workflows/ci.yml/badge.svg)](https://github.com/Sarmkadan/aspire-local-ai/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AspireLocalAI.svg)](https://www.nuget.org/packages/AspireLocalAI)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

.NET Aspire integration for local LLMs. Run Ollama and vLLM as first-class Aspire resources with
model-aware health checks, persistent model storage, GPU passthrough and OpenTelemetry in the dashboard.

## Features

- `AddOllama()` / `AddVllm()` - container resources wired into the Aspire application model
- `WithModel("llama3.2")` - declare required models; pulled on startup, resource stays unhealthy until ready
- `WithDataVolume()` - keep downloaded weights across restarts
- `WithGpu()` - NVIDIA / AMD device passthrough
- `WithModelHealthCheck()` - endpoint reachability + model presence + optional warm-up prompt
- `WithOpenTelemetry()` - tokens/sec, queue depth and per-request traces in the Aspire dashboard
- Connection strings ready for `Microsoft.Extensions.AI` / OpenAI-compatible clients

## Quick start

AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithGpu()
    .WithModel("llama3.2")
    .WithModel("nomic-embed-text")
    .WithModelHealthCheck()
    .WithOpenTelemetry();

builder.AddProject<Projects.Api>("api")
    .WithReference(ollama)
    .WaitFor(ollama);

builder.Build().Run();
```

Consuming project (`Microsoft.Extensions.AI` + OllamaSharp shown as an example):

```csharp
var endpoint = builder.Configuration.GetConnectionString("ollama")!;
builder.Services.AddChatClient(new OllamaApiClient(new Uri(endpoint), "llama3.2"));
```

## vLLM example

AppHost (one vLLM resource serves one model):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var vllm = builder.AddVllm("vllm", "Qwen/Qwen2.5-7B-Instruct")
    .WithDataVolume()
    .WithGpu(GpuVendor.Nvidia, count: 1)
    .WithModelHealthCheck()
    .WithOpenTelemetry();

builder.AddProject<Projects.Api>("api")
    .WithReference(vllm)
    .WaitFor(vllm);

builder.Build().Run();
```

Consuming project. The connection string has the form `Endpoint=http://host:port/v1;Model=name`, so it is parsed
with `DbConnectionStringBuilder` and passed to an OpenAI-compatible client (`OpenAI` package):

```csharp
using System.ClientModel;
using System.Data.Common;
using OpenAI;

var connectionString = builder.Configuration.GetConnectionString("vllm")!;
var parts = new DbConnectionStringBuilder { ConnectionString = connectionString };
var endpoint = (string)parts["Endpoint"];
var model = (string)parts["Model"];

var openAi = new OpenAIClient(
    new ApiKeyCredential("unused"),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

var chatClient = openAi.GetChatClient(model);
```

## GPU passthrough

`WithGpu()` adds the runtime arguments below to the container. It works the same way for Ollama and vLLM.

| Vendor | Runtime args | Prerequisites |
|--------|--------------|---------------|
| `Nvidia` (default) | `--gpus all`, or `--gpus N` when `count: N` is passed | NVIDIA Container Toolkit installed on the host |
| `Amd` | `--device /dev/kfd --device /dev/dri` | ROCm installed on the host. For Ollama the image tag switches to `rocm` |

> **Note:** `WithGpu()` on Docker Desktop for Mac is not supported. There is no GPU passthrough there, so the container
> fails at start with a device driver error instead of falling back to CPU. Remove `WithGpu()` for local runs on a Mac.

## Installation

```bash
dotnet add package AspireLocalAI
```

Requires .NET 9 SDK and Docker (or Podman). GPU support needs the NVIDIA Container Toolkit or ROCm.

## API overview

| Type | Purpose |
|------|---------|
| `OllamaResource` | Ollama container, multiple models, `Endpoint=...` connection string |
| `VllmResource` | vLLM container, single model, OpenAI-compatible `/v1` endpoint |
| `ILocalModelServer` | Common contract used by health checks and telemetry |
| `LocalAIResourceBuilderExtensions` | `AddOllama`, `AddVllm`, `WithModel`, `WithDataVolume`, `WithGpu`, `WithModelHealthCheck`, `WithOpenTelemetry` |
| `LocalAIHealthCheckOptions` | Timeout, `RequireAllModels`, `WarmupPrompt` |

## Health checks

`WithModelHealthCheck()` registers a health check under the key `<resource>_model_check` (for example `ollama_model_check`)
and attaches it to the resource. The check probes the server on each run:

- Ollama: `GET /api/tags`, reads `models[].name`. A `:latest` suffix is ignored on both sides when matching names.
- vLLM: `GET /v1/models`, reads `data[].id`.

| Condition | Status |
|-----------|--------|
| Endpoint not allocated yet | Unhealthy |
| Endpoint unreachable, probe times out, or returns an error or malformed body | Unhealthy |
| Models missing and `RequireAllModels = true` | Degraded |
| Models missing and `RequireAllModels = false` | Healthy (only reachability is verified) |
| All declared models present | Healthy |

`LocalAIHealthCheckOptions` tunes the check:

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Timeout` | `TimeSpan` | 10 seconds | Maximum time for a single probe request. Listing models on a cold Ollama instance can be slow. |
| `RequireAllModels` | `bool` | `true` | When `true`, a missing model makes the check Degraded. When `false`, only endpoint reachability is checked. |
| `WarmupPrompt` | `string?` | `null` | When set, sends a one-token prompt once all declared models are present, so the first real request does not pay the model load cost. A warm-up failure is only reported in the check data and is retried on the next run; it never marks the resource unhealthy. |

```csharp
var ollama = builder.AddOllama("ollama")
    .WithModel("llama3.2")
    .WithModelHealthCheck(new LocalAIHealthCheckOptions { Timeout = TimeSpan.FromSeconds(30), WarmupPrompt = "hi" });

builder.AddProject<Projects.Api>("api")
    .WithReference(ollama)
    .WaitFor(ollama);
```

`WaitFor(ollama)` holds the consuming project until the resource reports Healthy. A Degraded check (missing model) therefore
keeps the dependent project waiting, so pull the models you need or set `RequireAllModels = false` if that is not wanted.

## Connection strings

| Server | Connection string | Example |
|--------|-------------------|---------|
| Ollama | `Endpoint=http://host:port` | `Endpoint=http://localhost:11434` |
| vLLM | `Endpoint=http://host:port/v1;Model=name` | `Endpoint=http://localhost:8000/v1;Model=Qwen/Qwen2.5-7B-Instruct` |

`WithReference(resource)` injects the connection string into the consuming project as the `ConnectionStrings__<name>`
environment variable, where `<name>` is the resource name. Read it with `GetConnectionString("<name>")`. The host
and port are assigned by Aspire at run time, so never hardcode them.

Model weights are stored in a volume when you call `WithDataVolume()`. The default volume name is `<resource>-data`,
and it can be overridden with `WithDataVolume("name")`.

| Server | Mount path in the container |
|--------|-----------------------------|
| Ollama | `/root/.ollama` |
| vLLM | `/root/.cache/huggingface` |

## Status

Early scaffold. Public API shape is defined; implementations are in progress. See issues for the roadmap.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) - Copyright 2026 Vladyslav Zaiets
