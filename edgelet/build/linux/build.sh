#!/bin/bash

###############################################################################
# This script builds the project
###############################################################################

set -e

###############################################################################
# These are the packages this script will build.
###############################################################################
packages=(iotedge iotedged iotedge-diagnostics iotedge-proxy)

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/edgelet
SCRIPT_NAME=$(basename "$0")
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"
RELEASE=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h, --help          Print this help and exit."
    echo " -r, --release       Release build? (flag, default: false)"
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
            RELEASE="true"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--release" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

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

PACKAGES=
for p in "${packages[@]}"
do
    PACKAGES="${PACKAGES} -p ${p}"
done

if [[ -z ${RELEASE} ]]; then
    cd "$PROJECT_ROOT" && $CARGO build ${PACKAGES}
else
    cd "$PROJECT_ROOT" && $CARGO build ${PACKAGES} --release
fi
