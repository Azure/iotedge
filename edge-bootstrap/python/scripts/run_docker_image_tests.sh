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

TAG_BASE=iotedgectl_py
BASE_VERSION_LIST=(2.7.14-jessie 3.4.8-jessie 3.5.5-jessie 3.6.4-jessie 3.6.5-jessie 3.7.0b2-stretch)

# Build images
for version in ${BASE_VERSION_LIST[*]}
do
    echo ""
    echo "#########################################################################################"
    echo "Building python ${version} Linux image..."
    echo "#########################################################################################"
    docker build --build-arg BASE_VERSION=${version} -t $TAG_BASE${version} --file $EXE_DIR/docker/linux/Dockerfile $EXE_DIR
done

for version in ${BASE_VERSION_LIST[*]}
do
    echo ""
    echo "#########################################################################################"
    echo "Executing python ${version} Tests.."
    echo "#########################################################################################"
    docker run --rm $TAG_BASE${version}
done

for version in ${BASE_VERSION_LIST[*]}
do
    echo ""
    echo "#########################################################################################"
    echo "Removing python ${version} Linux image..."
    echo "#########################################################################################"
    docker rmi $TAG_BASE${version}
done
