#!/bin/bash

set -e

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

export DEBIAN_FRONTEND=noninteractive
export TZ=UTC

apt-get update -y
apt-get upgrade -y
apt-get install -y software-properties-common
add-apt-repository -y ppa:longsleep/golang-backports
apt-get update -y
apt-get install -y \
    cmake curl gcc g++ git jq make pkg-config \
    libclang1 libssl-dev llvm-dev \
    cpio genisoimage golang-1.13-go qemu-utils pigz python-pip python3-distutils rpm tar wget

rm -f /usr/bin/go
ln -vs /usr/lib/go-1.13/bin/go /usr/bin/go
if [ -f /.dockerenv ]; then
    mv /.dockerenv /.dockerenv.old
fi

echo 'Installing rustup'
curl -sSLf https://sh.rustup.rs | sh -s -- -y
. ~/.cargo/env


# need to use preview repo for the next 2 weeks untill mariner 2.0 gets moved to prod
case "${MARINER_RELEASE}" in
    '1.0-stable')
        UsePreview=n
        MarinerIdentity=mariner1
        PackageExtension="cm1"
        ;;
    '2.0-stable')
        UsePreview=y
        MarinerIdentity=mariner2
        PackageExtension="cm2"
        ;;
esac

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/${MarinerIdentity}"
REVISION="${REVISION:-1}"

pushd $EDGELET_ROOT
case "${MARINER_ARCH}" in
    'x86_64')
        rustup target add amd64-unknown-linux-gnu
        ;;
    'arm64')
        rustup target add aarch64-unknown-linux-gnu
        ;;
esac
popd

# Update versions in specfiles
pushd "${BUILD_REPOSITORY_LOCALPATH}"
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/aziot-edge/aziot-edge.signatures.json
sed -i "s/@@VERSION@@/${VERSION}/g" builds/mariner/SPECS/aziot-edge/aziot-edge.spec
sed -i "s/@@RELEASE@@/${REVISION}/g" builds/mariner/SPECS/aziot-edge/aziot-edge.spec
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

[source."https://github.com/Azure/iot-identity-service"]
git = "https://github.com/Azure/iot-identity-service"
branch = "release/1.2"
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
echo "Creating source tarball aziot-edge-${VERSION}.tar.gz"
tar -czvf aziot-edge-${VERSION}.tar.gz --transform s/./aziot-edge-${VERSION}/ -C "${BUILD_REPOSITORY_LOCALPATH}" .
popd


# Copy source tarball to expected locations
mkdir -p "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/SOURCES/"
mv "${tmp_dir}/aziot-edge-${VERSION}.tar.gz" "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/SOURCES/"
rm -rf ${tmp_dir}

tmp_dir=$(mktemp -d)
pushd $tmp_dir
mkdir "rust"
cp -r ~/.cargo "rust"
cp -r ~/.rustup "rust"
tar cf "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/SOURCES/rust.tar.gz" "rust"
popd

# Download Mariner repo and build toolkit
echo "Cloning the \"${MARINER_RELEASE}\" tag of the CBL-Mariner repo."
git clone https://github.com/microsoft/CBL-Mariner.git
pushd CBL-Mariner
git checkout ${MARINER_RELEASE}
pushd toolkit
make package-toolkit REBUILD_TOOLS=y
popd
mv out/toolkit-*.tar.gz "${MARINER_BUILD_ROOT}/toolkit.tar.gz"
popd

# copy over IIS RPM
mkdir -p ${MARINER_BUILD_ROOT}/out/RPMS/${MARINER_ARCH}
cp aziot-identity-service/${MarinerIdentity}/${ARCH}/aziot-identity-service-*.$PackageExtension.${MARINER_ARCH}.rpm ${MARINER_BUILD_ROOT}/out/RPMS/${MARINER_ARCH}

# Prepare toolkit
pushd ${MARINER_BUILD_ROOT}
tar xzf toolkit.tar.gz
pushd toolkit

# Build Mariner RPM packages
make build-packages PACKAGE_BUILD_LIST="aziot-edge" SRPM_FILE_SIGNATURE_HANDLING=update USE_PREVIEW_REPO=$UsePreview CONFIG_FILE= -j$(nproc)
popd
popd