# containerd-grpc

Bindings to [containerd](https://github.com/containerd/containerd/)'s gRPC API, backed by [`tonic`](https://docs.rs/tonic).

## API notes:

There are several places in the containerd gRPC API where a protobuf `Any` type is used. Determining the structure of this Any type will typically require spelunking through the containerd codebase to determine what kind of data is being marshalled/unmarhalled into it.

To aid in this unfortunate quest, here's some information that might help:

To make add some structure to the `Any` type, containerd uses a library called [`typeurl`](https://github.com/containerd/typeurl). In a nutshell, it transforms the raw protobuf `Any` type into a tagged union of `all protobuf builtin types` + `any "registered" types`, where "registered" types are specified at runtime by the client and server. These "registered" types are then marshalled as JSON strings, stuffed into the `Any` bytes array, and reconstructed on the other end.

Currently, the list of types containerd registers with `typeurl` can be found in [containerd/runtime/typeurl.go](https://github.com/containerd/containerd/blob/master/runtime/typeurl.go).

Notably, all of these types are OCI-Runtime spec JSON objects, which should be easy to encode as Serde-annotated Rust structures.

## `proto/` Directory Structure

containerd uses a go-specific protobuf build system called [`protobuild`](https://github.com/stevvooe/protobuild) which has certain features which `prost` doesn't, namely, the ability to specify directory/dependency path remapping. As such, the directory structure of the `proto/` directory must match the import paths specified in the containerd `.proto` files _exactly_, which results in the [seemingly unnecessary] nested folder structure. 

## Updating `.proto` definitions

All `.proto` files are copied over verbatim from their respective repos. They are not patched or modified in any way. 

As such, updating the `.proto` files should be as simple as adding the new/updated `.proto` files to the appropriate subdirectory, updating the build script, and bumping the crate version number.
