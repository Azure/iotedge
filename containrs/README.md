# containrs

A lightweight library for ingesting [OCI-compliant](https://www.opencontainers.org/) container images.

## Roadmap

- [ ] Pulling images
    - [x] Parsing and Normalizing docker-style image references 
    - [ ] Repository Authentication
        - [x] Docker Anonymous
        - [ ] OAuth2
    - [x] Pulling image manifests
    - [ ] Pulling image data
    - [ ] Digest validation
- [ ] Unpacking images
    - [ ] Maintain a Metadata store
    - ... (TBD)
- [ ] Interface with runc
