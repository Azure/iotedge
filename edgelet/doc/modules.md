# Alternative Module Implementations
The IoT Edge runtime was designed to abstract the concept of a module from its implementation technology.
Currently, the only module implementation is based on Docker containers.
However, it is possible to implement a few traits and back a module by some other technology.
This document describes these traits.

## Traits
The various trait definitions are found in the [edgelet-core](../edgelet-core) crate in [module.rs][1].
These traits are:
  * `Module`
  * `ModuleRuntime`
  * `ModuleRegistry`

There is also an additional `struct` named `ModuleSpec` which is generic in a `Config` type.

### Module Trait
The `Module` trait contains information about an instantiated module.
The current trait looks as follows (the code is the definitive source):

```rust
pub trait Module {
    type Config;
    type Error: Fail;
    type RuntimeStateFuture: Future<Item = ModuleRuntimeState, Error = Self::Error>;

    fn name(&self) -> &str;
    fn type_(&self) -> &str;
    fn config(&self) -> &Self::Config;
    fn runtime_state(&self) -> Self::RuntimeStateFuture;
}
```

All modules have a `name`, a `type`, a `config`, and can return their runtime state when queried.
The name and type are strings.
The config is an [associated type][2] of the module.
This type must be filled in by the implementing `struct`.

The `config` is only used by the implementation of the corresponding `ModuleRuntime` trait.
This means that the core of the daemon does not need to understand this `config`.
It is an opaque blob of information that the implementation of the `ModuleRuntime` trait understands.

### ModuleRuntime Trait
Implementations of the `ModuleRuntime` trait instantiate modules (and dispose them), start, stop, and query the state of these modules.
The current trait looks as follows:

```rust
pub trait ModuleRuntime {
    type Error: Fail;

    type Config;
    type Module: Module<Config = Self::Config>;
    type ModuleRegistry: ModuleRegistry<Config = Self::Config, Error = Self::Error>;
    type Chunk: AsRef<[u8]>;
    type Logs: Stream<Item = Self::Chunk, Error = Self::Error>;

    type CreateFuture: Future<Item = (), Error = Self::Error>;
    type InitFuture: Future<Item = (), Error = Self::Error>;
    type ListFuture: Future<Item = Vec<Self::Module>, Error = Self::Error>;
    type LogsFuture: Future<Item = Self::Logs, Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;
    type RestartFuture: Future<Item = (), Error = Self::Error>;
    type StartFuture: Future<Item = (), Error = Self::Error>;
    type StopFuture: Future<Item = (), Error = Self::Error>;
    type SystemInfoFuture: Future<Item = SystemInfo, Error = Self::Error>;
    type RemoveAllFuture: Future<Item = (), Error = Self::Error>;

    fn init(&self) -> Self::InitFuture;
    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture;
    fn start(&self, id: &str) -> Self::StartFuture;
    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture;
    fn restart(&self, id: &str) -> Self::RestartFuture;
    fn remove(&self, id: &str) -> Self::RemoveFuture;
    fn system_info(&self) -> Self::SystemInfoFuture;
    fn list(&self) -> Self::ListFuture;
    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture;
    fn registry(&self) -> &Self::ModuleRegistry;
    fn remove_all(&self) -> Self::RemoveAllFuture;
}
```

This trait has one main associated type, a `Module`, which implements the `Module` trait with a specific `Config` implementation.
A module runtime can create, delete, start, stop, and list modules.
The various "future" associated types are here because Rust does not allow generic return types.
This allows implementors to choose the specific [Future][3] type to return.

A new module type will need a `ModuleRuntime` implementation.

### ModuleSpec struct
A `ModuleSpec` is used by implementations of `ModuleRuntime` to create new modules.

The current struct looks as follows:

```rust
#[derive(Deserialize, Debug, Serialize)]
pub struct ModuleSpec<T> {
    name: String,
    #[serde(rename = "type")]
    type_: String,
    config: T,
    #[serde(default = "HashMap::new")]
    env: HashMap<String, String>,
}
```

The `struct` includes the basic information needed for modules.
It is generic in a `Config` type.

The `Config` type holds the implementation specific information.
For example, the `DockerConfig` struct includes the `image` and `createOptions` to use when creating the container.
Due to the way the type bounds are set up, this `Config` implementation must be the same as the associated types of the `Module` and `ModuleRuntime` trait implementations.

### ModuleRegistry trait
Implementations of the `ModuleRegistry` trait handle downloading (pulling) and removing of a module's packages.

The current trait looks as follows:

```rust
pub trait ModuleRegistry {
    type Error: Fail;
    type PullFuture: Future<Item = (), Error = Self::Error>;
    type RemoveFuture: Future<Item = (), Error = Self::Error>;
    type Config;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture;
    fn remove(&self, name: &str) -> Self::RemoveFuture;
}
```

Like all of the other traits, this trait also has an associated `Config`.

## Examples
There are a couple of implementations of these traits in the repository that can be referenced.

### Docker
A complete implementation of these traits for Docker is in the [edgelet-docker][4] crate. The interesting files are `config.rs`, `module.rs`, and `runtime.rs`.

This is the default implementation used in the current IoT Edge product.

### Test
There is an implementation in [edgelet-test-utils][5] that is used for unit testing and fault injection. It is a good example of a minimal implementation.

## Testing
The easiest method to test the new implementation in the daemon is to send HTTP requests to the management API.
There is a [swagger document][7] that describes the API.
This can be used to generate a client in your language of choice, or you can simply `curl` the endpoint.

## More Information
The `futures` and `failure` crates are used extensively across the daemon codebase. A working knowledge of these two crates will be required to implement the traits described in this document.

More information can be found here:
* [failure][6] documentation
* [futures][3] documentation


[1]: ../edgelet-core/src/module.rs
[2]: https://doc.rust-lang.org/book/second-edition/ch19-03-advanced-traits.html#specifying-placeholder-types-in-trait-definitions-with-associated-types
[3]: https://docs.rs/futures/0.1.24/futures/
[4]: ../edgelet-docker
[5]: ../edgelet-test-utils
[6]: https://docs.rs/failure/0.1
[7]: ../api/management.yaml