#! /bin/bash
###############################################################################
# This script builds the azure-iot-edge-runtime-ctl test images
###############################################################################

set -e

SCRIPT_DIR=$(dirname $0)
SCRIPT_DIR=$(readlink -f ${SCRIPT_DIR})
EXE_DIR="${SCRIPT_DIR}/.."
EXE_DIR=$(readlink -f ${EXE_DIR})

echo "Test base directory: ${EXE_DIR}"

echo "#############################################################################################"
echo "Building python 2.7.14 Linux image..."
echo "#############################################################################################"
docker build -t iotedgectl_py2 --file $EXE_DIR/docker/python2/linux/Dockerfile $EXE_DIR

echo "#############################################################################################"
echo "Building python 3.x Linux image..."
echo "#############################################################################################"
docker build -t iotedgectl_py3 --file $EXE_DIR/docker/python3/linux/Dockerfile $EXE_DIR

echo "#############################################################################################"
echo "Executing 2.x Tests..."
echo "#############################################################################################"
docker run --rm iotedgectl_py2

echo "#############################################################################################"
echo "Executing 3.x Tests..."
echo "#############################################################################################"
docker run --rm iotedgectl_py3

echo "#############################################################################################"
echo "Cleanup test images"
echo "#############################################################################################"
docker rmi iotedgectl_py2
docker rmi iotedgectl_py3
