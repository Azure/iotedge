#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

export DEBIAN_FRONTEND=noninteractive
export TZ=UTC


# need to use preview repo for the next 2 weeks untill mariner 2.0 gets moved to prod
case "${MARINER_RELEASE}" in
    '1.0-stable')
        UsePreview=n
        MarinerIdentity=mariner1
        PackageExtension="cm1"
        ;;
    '2.0-stable')
        UsePreview=n
        MarinerIdentity=mariner2
        PackageExtension="cm2"
        ;;
esac

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/${MarinerIdentity}"

apt-get update -y
apt-get upgrade -y
apt-get install -y software-properties-common
add-apt-repository -y ppa:longsleep/golang-backports
apt-get update -y
apt-get install -y \
    cmake curl gcc g++ git jq make pkg-config \
    libclang1 libssl-dev llvm-dev \
    cpio genisoimage golang-1.17-go qemu-utils pigz python-pip python3-distutils rpm tar wget

rm -f /usr/bin/go
ln -vs /usr/lib/go-1.17/bin/go /usr/bin/go
if [ -f /.dockerenv ]; then
    mv /.dockerenv /.dockerenv.old
fi

# Download Mariner repo and build toolkit
mkdir -p ${MARINER_BUILD_ROOT}
MarinerToolkitDir='/tmp/CBL-Mariner'
if ! [ -f "$MarinerToolkitDir/toolkit.tar.gz" ]; then
    rm -rf "$MarinerToolkitDir"
    git clone 'https://github.com/microsoft/CBL-Mariner.git' --branch "$MARINER_RELEASE" --depth 1 "$MarinerToolkitDir"
    pushd "$MarinerToolkitDir/toolkit/"
    make package-toolkit REBUILD_TOOLS=y
popd
    cp "$MarinerToolkitDir"/out/toolkit-*.tar.gz "${MARINER_BUILD_ROOT}/toolkit.tar.gz"
    rm -rf MarinerToolkitDir
fi

echo 'Installing rustup'
curl -sSLf https://sh.rustup.rs | sh -s -- -y
. ~/.cargo/env
pushd $EDGELET_ROOT
rustup target add x86_64-unknown-linux-gnu
popd

# Get version from this file, but omit strings like "~dev" which are illegal in Mariner RPM versions.
VERSION="$(cat "$EDGELET_ROOT/version.txt" | sed 's/~.*//')"
echo "Edgelet version is ${VERSION}"

# Update versions in specfiles
pushd "${BUILD_REPOSITORY_LOCALPATH}"
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/azure-iotedge/azure-iotedge.signatures.json
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/azure-iotedge/azure-iotedge.spec
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/libiothsm-std/libiothsm-std.signatures.json
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/libiothsm-std/libiothsm-std.spec
popd

pushd "${EDGELET_ROOT}"

# Cargo vendored dependencies are being downloaded now to be cached for mariner iotedge build.
echo "set cargo home location"
mkdir ${BUILD_REPOSITORY_LOCALPATH}/cargo-home
export CARGO_HOME=${BUILD_REPOSITORY_LOCALPATH}/cargo-home

echo "Vendoring Rust dependencies"
cargo vendor vendor


# Configure Cargo to use vendored the deps
mkdir .cargo
cat > .cargo/config << EOF
[source.crates-io]
replace-with = "vendored-sources"

[source."https://github.com/Azure/hyperlocal-windows"]
git = "https://github.com/Azure/hyperlocal-windows"
branch = "main"
replace-with = "vendored-sources"

[source."https://github.com/Azure/mio-uds-windows.git"]
git = "https://github.com/Azure/mio-uds-windows.git"
branch = "main"
replace-with = "vendored-sources"

[source."https://github.com/Azure/tokio-uds-windows.git"]
git = "https://github.com/Azure/tokio-uds-windows.git"
branch = "main"
replace-with = "vendored-sources"

[source.vendored-sources]
directory = "vendor"
EOF

# Include license file directly, since parent dir will not be present in the tarball
rm ./LICENSE
cp ../LICENSE ./LICENSE

popd # EDGELET_ROOT

# Create source tarball, including cargo dependencies and license
tmp_dir=$(mktemp -d -t mariner-iotedge-build-XXXXXXXXXX)
pushd $tmp_dir
echo "Creating source tarball azure-iotedge-${VERSION}.tar.gz"
tar -czvf azure-iotedge-${VERSION}.tar.gz --transform s/./azure-iotedge-${VERSION}/ -C "${BUILD_REPOSITORY_LOCALPATH}" .
popd

# Copy source tarball to expected locations
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
cp "${tmp_dir}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/"
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"
cp "${tmp_dir}/azure-iotedge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/SOURCES/"
rm -rf ${tmp_dir}

# Copy spec files to expected locations
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/azure-iotedge/azure-iotedge.signatures.json" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/azure-iotedge/azure-iotedge.spec" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/azure-iotedge/gcc-11.patch" "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/libiothsm-std/libiothsm-std.signatures.json" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/libiothsm-std/libiothsm-std.spec" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/libiothsm-std/gcc-11.patch" "${MARINER_BUILD_ROOT}/SPECS/libiothsm-std/"

tmp_dir=$(mktemp -d)
pushd $tmp_dir
mkdir "rust"
cp -r ~/.cargo "rust"
cp -r ~/.rustup "rust"
tar cf "${MARINER_BUILD_ROOT}/SPECS/azure-iotedge/SOURCES/rust.tar.gz" "rust"
popd

# Prepare toolkit
pushd ${MARINER_BUILD_ROOT}
tar xzf toolkit.tar.gz
pushd toolkit

# Build Mariner RPM packages
make build-packages PACKAGE_BUILD_LIST="azure-iotedge libiothsm-std" SRPM_FILE_SIGNATURE_HANDLING=update USE_PREVIEW_REPO=$UsePreview CONFIG_FILE= -j$(nproc)
popd
popd
