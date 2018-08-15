#!/bin/bash

set -ex
. ./containers.sh
. ./toolchain.sh

main() {
    local target_triple=$1
    local dockerfile=Dockerfile.centos7.${target_triple}

    if [[ ! -f $dockerfile ]]
    then
        echo "Expected $dockerfile in current directory."
        return 1
    fi

    # extract Linaro toolchain
    extract_arm32_toolchain

    # Once toolchain is downloaded and extracted, build docker image with toolchain
    build_arm32_toolchain_container $dockerfile $CONTAINER_REGISTRY centos_7.5-1

    # cleanup
    cleanup_arm32_toolchain

}

main "${@}"
