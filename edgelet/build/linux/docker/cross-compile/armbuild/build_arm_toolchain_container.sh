#!/bin/bash

set -ex

. ./containers.sh
. ./toolchain.sh

main() {
    local dockerfile=Dockerfile.arm.armv7-unknown-linux-gnueabi

    if [[ ! -f $dockerfile ]]
    then
        echo "Expected $dockerfile in current directory."
        return 1
    fi

    # extract Linaro toolchain
    extract_arm32_toolchain

    # Once toolchain is downloaded and extracted, build docker image with toolchain
    build_arm32_toolchain_container $dockerfile $CONTAINER_REGISTRY 0.2

    # cleanup
    cleanup_arm32_toolchain
}

main "${@}"
