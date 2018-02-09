# Cargo Fmt Dockerfile

This Dockerfile creates an environment for running cargo fmt against your project.

## Building the Image

`docker build -t edgebuilds.azurecr.io/cargo-fmt:nightly .`

The scripts expect this image to be pushed to edgebuilds.azurecr.io.

## Running the Image

`docker run --rm -v "$PWD:/volume" edgebuilds.azurecr.io/cargo-fmt:nightly`
