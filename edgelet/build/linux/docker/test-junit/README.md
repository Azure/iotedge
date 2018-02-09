# Cargo Test Junit Dockerfile

This Dockerfile creates an environment to convert `cargo test` output into junit output

## Building the Image

`docker build -t edgebuilds.azurecr.io/cargo-test-junit:nightly .`

The scripts expect this image to be pushed to edgebuilds.azurecr.io.

## Running the Image

`docker run --rm -v "$PWD:/volume" edgebuilds.azurecr.io/cargo-test-junit:nightly`
