#!/bin/bash

set -ex


main() {
    local version=7.2.1-2017.11
    local build_tools=gcc-linaro-${version}-x86_64_arm-linux-gnueabihf

    if [[ ! -f Dockerfile ]]
    then
        echo "Expected Dockerfile in current directory."
        return 1
    fi

    # extract Linaro toolchain 
    curl -L -o ${build_tools}.tar.xz https://releases.linaro.org/components/toolchain/binaries/latest/arm-linux-gnueabihf/${build_tools}.tar.xz 
    xzcat ${build_tools}.tar.xz | \
        tar -xvf -
    
    rm ${build_tools}.tar.xz

    # Once toolchain is downloaded and extracted, build docker image with toolchain
    docker build --build-arg TOOLCHAIN=${build_tools} --tag ${build_tools}:0.1 .

    # cleanup
    rm -fr gcc-linaro-${version}-x86_64_arm-linux-gnueabihf
}

main "${@}"
