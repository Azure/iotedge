# adapted from https://github.com/japaric/cross
set -ex

main() {
    local version=$1
    local triple=$2
    local sysroot=$3

    local dependencies=(
        curl
        make
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
    
    curl -L https://github.com/curl/curl/releases/download/curl-7_59_0/curl-$version.tar.gz | \
        tar --strip-components=1 -xz
    AR=${triple}-ar CC=${triple}-gcc ./configure --host x86_64-unknown-linux-gnueabi --build $triple --with-sysroot $sysroot --prefix ${sysroot}/usr
    make -j$(nproc)
    make install

    # clean up
    popd

    rm -rf $td
    rm $0
}

main "${@}"
