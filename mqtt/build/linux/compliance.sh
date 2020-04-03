#!/bin/bash

###############################################################################
# This script runs eclipse compliance tests against the broker
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../../..}
PROJECT_ROOT=${BUILD_REPOSITORY_LOCALPATH}/mqtt
SCRIPT_NAME=$(basename "$0")
CARGO="${CARGO_HOME:-"$HOME/.cargo"}/bin/cargo"
RELEASE="true"

$DIR/build.sh -r
$DIR/certgen.sh

$PROJECT_ROOT/target/release/mqttd
