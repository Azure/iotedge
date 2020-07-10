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
BUILD_TARGET=
RELEASE=
PACKAGES=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    echo " -t, --target        Target architecture"
    echo " -r, --release       Release build? (flag, default: false)"
    echo " --build-target      The entity you want to build"
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
        if [ $save_next_arg -eq 1 ]; then
            TARGET="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            RELEASE="true"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            CARGO="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            BUILD_TARGET="$arg"
            if [ "$BUILD_TARGET" = "watchdog" ]; then
                PROJECT_ROOT=$PROJECT_ROOT/edge-hub/watchdog
                PACKAGES=(watchdog)
            elif [ "$BUILD_TARGET" = "mqtt" ]; then
                PROJECT_ROOT=$PROJECT_ROOT/mqtt
                PACKAGES=(mqttd)
            elif [ "$BUILD_TARGET" = "edgelet" ]; then
                PROJECT_ROOT=$PROJECT_ROOT/edgelet
                PACKAGES=(iotedge iotedged iotedge-diagnostics iotedge-proxy)
            else
                echo "Invalid option for build-target"
                exit 1;
            fi
            BUILD_TARGET_SET=true
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target" ) save_next_arg=1;;
                "-r" | "--release" ) save_next_arg=2;;
                "-c" | "--cargo" ) save_next_arg=3;;
                "--build-target" ) save_next_arg=4;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

[[ -z "$BUILD_TARGET" ]] && { print_error 'Option build-target is required.'; exit 1; }

PACKAGES_FORMATTED=
for p in "${PACKAGES[@]}"
do
    PACKAGES_FORMATTED="${PACKAGES_FORMATTED} -p ${p}"
done

if [ $BUILD_TARGET -eq "edgelet" ]; then
    # ld crashes in the VSTS CI's Linux amd64 job while trying to link iotedged
    # with a generic exit code 1 and no indicative error message. It seems to
    # work fine if we reduce the number of objects given to the linker,
    # by disabling parallel codegen and incremental compile.
    #
    # We don't want to disable these for everyone else, so only do it in this script
    # that the CI uses.
    >> "$PROJECT_ROOT/Cargo.toml" cat <<-EOF

[profile.dev]
codegen-units = 1
incremental = false

[profile.test]
codegen-units = 1
incremental = false
EOF
fi

if [[ -z ${RELEASE} ]]; then
    cd "$PROJECT_ROOT"
    $CARGO build ${PACKAGES_WITH_MANIFEST} --target "$TARGET"
    $CARGO build ${PACKAGES_WITH_MANIFEST} --no-default-features --target "$TARGET"
else
    cd "$PROJECT_ROOT" && $CARGO build ${PACKAGES} --release
fi

