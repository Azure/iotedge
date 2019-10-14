# containrs

A lightweight library for working with [OCI-compliant](https://www.opencontainers.org/) container images.

## Issues

Until a proper issue tracker is set up, the quickest way to list all known issues would be to run `git grep -EI "TODO|FIXME|HACK|XXX|unimplemented` in the project's root directory :smile:

## Roadmap

- [ ] Registry Auth
    - [x] Docker Anonymous
    - [x] OAuth2 - Username Password
    - [ ] OAuth2 - Refresh Token
    - [ ] ACR
- [ ] Registry Endpoints
    - [x] base
    - [x] \_catalog
    - [x] tags/list
    - [x] Pull
        - [x] manfiests
        - [x] blobs
    - [ ] Push (not currently in scope)
        - [ ] blobs
        - [ ] manfiests
- [x] Pulling images
    - [x] Parsing and Normalizing docker-style image references 
    - [x] Pulling image manifests
    - [x] Pulling image data
    - [x] Digest validation
    - [x] Exporting according to OCI Image Layout spec
- [ ] Unpacking images
    - [ ] Maintain a Metadata store
    - ... (TBD)
- [ ] Interface with containerd/runc
