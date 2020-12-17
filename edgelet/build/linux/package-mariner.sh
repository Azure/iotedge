#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/mariner"

REVISION="${REVISION:-1}"
DEFAULT_VERSION="$(cat "$EDGELET_ROOT/version.txt")"
VERSION="${VERSION:-$DEFAULT_VERSION}"

# Create source tarball
pushd "${BUILD_REPOSITORY_LOCALPATH}"
tar -czf azure-iotedge-${VERSION}.tar.gz --transform='s,^iotedge/,azure-iotedge-${VERSION}/,' "${EDGELET_ROOT}"
popd

# Update expected tarball hash
TARBALL_HASH=$(sha256sum "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz")
sed -i 's/\(azure-iotedge-[0-9.]+.tar.gz": "\)[a-fA-F0-9]+/\1${TARBALL_HASH}/g' "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/azure-iotedge.signatures.json"
sed -i 's/\(azure-iotedge-[0-9.]+.tar.gz": "\)[a-fA-F0-9]+/\1${TARBALL_HASH}/g' "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/libiothsm-std.signatures.json"

# Copy source tarball to expected locations
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
cp "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"
cp "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"

# Mariner package builds may not touch the internet, so provide Cargo dependencies
curl "https://marineriotedge.file.core.windows.net/mariner-build-env/azure-iotedge-1.0.10-cargo.tar.gz?sv=2019-12-12&ss=bfqt&srt=o&sp=rlpx&se=2020-11-13T06:50:00Z&st=2020-11-12T22:50:00Z&spr=https&sig=islDvt5euCCf%2FyhKyAsY18TWepRgPq3pLkV9OQ%2FQbfs%3D" --output azure-iotedge-1.0.10-cargo.tar.gz
mv azure-iotedge-1.0.10-cargo.tar.gz "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"

# Download Mariner toolkit
curl "https://marineriotedge.file.core.windows.net/mariner-build-env/toolkit-1.0.20201029-x86_64.tar.gz?sv=2019-12-12&ss=bfqt&srt=o&sp=rlpx&se=2020-11-13T06:50:00Z&st=2020-11-12T22:50:00Z&spr=https&sig=islDvt5euCCf%2FyhKyAsY18TWepRgPq3pLkV9OQ%2FQbfs%3D" --output toolkit-1.0.20201018.tar.gz
mv toolkit-*.tar.gz ./toolkit.tar.gz
tar xzf toolkit.tar.gz
cd toolkit
sudo make clean

# Build Mariner RPM packages
sudo make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" CONFIG_FILE= -j$(nproc)
