# containrs

A lightweight library for ingesting [OCI-compliant](https://www.opencontainers.org/) container images.

## Issues

Until a proper issue tracker is set up, the quickest way to list all known issues would be to run `git grep -EI "TODO|FIXME|HACK|XXX` in the project's root directory :smile:

## Roadmap

- [ ] Pulling images
    - [x] Parsing and Normalizing docker-style image references 
    - [ ] Repository Authentication
        - [x] Docker Anonymous
        - [x] OAuth2 - Username Password
        - [ ] OAuth2 - Refresh Token
        - [ ] ACR
    - [x] Pulling image manifests
    - [x] Pulling image data
    - [ ] Digest validation
- [ ] Unpacking images
    - [ ] Maintain a Metadata store
    - ... (TBD)
- [ ] Interface with runc
