# Cargo Clippy Dockerfile

This Dockerfile creates an environment for running cargo clippy against your project.

## Building the Image

`docker build -t edgebuilds.azurecr.io/cargo-clippy:nightly .`

The scripts expect this image to be pushed to edgebuilds.azurecr.io.

## Running the Image

`docker run --rm -v "$PWD:/volume" edgebuilds.azurecr.io/cargo-clippy:nightly`
