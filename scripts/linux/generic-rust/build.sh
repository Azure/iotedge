#!/bin/bash

###############################################################################
# This script builds the project
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}
SCRIPT_NAME=$(basename "$0")
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"
TARGET="x86_64-unknown-linux-gnu"
CARGO_ARGS=""
PACKAGES=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help              Print this help and exit."
    echo " -t, --target            Target architecture"
    echo " -c, --cargo             Path of cargo installation"
    echo " --project-root          The project root of the desired build"
    echo " --packages              The entities you want to build. By default this is a package name, not a manifest path"
    echo " --features              Space-separated list of features to activate"
    echo " -r, --release           Release build? (flag, default: false)"
    echo " --reduced-linker        Flag to limit the amount of objects sent to the linker"
    echo " --manifest-path         Flag specifying that the packages arg is a manifest path"
    echo " --no-default-features   Flag to specify whether to build with no default features"
    exit 1;
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ ${save_next_arg} -eq 1 ]; then
            TARGET="$arg"
            save_next_arg=0
        elif [ ${save_next_arg} -eq 2 ]; then
            CARGO="$arg"
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            PROJECT_ROOT=${PROJECT_ROOT}/$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 4 ]; then
            # Rather than pass an array, pass a semicolon delimited string
            # https://stackoverflow.com/questions/1063347/passing-arrays-as-parameters-in-bash
            PACKAGES=$arg
            IFS=';' read -a PACKAGES <<< "$PACKAGES"
            save_next_arg=0
        elif [ ${save_next_arg} -eq 5 ]; then
            CARGO_ARGS="${CARGO_ARGS} --features \"${arg}\""
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target" ) save_next_arg=1;;
                "-c" | "--cargo" ) save_next_arg=2;;
                "--project-root" ) save_next_arg=3;;
                "--packages" ) save_next_arg=4;;
                "--features" ) save_next_arg=5;;
                "-r" | "--release" ) CARGO_ARGS="${CARGO_ARGS} --release";;
                "--reduced-linker" ) REDUCED_LINKER=1;;
                "--manifest-path" ) MANIFEST_PATH=1;;
                "--no-default-features" ) CARGO_ARGS="${CARGO_ARGS} --no-default-features";;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

if [ "${MANIFEST_PATH}" = '1' ]; then
    PACKAGE_ARG="--manifest-path"
else
    PACKAGE_ARG="-p"
fi

PACKAGES_FORMATTED=
for p in "${PACKAGES[@]}"
do
    PACKAGES_FORMATTED="${PACKAGES_FORMATTED} ${PACKAGE_ARG} $p"
done

if [ "${REDUCED_LINKER}" = '1' ]; then
    # ld crashes in the VSTS CI's Linux amd64 job while trying to link aziot-edged
    # with a generic exit code 1 and no indicative error message. It seems to
    # work fine if we reduce the number of objects given to the linker,
    # by disabling parallel codegen and incremental compile.
    #
    # We don't want to disable these for everyone else, so only do it in this script
    # that the CI uses.
    sed -i '/\[profile.dev\]/a codegen-units = 1\nincremental = false' edgelet/Cargo.toml

    >> "${PROJECT_ROOT}/Cargo.toml" cat <<-EOF
[profile.test]
codegen-units = 1
incremental = false
EOF
fi

cd "${PROJECT_ROOT}"
BUILD_COMMAND="$CARGO build ${PACKAGES_FORMATTED} ${CARGO_ARGS} --target \"$TARGET\""

echo $BUILD_COMMAND
eval $BUILD_COMMAND
