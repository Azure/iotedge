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

# Download Mariner repo and build toolkit
git clone https://github.com/microsoft/CBL-Mariner.git
cd CBL-Mariner
git checkout tags/1.0-stable
cd toolkit
sudo make package-toolkit REBUILD_TOOLS=y
cd ..

# Move toolkit to build root for Mariner flavor
sudo mv out/toolkit-*.tar.gz "${MARINER_BUILD_ROOT}/toolkit.tar.gz"

# Build Mariner RPM packages
cd ${MARINER_BUILD_ROOT}
sudo tar xzf toolkit.tar.gz
cd toolkit
sudo make clean

echo "Version is: "${VERSION}"
ls ..
cat ${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/azure-iotedge.signatures.json
cat ${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/libiothsm-std.signatures.json
ls ${MARINER_BUILD_ROOT}/SPECS
ls ${MARINER_BUILD_ROOT}/SPECS/azure-iotedge
ls ${MARINER_BUILD_ROOT}/SPECS/libiothsm-std

sudo make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" CONFIG_FILE= -j$(nproc) LOG_LEVEL=trace
