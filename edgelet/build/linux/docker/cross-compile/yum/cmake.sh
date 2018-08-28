# Adapted from https://github.com/japaric/cross
set -ex

main() {
    local version=$1

    local dependencies=(
        curl
        gcc-c++
        make
    )

    local purge_list=()
    for dep in ${dependencies[@]}; do
        if ! yum list -q installed $dep; then
            yum install -y $dep
            purge_list+=( $dep )
        fi
    done

    local td=$(mktemp -d)

    pushd $td

    curl https://cmake.org/files/v${version%.*}/cmake-$version.tar.gz | \
        tar --strip-components 1 -xz
    ./bootstrap
    nice make -j$(nproc)
    make install

    # clean up
    popd

    if [ ${#purge_list[@]} -ne 0 ]; then
        yum remove -y ${purge_list[@]}
    fi

    rm -rf $td
    rm $0
}

main "${@}"
