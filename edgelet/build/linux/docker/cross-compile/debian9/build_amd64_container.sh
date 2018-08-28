#!/bin/bash

set -ex
. ./containers.sh

main() {
    local dockerfile=Dockerfile.debian9.x86_64-unknown-linux-gnu
    if [[ ! -f $dockerfile ]]
    then
        echo "Expected $dockerfile in current directory."
        return 1
    fi

    build_container $dockerfile $CONTAINER_REGISTRY/debian-build:9.5-1
}

main "${@}"
