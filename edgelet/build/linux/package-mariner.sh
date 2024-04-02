#!/bin/bash

set -ex

# Get directory of running script
DIR="$(cd "$(dirname "$0")" && pwd)"

export DEBIAN_FRONTEND=noninteractive
export TZ=UTC


# need to use preview repo for the next 2 weeks untill mariner 2.0 gets moved to prod
case "${MARINER_RELEASE}" in
    '1.0'*)
        UsePreview=n
        MarinerIdentity=mariner1
        PackageExtension="cm1"
        ;;
    '2.0'*)
        UsePreview=n
        MarinerIdentity=mariner2
        PackageExtension="cm2"
        ;;
esac

BUILD_REPOSITORY_LOCALPATH="$(realpath "${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}")"
EDGELET_ROOT="${BUILD_REPOSITORY_LOCALPATH}/edgelet"
MARINER_BUILD_ROOT="${BUILD_REPOSITORY_LOCALPATH}/builds/${MarinerIdentity}"
REVISION="${REVISION:-1}"

apt-get update -y
apt-get upgrade -y
apt-get install -y software-properties-common
add-apt-repository -y ppa:longsleep/golang-backports
apt-get update -y
apt-get install -y \
    cmake curl gcc g++ git jq make pkg-config \
    libclang1 libssl-dev llvm-dev \
    cpio genisoimage golang-1.20-go qemu-utils pigz python3-pip python3-distutils rpm tar wget

rm -f /usr/bin/go
ln -vs /usr/lib/go-1.20/bin/go /usr/bin/go
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
case "${MARINER_ARCH}" in
    'x86_64')
        rustup target add x86_64-unknown-linux-gnu
        ;;
    'aarch64')
        rustup target add aarch64-unknown-linux-gnu
        ;;
esac
popd

# get aziot-identity-service version
IIS_VERSION=$(
    rpm -qp --queryformat '%{Version}' $(ls /src/aziot-identity-service-*.$PackageExtension.${MARINER_ARCH}.rpm | head -1)
)

# Update versions in specfiles
pushd "${BUILD_REPOSITORY_LOCALPATH}"
sed -i "s/@@VERSION@@/${VERSION}/g" ${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.signatures.json
sed -i "s/@@VERSION@@/${VERSION}/g" ${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.spec
sed -i "s/@@RELEASE@@/${REVISION}/g" ${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.spec

# Update aziot-identity-service version dependency
if [[ ! -z $IIS_VERSION ]]; then
    sed -i "s/@@IIS_VERSION@@/${IIS_VERSION}/g" ${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.spec
else
    # if a version could not be parsed remove the version dependency
    sed -i "s/aziot-identity-service = @@IIS_VERSION@@%{?dist}/aziot-identity-service/g" ${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.spec
fi
popd

pushd "${EDGELET_ROOT}"

# Cargo vendored dependencies are being downloaded now to be cached for mariner iotedge build.
echo "set cargo home location"
mkdir ${BUILD_REPOSITORY_LOCALPATH}/cargo-home
export CARGO_HOME=${BUILD_REPOSITORY_LOCALPATH}/cargo-home

echo "Vendoring Rust dependencies"
cargo vendor vendor


# Configure Cargo to use vendored the deps
mkdir -p .cargo
cat > .cargo/config << EOF
[source.crates-io]
replace-with = "vendored-sources"

[source."https://github.com/Azure/iot-identity-service"]
git = "https://github.com/Azure/iot-identity-service"
branch = "release/1.4"
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

# Copy spec files to expected locations
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.signatures.json" "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/"
cp "${BUILD_REPOSITORY_LOCALPATH}/builds/mariner/SPECS/aziot-edge/aziot-edge.spec" "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/"

tmp_dir=$(mktemp -d)
pushd $tmp_dir
mkdir "rust"
cp -r ~/.cargo "rust"
cp -r ~/.rustup "rust"
tar cf "${MARINER_BUILD_ROOT}/SPECS/aziot-edge/SOURCES/rust.tar.gz" "rust"
popd

# copy over IIS RPM
mkdir -p ${MARINER_BUILD_ROOT}/out/RPMS/${MARINER_ARCH}
mv /src/aziot-identity-service-*.$PackageExtension.${MARINER_ARCH}.rpm ${MARINER_BUILD_ROOT}/out/RPMS/${MARINER_ARCH}

# Prepare toolkit
pushd ${MARINER_BUILD_ROOT}
tar xzf toolkit.tar.gz
pushd toolkit

# Build Mariner RPM packages
make build-packages PACKAGE_BUILD_LIST="aziot-edge" SRPM_FILE_SIGNATURE_HANDLING=update USE_PREVIEW_REPO=$UsePreview CONFIG_FILE= -j$(nproc)
popd
popd
