#!/bin/bash

set -ex
. ./containers.sh

main() {
    local dockerfile=Dockerfile.mariner.x86_64-unknown-linux-gnu
    if [[ ! -f $dockerfile ]]
    then
        echo "Expected $dockerfile in current directory."
        return 1
    fi

    # Download Mariner toolkit
    curl "https://marineriotedge.file.core.windows.net/mariner-build-env/toolkit-1.0.20201018.tar.gz?sv=2019-12-12&ss=bfqt&srt=sco&sp=rl&se=2020-11-05T14:58:28Z&st=2020-10-26T05:58:28Z&spr=https&sig=%2BqFE6WNhF01fUMa5kOiNBwIlG7hvJo9LZaRgqLfe6Q8%3D" --output toolkit-1.0.20201018.tar.gz
    build_container $dockerfile $CONTAINER_REGISTRY/mariner-build:1.0.20201018-1
}

main "${@}"
