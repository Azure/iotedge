#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
PROJECT_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"

REVISION="${REVISION:-1}"
DEFAULT_VERSION="$(cat "$PROJECT_ROOT/version.txt")"
VERSION="${VERSION:-$DEFAULT_VERSION}"

# Create source tarball
pushd "${PROJECT_ROOT}/.."
tar -czf azure-iotedge-${VERSION}.tar.gz --transform='s,^iotedge/,azure-iotedge-${VERSION}/,' "${PROJECT_ROOT}"
popd

# Update expected tarball hash
TARBALL_HASH=$(sha256sum "${PROJECT_ROOT}/../azure-iotedge-${VERSION}.tar.gz")
sed -i 's/\(azure-iotedge-[0-9.]+.tar.gz": "\)[a-fA-F0-9]+/\1${TARBALL_HASH}/g' "${PROJECT_ROOT}/SPECS/azure-iotedge/azure-iotedge.signatures.json"
sed -i 's/\(azure-iotedge-[0-9.]+.tar.gz": "\)[a-fA-F0-9]+/\1${TARBALL_HASH}/g' "${PROJECT_ROOT}/SPECS/libiothsm-std/libiothsm-std.signatures.json"

# Copy source tarball to expected locations
mkdir -p "${PROJECT_ROOT}/SPECS/azure-iotedge/SOURCES/"
cp "${PROJECT_ROOT}/../azure-iotedge-${VERSION}.tar.gz" "${PROJECT_ROOT}/SPECS/azure-iotedge/SOURCES/"
mkdir -p "${PROJECT_ROOT}/SPECS/libiothsm-std/SOURCES/"
cp "${PROJECT_ROOT}/../azure-iotedge-${VERSION}.tar.gz" "${PROJECT_ROOT}/SPECS/libiothsm-std/SOURCES/"

# Mariner package builds may not touch the internet, so provide Cargo dependencies
curl "https://marineriotedge.file.core.windows.net/mariner-build-env/azure-iotedge-1.0.10-cargo.tar.gz?sv=2019-12-12&ss=bf&srt=o&sp=rl&se=2020-11-12T01:27:33Z&st=2020-11-11T17:27:33Z&spr=https&sig=MvkiNjw9v%2Ff0gkWyc9npVosAGGDcMF0er8TkHg0dBiA%3D" --output azure-iotedge-1.0.10-cargo.tar.gz
mv azure-iotedge-1.0.10-cargo.tar.gz "${PROJECT_ROOT}/SPECS/azure-iotedge/SOURCES/"

# Download Mariner toolkit
curl "https://marineriotedge.file.core.windows.net/mariner-build-env/toolkit-1.0.20201029-x86_64.tar.gz?sv=2019-12-12&ss=bf&srt=o&sp=rl&se=2020-11-12T01:27:33Z&st=2020-11-11T17:27:33Z&spr=https&sig=MvkiNjw9v%2Ff0gkWyc9npVosAGGDcMF0er8TkHg0dBiA%3D" --output toolkit-1.0.20201018.tar.gz
mv toolkit-*.tar.gz ./toolkit.tar.gz
tar xzf toolkit.tar.gz
cd toolkit
sudo make clean

# Build Mariner RPM packages
sudo make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" CONFIG_FILE= -j$(nproc)
