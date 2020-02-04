#!/bin/bash

DEFAULT_TOOLCHAIN_ARM32_VERSION_PATH=7.3-2018.05
DEFAULT_TOOLCHAIN_ARM64_VERSION_PATH=7.4-2019.02
DEFAULT_TOOLCHAIN_ARM32_VERSION=7.3.1-2018.05
DEFAULT_TOOLCHAIN_ARM64_VERSION=7.4.1-2019.02

extract_arm32_toolchain() {
    local version=
    local version_path=
    if [[ $# -eq 0 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM32_VERSION
       version_path=$DEFAULT_TOOLCHAIN_ARM32_VERSION_PATH
    else
        version=$1
        version_path=$2
    fi
    local build_tools=gcc-linaro-${version}-x86_64_arm-linux-gnueabihf

    echo "Extracting $build_tools"
    curl -L -o ${build_tools}.tar.xz https://releases.linaro.org/components/toolchain/binaries/${version_path}/arm-linux-gnueabihf/${build_tools}.tar.xz
    xzcat ${build_tools}.tar.xz | \
        tar -xvf -

    rm ${build_tools}.tar.xz
}

cleanup_arm32_toolchain() {
    local version=
    if [[ $# -eq 0 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM32_VERSION
    else
        version=$1
    fi
    local build_tools=gcc-linaro-${version}-x86_64_arm-linux-gnueabihf

    echo "Cleaning up $build_tools"
    rm -fr $build_tools
}

build_arm32_toolchain_container () {
    local dockerfile=$1
    local registry=$2
    local tag_version=$3
    local version=
    if [[ $# -eq 3 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM32_VERSION
    else
        version=$4
    fi
    local build_tools=gcc-linaro-${version}-x86_64_arm-linux-gnueabihf

    docker build -f $dockerfile --build-arg TOOLCHAIN=${build_tools} --tag ${registry}/${build_tools}:${tag_version} .
}

extract_arm64_toolchain() {
    local version=
    local version_path=
    if [[ $# -eq 0 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM64_VERSION
       version_path=$DEFAULT_TOOLCHAIN_ARM64_VERSION_PATH
    else
        version=$1
        version_path=$2
    fi
    local build_tools=gcc-linaro-${version}-x86_64_aarch64-linux-gnu

    echo "Extracting $build_tools"
    curl -L -o ${build_tools}.tar.xz https://releases.linaro.org/components/toolchain/binaries/${version_path}/aarch64-linux-gnu/${build_tools}.tar.xz
    xzcat ${build_tools}.tar.xz | \
        tar -xvf -

    rm ${build_tools}.tar.xz
}

cleanup_arm64_toolchain() {
    local version=
    if [[ $# -eq 0 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM64_VERSION
    else
        version=$1
    fi
    local build_tools=gcc-linaro-${version}-x86_64_aarch64-linux-gnu

    echo "Cleaning up $build_tools"
    rm -fr $build_tools
}

build_arm64_toolchain_container () {
    local dockerfile=$1
    local registry=$2
    local tag_version=$3
    local version=
    if [[ $# -eq 3 ]]
    then
       version=$DEFAULT_TOOLCHAIN_ARM64_VERSION
    else
        version=$4
    fi
    local build_tools=gcc-linaro-${version}-x86_64_aarch64-linux-gnu

    docker build -f $dockerfile --build-arg TOOLCHAIN=${build_tools} --tag ${registry}/${build_tools}:${tag_version} .
}
