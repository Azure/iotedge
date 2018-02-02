#!/bin/bash

# This script runs all the IoT Edge Ctl unit and integration tests.
# This script expects that python and pip are installed.

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

# set up global variables
ROOTFOLDER=$DIR/../..
IOTEDGECTL_DIR=$ROOTFOLDER/edge-bootstrap/python
RESULT=0

echo "Running iotedgectl tests..."

test_cmd="${IOTEDGECTL_DIR}/scripts/run_docker_image_tests.sh"

echo "Executing iotedgectl tests command ${test_cmd}"
${test_cmd}
if [[ $? -gt 0 ]]; then
    RESULT=1
    echo "Failed iotedgectl test: ${test_cmd}"
fi
echo "iotedgectl tests result: $RESULT"

exit $RESULT
