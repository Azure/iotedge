# Copilot Instructions for Azure IoT Edge

## Repository Overview

Azure IoT Edge is a dual-language (C# and Rust) system with three main components:

- **Edge Agent** (`edge-agent/`) — C# service that manages module lifecycle, deployments, and desired-state reconciliation via IoT Hub twin
- **Edge Hub** (`edge-hub/`) — C# service acting as a local gateway for MQTT, AMQP, and HTTP protocols with message routing between modules and the cloud
- **IoT Edge Security Daemon** (`edgelet/`) — Rust daemon (`aziot-edged`) providing workload and management APIs, module provisioning, and integration with the Azure IoT Identity Service
- **Edge Modules** (`edge-modules/`) — Standalone modules including `api-proxy-module` (Rust), `metrics-collector` (C#), and `SimulatedTemperatureSensor` (C#)
- **MQTT library** (`mqtt/`) — Rust MQTT3 client library and `edgelet-client` used by `api-proxy-module`

Shared C# utilities live in `edge-util/` (Option type, Preconditions, storage abstractions, RocksDB bindings).

## Build and Test

### C# (.NET 8.0)

Solution: `Microsoft.Azure.Devices.Edge.sln`

```bash
# Build everything (Linux)
scripts/linux/buildBranch.sh -c Release --no-rocksdb-bin --skip-quickstart

# Run all unit tests
dotnet test --configuration Release --filter 'Category=Unit' --logger trx

# Run a single test by fully qualified name
dotnet test --filter "FullyQualifiedName~MyNamespace.MyClass.MyMethod" --logger trx --no-build

# Run integration tests (requires Azure KeyVault cert; see doc/devguide.md)
scripts/linux/runTests.sh "Category=Integration" "Release"
```

Test framework: **xUnit** with **Moq**. Tests use trait-based categories: `[Unit]`, `[Integration]`, `[Bvt]`.

### Rust (toolchain 1.73)

Workspace root: `edgelet/Cargo.toml`

```bash
# Build the daemon and CLI
cd edgelet && cargo build -p aziot-edged -p iotedge

# Run all Rust tests
cd edgelet && cargo test --all

# Run tests for a single crate
cd edgelet && cargo test -p edgelet-core

# Run a single test by name
cd edgelet && cargo test -p edgelet-core -- test_name
```

The `api-proxy-module` is a separate Rust project at `edge-modules/api-proxy-module/`.

### Rust Linting

CI enforces both `rustfmt` and `clippy`. Run before submitting:

```bash
cd edgelet && cargo fmt --all -- --check
cd edgelet && cargo clippy --all && cargo clippy --all --tests
```

### C# Linting

StyleCop.Analyzers runs on Release/CheckInBuild configurations. Warnings are treated as errors (`TreatWarningsAsErrors=True`).

## CI Pipelines

CI uses **Azure Pipelines** (not GitHub Actions). Separate pipelines run based on which files changed:

| Pipeline | Trigger condition | What it checks |
|---|---|---|
| `builds/checkin/dotnet.yaml` | Changes outside `edgelet/`, `doc/`, `mqtt/` | C# unit tests, 58% code coverage |
| `builds/checkin/edgelet.yaml` | Changes in `edgelet/` or `builds/` | Rust build + test (amd64, arm32, arm64), `rustfmt`, `clippy`, 30% coverage |
| `builds/checkin/api-proxy.yaml` | Changes in `edge-modules/api-proxy-module/` or `mqtt/edgelet-client/` | API proxy Rust build + test |

## Architecture Patterns

### C# Conventions

- **Dependency Injection**: Autofac with module registration pattern. DI modules live in `*/Service/modules/` directories (e.g., `EdgeletModule.cs`, `RoutingModule.cs`)
- **Custom `Option<T>` type**: Defined in `edge-util/src/Microsoft.Azure.Devices.Edge.Util/Option.cs`. Used pervasively instead of nullable references. Supports `ForEachAsync`, `Map`, `OrDefault`, `Exists` — treat it like a Rust/Scala Option
- **`Preconditions` class**: Guard methods (`CheckNotNull`, `CheckRange`, etc.) in `edge-util/`. All constructor parameters are validated with `Preconditions.CheckNotNull(param, nameof(param))`
- **Async naming**: All async methods must end with `Async` suffix (enforced by `.editorconfig`)
- **`this.` qualifier**: Required for all field, property, method, and event references
- **Event-based logging**: Domain events (e.g., `Events.AgentCreated()`) rather than direct logger calls in core logic. Uses Serilog via `Microsoft.Extensions.Logging`
- **Exception filtering**: `catch (Exception ex) when (!ex.IsFatal())` pattern for non-fatal error handling

### Rust Conventions

- **Lint attributes on every crate**: `#![deny(rust_2018_idioms, warnings)]` and `#![deny(clippy::all, clippy::pedantic)]` with specific `#![allow(...)]` exceptions
- **Async runtime**: Tokio
- **Error handling**: Mix of `anyhow` (in modules like api-proxy) and custom error types
- **API definitions**: OpenAPI/Swagger YAML specs in `edgelet/api/` — some Rust code is generated from these via a modified `swagger-codegen`
- **Cargo registry**: Uses a private Azure DevOps Cargo feed (`iotedge_PublicPackages`) as a mirror of crates.io, configured in `edgelet/.cargo/config.toml`

### Docker

Both Edge Agent and Edge Hub use `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` as base image. They bundle `librocksdb.so` and support multi-platform builds (amd64, arm32v7, arm64v8). Edge Hub exposes ports 1883, 8883 (MQTT), 5671 (AMQP), and 443 (HTTPS).

## Key Files

- `netcoreappVersion.props` — Sets .NET target framework and enables package lock files for all C# projects
- `rust-toolchain.toml` — Pins the Rust toolchain version
- `edgelet/.cargo/config.toml` — Cargo registry configuration (private feed mirror)
- `versionInfo.json` — Product version (build system replaces `BUILDNUMBER` and `COMMITID` placeholders)
- `doc/devguide.md` — C# development guide
- `edgelet/doc/devguide.md` — Rust development guide (includes local daemon setup instructions)
