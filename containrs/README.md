# containrs

## Overview

### `containrs`

A library to interface with [OCI-compliant](https://www.opencontainers.org/) container registries from Rust.

_Author's note:_ I wrote this library while I was still "getting up to speed" on the intricacies of async/await in Rust, and how the OCI Distribution spec was structured. As such, it may be worthwhile to give the library a once-over, and re-think certain design decisions / improve certain bits of functionality. e.g: I feel as though the authentication flow could be improved, and that some client methods should return `impl Stream`.

#### Roadmap

- [ ] Registry Auth
    - [x] Docker Anonymous
    - [x] OAuth2 - Username Password
    - [ ] OAuth2 - Refresh Token
    - [ ] ACR
- [ ] Handling expired credentials / re-authentication
- [ ] Registry Endpoints
    - [x] base
    - [x] \_catalog
    - [x] tags/list
    - [x] Pull
        - [x] manfiests
        - [x] blobs
            - [x] **With resumable downloads!**
    - [ ] Push (not currently in scope)
        - [ ] blobs
        - [ ] manfiests
- [x] Pulling images
    - [x] Parsing and Normalizing docker-style image references
    - [x] Pulling image manifests
    - [x] Pulling image data
    - [x] Digest validation
    - [x] Exporting according to OCI Image Layout spec

### `containrs-cli`

A CLI for `containrs`.

- Provides low-level bindings to most `containrs::Client` methods (useful for testing / debugging)
- Implements some high-level flows
    - e.g: downloading a complete OCI container image, verifying data against the expected digest, and outputting it according to the OCI Image spec.

### `oci-distribution`, `oci-image` and `oci-runtime` (+ `oci-common`)

Rust bindings to types and utilities specified by the OCI Distribution, OCI Image, and OCI Runtime specifications. Support `serde` for serialization and deserialization, with certain types including custom `serde` implementations for stricter parse-time validation.

### `oci-digest`

A Rust implementation of the `Digest` types used throughout the OCI specification. Supports `serde` for serialization and deserialization, with built-in string validation.

### `docker-reference`

Strongly-typed docker image reference URIs.

```rust
use docker_reference::{Reference, ReferenceKind};

let reference = "iotedgeresources.azurecr.io/samplemodule:0.0.2-amd64".parse::<Reference>().unwrap();
assert_eq!(reference.repo(), "samplemodule");
assert_eq!(reference.registry(), "iotedgeresources.azurecr.io");
assert_eq!(reference.kind(), &ReferenceKind::Tag("0.0.2-amd64".to_string()));

// example of docker-style canonicalization
let reference = "ubuntu".parse::<Reference>().unwrap();
assert_eq!(reference.repo(), "library/ubuntu"); // automatically adds "library/" to bare name
assert_eq!(reference.registry(), "registry-1.docker.io"); // infers default docker registry
assert_eq!(reference.kind(), &ReferenceKind::Tag("latest".to_string())); // adds "latest" tag automatically
```

### `docker-scope`

Strongly-typed docker-style container registry authorization scopes (as used in WWW-Authenticate HTTP headers). Facilitates intelligent caching of authorization headers in `containrs`.

### `www-authenticate`

Strongly-typed `WWW-Authenticate` HTTP headers. Used extensively by `containrs` when authorizing requests.

## Outstanding issues / TODOs

Until a proper issue tracker is set up, the quickest way to list all known issues would be to run `git grep -C1 -EI "TODO|FIXME|HACK|XXX|unimplemented` in the project's root directory :smile:
