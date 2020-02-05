# shellrt

A simple, JSON-based protocol to enable writing `edgelet` Module Runtimes as separate helper processes.

* * *

Instead of implementing module runtimes directly within the `edgelet` process, the shellrt API makes it possible to defer a module runtime implementation to a separate "plugin" runtime.

`iotedged` serializes Module Runtime requests (e.g: pull an image, create a container, etc...) into shellrt messages, spawns the plugin process, and sends the request to the plugin via stdin. The plugin will then execute the request, and return any acknowledgments / requested data back to `edgelet` via stdout.

The protocol itself is language-agnostic (plain JSON), but comes with first-class, strongly-typed Rust bindings.

## Relationship with `edgelet`

The shellrt API is closely modeled around the existing `ModuleRegistry` and `ModuleRuntime` traits in edgelet. While many methods have 1:1 corresponding methods, there are a few methods that are omited, added, or have slightly different parameters. That said, the APIs are "close-enough" to one another that integrating one with the other won't require too much data munging or orchestration.

`iotedged` trait | method            | corresponding shellrt API command | notes | implemented in `shellrt-api`
-----------------|-------------------|--------------|-|--------------------------------------------------------
`ModuleRegistry` | pull              | "img_pull"   | | [x]
.                | remove            | "img_remove" | | [x]
`ModuleRuntime`  | create            | "create"     | payload varies depending on shellrt runtime plugin | [x]
.                | get               | "status"     | | [x]
.                | start             | "start"      | | [x]
.                | stop              | "stop"       | | [x]
.                | restart           | "restart"    | | [x]
.                | remove            | "remove"     | | [x]
.                | system_info       | "sys_info"   | | [ ]
.                | list              | "list"       | | [x]
.                | list_with_details | .            | implemented using "list" + "status" | [x]
.                | logs              | "logs"       | | [x]
.                | remove_all        | .            | implemented using "list" + "remove" | [x]
`Module`         | runtime_state     | "status"     | | [x]
`ModuleTop`      | top               | "top"        | | [ ]

## Usage

At the moment, the shellrt JSON API is defined solely by it's reference implementation in Rust, which lives in the  `shellrt-api` crate. This crate only depends on `serde` and `serde_json`, and doesn't use any `async/await` syntax (making it suitable for inclusion in pre-async/await Rust codebases, such as `iotedged`).

This workspace includes a example shellrt server (`shellrt-driver`), and a work-in-progress, proof of concept shellrt plugin (`shellrt-containerd`)

### `shellrt-containerd`

A POC shellrt module runtime implementation backed by `containerd` and `containrs` (for pulling images).

While many features are implemented and working properly (i.e: pulling / removing images, and most of the container lifecycle methods) there are still several gaps in functionality that need to be filled in:

- No support for the "top" and "sys_info" messages.
    - Implementing "top" shouldn't be too tricky, through it will require bypassing the higher-level `containerd-cri` gRPC API, and directly querying the lower-level `containerd.services.tasks` gRPC API instead.
    - "sys_info" isn't provided by any of `containerd`'s gRPC APIs, and will have to be implemented manually (from Rust)
- Incomplete "log" handler
    - While basic "return all available logs" functionality works, more advanced features such as paging and trailing (`tail -f`) aren't currently supported. See `shellrt-containerd/src/handler/logs.rs` for details on what's left to be done.
- Stubbed "create" handler
    - Right now, the "create" handler has some hard-coded values when creating the container, only really suitable for creating a bare `ubuntu` image that runs and infinite `echo` loop.
- No networking support
    - Networking will have to be set up manually, most likely using the CNI
    - _Note:_ There will likely be some complications stemming from the [ab]use of `containerd-cri`, as spawned containers are technically running in a "pod sandboxes"

### `shellrt-driver`

Similar to the relationship between `ctr` and `containerd`, or `crictl` and CRI-servers, `shellrt-driver` is a barebones CLI to interact with shellrt plugins.

While it's entirely possible to test shellrt plugins by piping raw JSON via stdin, or by running them under more "heavyweight" shellrt servers, the `shellrt-driver` program provides a useful middle-ground -- automatically creating / validating JSON payloads, without being bloated with extraneous functionality.

## Example

Here's a quick example which builds the project, and spins up a ubuntu container using the `shellrt-driver` and `shellrt-containerd`.

```bash
cargo build
# enable some debug output
RUST_LOG=shellrt_containerd,shellrt_driver,containrs
# sudo is only required to communicate with the containerd socket.

# 1. pull the kubernetes "pause" image (required by `containerd-cri`)
sudo ./target/debug/shellrt-driver ./target/debug/shellrt-containerd img_pull k8s.gcr.io/pause@sha256:59eec8837a4d942cc19a52b8c09ea75121acc38114a2c68b98983ce9356b8610
# 2. pull the ubuntu image
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd img_pull ubuntu
# 3. create a new container "ubuntutest" using the ubuntu image
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd create containerd-cri ubuntutest ubuntu
# 4. start the container
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd start ubuntutest
# 5. inspect the container's logs
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd logs ubuntutest
# 6. stop the container
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd stop ubuntutest
# 7. remove the container
sudo ./target/dummy/debug/shellrt-driver ./target/dummy/debug/shellrt-containerd remove ubuntutest
```

## Outstanding issues and TODOs

Until a proper issue tracker is set up, the quickest way to list all known issues would be to run `git grep -C1 -EI "TODO|FIXME|HACK|XXX|unimplemented` in the project's root directory :smile:
