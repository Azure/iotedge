# cri-grpc

Bindings to Kubernete's [CRI](https://github.com/kubernetes/cri-api) gRPC API, backed by [`tonic`](https://docs.rs/tonic).

## `proto/` Directory Structure

At the moment, `prost` (and by extension, `tonic`) doesn't support directory/dependency path remapping for `.proto` files. As such, the directory structure of the `proto/` directory must match the import paths specified in the `.proto` files _exactly_. This is the reason for the [seemingly unnecessary] deeply nested folder structure. 

## Updating `.proto` definitions

All `.proto` files are copied over verbatim from their respective repos. They are not patched or modified in any way.

As such, updating the `.proto` files should be as simple as adding the new/updated `.proto` files to the appropriate subdirectory, updating the build script, and bumping the crate version number.

**NOTE:** When updating, make sure to check if any new constants are added to the `constants.go` file in the `cri-api` repo!
