# Adapted from https://github.com/japaric/cross
set -ex

main() {
    local version=$1
    local triple=$2
    local sysroot=$3

    local dependencies=(
        curl
        make
	    zlib-devel
    )

    # NOTE cross toolchain must be already installed
    local purge_list=()
    for dep in ${dependencies[@]}; do
        if ! yum list -q installed $dep; then
            yum install -y $dep
            purge_list+=( $dep )
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
    if [ ${#purge_list[@]} -ne 0 ]; then
        yum remove -y ${purge_list[@]}
    fi


    popd

    rm -rf $td
    rm $0
}

main "${@}"
