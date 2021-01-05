#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/mariner"
VERSION="$(cat "$EDGELET_ROOT/version.txt")"

pushd "${EDGELET_ROOT}"

# Cargo vendored dependencies should be downloaded by the AzureCLI task. Extract them now.
echo "Vendoring Rust dependencies"
unzip "azure-iotedge-cargo-vendor.zip"
rm "azure-iotedge-cargo-vendor.zip"

# Configure Cargo to use vendored the deps
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

# Include license file directly, since parent dir will not be present in the tarball
rm ./LICENSE
cp ../LICENSE ./LICENSE

popd # EDGELET_ROOT

# Create source tarball, including cargo dependencies and license
pushd "${BUILD_REPOSITORY_LOCALPATH}"
tar -czf azure-iotedge-${VERSION}.tar.gz --transform="s,^.*edgelet/,azure-iotedge-${VERSION}/edgelet/," "${EDGELET_ROOT}"
popd

# Update expected tarball hash
TARBALL_HASH=$(sha256sum "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" | awk '{print $1}')
sed -i 's/\("azure-iotedge-[0-9.]\+.tar.gz": "\)\([a-fA-F0-9]\+\)/\1'${TARBALL_HASH}'/g' "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/azure-iotedge.signatures.json"
sed -i 's/\("azure-iotedge-[0-9.]\+.tar.gz": "\)\([a-fA-F0-9]\+\)/\1'${TARBALL_HASH}'/g' "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/libiothsm-std.signatures.json"

# Copy source tarball to expected locations
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
cp "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"
cp "${BUILD_REPOSITORY_LOCALPATH}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"

# Download Mariner repo and build toolkit
git clone https://github.com/microsoft/CBL-Mariner.git
pushd CBL-Mariner
git checkout tags/1.0-stable
pushd toolkit
sudo make package-toolkit REBUILD_TOOLS=y
popd
sudo mv out/toolkit-*.tar.gz "${MARINER_BUILD_ROOT}/toolkit.tar.gz"
popd

# Prepare toolkit
pushd ${MARINER_BUILD_ROOT}
sudo tar xzf toolkit.tar.gz
pushd toolkit

# Build Mariner RPM packages
sudo make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" CONFIG_FILE= -j$(nproc) LOG_LEVEL=trace
popd
popd

# Prevent publishing the Rust RPM as an artifact
pushd "${BUILD_REPOSITORY_LOCALPATH}"
rm "builds/mariner/out/RPMS/x86_64/rust-1.45.2-1.cm1.x86_64.rpm"
popd
