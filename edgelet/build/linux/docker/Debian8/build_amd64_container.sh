#!/bin/bash

set -ex


main() {
    if [[ ! -f Dockerfile.x86_64-unknown-linux-gnu ]]
    then
        echo "Expected Dockerfile in current directory."
        return 1
    fi

    docker build -f Dockerfile.x86_64-unknown-linux-gnu --tag edgebuilds.azurecr.io/debian-build:8.11 .
}

main "${@}"
