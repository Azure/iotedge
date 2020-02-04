# Adapted from https://github.com/japaric/cross
set -ex

main() {

    local version=1.1.0g
    local os=$1 \
    local triple=$2
    local sysroot=$3

    local dependencies=(
        ca-certificates
        curl
        m4
        make
        perl
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
    curl https://www.openssl.org/source/openssl-$version.tar.gz | \
        tar --strip-components=1 -xz
    AR=${triple}ar CC=${triple}gcc ./Configure \
      --prefix=${sysroot}/usr \
      --openssldir=${sysroot}/usr \
      shared \
      no-asm \
      $os \
      -fPIC \
      ${@:4}
    make -j$(nproc)
    make install_sw

    # clean up

    popd

    rm -rf $td
    rm $0

}

main "${@}"

