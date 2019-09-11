# containrs

A lightweight library for ingesting [OCI-compliant](https://www.opencontainers.org/) container images.

## Project Structure

Currently, the project is divided into two packages:

- **containrs:** The core containrs library
- **containrs-cli:** A basic CLI to test / interact with containrs

## Roadmap

- [ ] Pulling images
    - [ ] Parsing and Normalizing docker-style image references 
    - [ ] Repository Authentication
        - [ ] Anonymous
        - [ ] Username + Password
    - [ ] Pulling image manifests
    - [ ] Pulling image data
    - [ ] Digest validation
- [ ] Unpacking images
    - [ ] Maintain a Metadata store
    - ... (TBD)
- [ ] Interface with runc
