# Adapted from https://github.com/japaric/cross
set -ex

main() {
    local version=$1
    local triple=$2
    local sysroot=$3

    local dependencies=(
        curl
        make
	zlib1g-dev
    )

    # NOTE cross toolchain must be already installed
    apt-get update
    for dep in ${dependencies[@]}; do
        if ! dpkg -L $dep; then
            apt-get install --no-install-recommends -y $dep
        fi
    done

    td=$(mktemp -d)

    pushd $td
    
    curl https://www.zlib.net/zlib-$version.tar.gz  | \
        tar --strip-components=1 -xz
    AR=${triple}-ar CC=${triple}-gcc ./configure --prefix ${sysroot}/usr
    make -j$(nproc)
    make install

    # clean up
    popd

    rm -rf $td
    rm $0
}

main "${@}"
