#!/bin/bash

###############################################################################
# This script runs eclipse compliance tests against the broker
###############################################################################
echo "test"
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

###############################################################################
# Build and download tests
###############################################################################
$DIR/build.sh -r 1
$DIR/certgen.sh
git clone https://github.com/eclipse/paho.mqtt.testing.git

###############################################################################
# Start broker and run tests
###############################################################################
set +e

$PROJECT_ROOT/target/release/mqttd &
broker=$!

python3 paho.mqtt.testing/interoperability/client_test.py
result=$?

kill -KILL $broker
exit $result