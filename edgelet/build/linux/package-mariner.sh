#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/mariner"
VERSION="$(cat "$EDGELET_ROOT/version.txt")"

# Pull Cargo deps and extract
echo "Vendoring Rust dependencies"
pushd "${EDGELET_ROOT}"
curl -o "azure-iotedge-1.0.10.3-cargo-vendor.zip" "https://marineriotedge.file.core.windows.net/mariner-build-env/azure-iotedge-1.0.10.3-cargo-vendor.zip?sv=2019-12-12&ss=bf&srt=o&sp=rl&se=2021-02-01T09:51:53Z&st=2021-01-04T01:51:53Z&spr=https&sig=yKbgAIjgol1nmv%2B3bFP%2BegX43i2bfmc82vhR%2F%2Bs7naw%3D"
unzip "azure-iotedge-1.0.10.3-cargo-vendor.zip"
rm "azure-iotedge-1.0.10.3-cargo-vendor.zip"
mkdir .cargo
cat > .cargo/config << EOF
[source.crates-io]
replace-with = "vendored-sources"

[source."https://github.com/Azure/hyperlocal-windows"]
git = "https://github.com/Azure/hyperlocal-windows"
branch = "master"
replace-with = "vendored-sources"

[source."https://github.com/Azure/mio-uds-windows.git"]
git = "https://github.com/Azure/mio-uds-windows.git"
branch = "master"
replace-with = "vendored-sources"

[source."https://github.com/Azure/tokio-uds-windows.git"]
git = "https://github.com/Azure/tokio-uds-windows.git"
branch = "master"
replace-with = "vendored-sources"

[source.vendored-sources]
directory = "vendor"
EOF
popd

# Create source tarball, including cargo dependencies
pushd "${BUILD_REPOSITORY_LOCALPATH}"
tar -czf azure-iotedge-${VERSION}.tar.gz --transform="s,^.*edgelet/,azure-iotedge-${VERSION}/edgelet/," "${EDGELET_ROOT}"
popd

# Update expected tarball hash
TARBALL_HASH=$(sha256sum "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" | awk '{print $1}')
# TODO: inject version into signatures.json and into the SPEC file definition in order to avoid hard-coding
sed -i 's/\("azure-iotedge-[0-9.]\+.tar.gz": "\)\([a-fA-F0-9]\+\)/\1'${TARBALL_HASH}'/g' "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/azure-iotedge.signatures.json"
sed -i 's/\("azure-iotedge-[0-9.]\+.tar.gz": "\)\([a-fA-F0-9]\+\)/\1'${TARBALL_HASH}'/g' "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/libiothsm-std.signatures.json"

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

# TODO: Remove log level trace
sudo make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" CONFIG_FILE= -j$(nproc) LOG_LEVEL=trace