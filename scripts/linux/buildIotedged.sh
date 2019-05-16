#!/bin/bash

# This script copies the iodedged executable files that goes into the azureiotedge-iotedged image,
# for each supported arch (x86_64, arm32v7, arm64v8).
# It then publishes executable files along with their corresponding dockerfiles to the publish directory,
# so that buildImage.sh can build the container image.

set -e

###############################################################################
# Define Environment Variables
###############################################################################
ARCH=$(uname -m)
TOOLCHAIN=
STRIP=
SCRIPT_NAME=$(basename $0)
SOURCE_DIR=
PUBLISH_DIR=
PROJECT=
SRC_DOCKERFILE=
DOCKERFILE=
DOCKER_IMAGENAME=
DEFAULT_DOCKER_NAMESPACE="microsoft"
DOCKER_NAMESPACE=${DEFAULT_DOCKER_NAMESPACE}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:=""}
EDGELET_DIR=
BUILD_CONFIGURATION="release"
BUILD_CONFIG_OPTION=

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [[ "$ARCH" == "x86_64" ]]; then
        ARCH="amd64"
        TOOLCHAIN="x86_64-unknown-linux-musl"
        STRIP="strip"
    elif [[ "$ARCH" == "armv7l" ]]; then
        ARCH="arm32v7"
        TOOLCHAIN="armv7-unknown-linux-gnueabihf"
        STRIP="arm-linux-gnueabihf-strip"
    elif [[ "$ARCH" == "aarch64" ]]; then
        ARCH="arm64v8"
        TOOLCHAIN="aarch64-unknown-linux-musl"
        STRIP="aarch64-linux-gnu-strip"
    else
        echo "Unsupported architecture"
        exit 1
    fi
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -i, --image-name     Image name (e.g. edge-agent)"
    echo " -P, --project        Project to build image for (e.g. iotedged)"
    echo " -t, --target-arch    Target architecture (default: uname -m)"
    echo " -n, --namespace      Docker namespace (default: $DEFAULT_DOCKER_NAMESPACE)"
    echo " -c, --configuration  Build configuration"
    echo "--bin-dir             Directory containing the output binaries. Either use this option or set env variable BUILD_BINARIESDIRECTORY"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [[ ${save_next_arg} -eq 1 ]]; then
            ARCH="$arg"
            check_arch
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 2 ]]; then
            PROJECT="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 3 ]]; then
            DOCKER_IMAGENAME="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 4 ]]; then
            DOCKER_NAMESPACE="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 5 ]]; then
            BUILD_CONFIGURATION="$arg"
            save_next_arg=0
        elif [[ ${save_next_arg} -eq 6 ]]; then
            BUILD_BINARIESDIRECTORY="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-t" | "--target-arch" ) save_next_arg=1;;
                "-P" | "--project" ) save_next_arg=2;;
                "-i" | "--image-name" ) save_next_arg=3;;
                "-n" | "--namespace" ) save_next_arg=4;;
                "-c" | "--configuration" ) save_next_arg=5;;
                "--bin-dir" ) save_next_arg=6;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_IMAGENAME} ]]; then
        echo "Docker image name parameter invalid"
        print_help_and_exit
    fi

    if [[ -z ${BUILD_BINARIESDIRECTORY} ]] || [[ ! -d ${BUILD_BINARIESDIRECTORY} ]]; then
        echo "Bin directory does not exist or is invalid"
        print_help_and_exit
    fi

    PUBLISH_DIR=${BUILD_BINARIESDIRECTORY}/publish

    if [[ ! -d ${PUBLISH_DIR} ]]; then
        echo "Publish directory does not exist or is invalid"
        print_help_and_exit
    fi

    EDGELET_DIR=${BUILD_REPOSITORY_LOCALPATH}/edgelet
    if [[ -z ${EDGELET_DIR} ]] || [[ ! -d ${EDGELET_DIR} ]]; then
        echo "No directory for edgelet found in $BUILD_BINARIESDIRECTORY"
        print_help_and_exit
    fi

    DOCKER_DIR=${EDGELET_DIR}/${PROJECT}/docker
    if [[ -z ${DOCKER_DIR} ]] || [[ ! -d ${DOCKER_DIR} ]]; then
        echo "No docker directory for $PROJECT at $DOCKER_DIR"
        print_help_and_exit
    fi

    DOCKERFILE="$DOCKER_DIR/linux/$ARCH/Dockerfile"
    if [[ ! -f ${DOCKERFILE} ]]; then
        echo "No Dockerfile at $DOCKERFILE"
        print_help_and_exit
    fi

    if ${BUILD_CONFIG_OPTION} == "release"; then
        BUILD_CONFIGURATION='release'
        BUILD_CONFIG_OPTION='--release'
    else
        BUILD_CONFIGURATION='debug'
        BUILD_CONFIG_OPTION=''
    fi
}

###############################################################################
# Build project and publish result
###############################################################################
build_project()
{
    local EXE_DOCKER_DIR=${PUBLISH_DIR}/${DOCKER_IMAGENAME}/docker/linux/${ARCH}
    mkdir -p ${EXE_DOCKER_DIR}

    local EXE_DOCKERFILE=${EXE_DOCKER_DIR}/Dockerfile
    echo "Copy Dockerfile to $EXE_DOCKERFILE"
    cp ${DOCKERFILE} ${EXE_DOCKERFILE}

    echo "Build ${EDGELET_DIR}/$PROJECT for $ARCH"

    cd ${EDGELET_DIR}

    cross build -p $PROJECT ${BUILD_CONFIG_OPTION} --target ${TOOLCHAIN}
    ${STRIP} ${EDGELET_DIR}/target/${TOOLCHAIN}/${BUILD_CONFIGURATION}/${PROJECT}
    cp ${EDGELET_DIR}/edgelet/target/${TOOLCHAIN}/${BUILD_CONFIGURATION}/${PROJECT} ${EXE_DOCKER_DIR}/${PROJECT}
}

###############################################################################
# Main Script Execution
###############################################################################
check_arch
process_args "$@"

build_project


#mkdir -p $PUBLISH_FOLDER/azureiotedge-iotedged/
#cp -R $BUILD_REPOSITORY_LOCALPATH/edgelet/iotedged/docker $PUBLISH_FOLDER/azureiotedge-iotedged/docker
#
#cd "$BUILD_REPOSITORY_LOCALPATH/edgelet"

#cross build -p iotedged $BUILD_CONFIG_OPTION --target x86_64-unknown-linux-musl
#strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedged
#cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/x86_64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedged $PUBLISH_FOLDER/azureiotedge-iotedged/docker/linux/amd64/

#cross build -p iotedged $BUILD_CONFIG_OPTION --target armv7-unknown-linux-musleabihf
#arm-linux-gnueabihf-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BUILD_CONFIGURATION/iotedged
#cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-musleabihf/$BUILD_CONFIGURATION/iotedged $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm32v7/

#cross build -p iotedged $BUILD_CONFIG_OPTION --target armv7-unknown-linux-gnueabihf
#arm-linux-gnueabihf-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-gnueabihf/$BUILD_CONFIGURATION/iotedged
#cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/armv7-unknown-linux-gnueabihf/$BUILD_CONFIGURATION/iotedged $PUBLISH_FOLDER/azureiotedge-diagnostics/docker/linux/arm32v7/

#cross build -p iotedged $BUILD_CONFIG_OPTION --target aarch64-unknown-linux-musl
#aarch64-linux-gnu-strip $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedged
#cp $BUILD_REPOSITORY_LOCALPATH/edgelet/target/aarch64-unknown-linux-musl/$BUILD_CONFIGURATION/iotedged $PUBLISH_FOLDER/azureiotedge/docker/linux/arm64v8/



#-----------------------------------


#V1
#mkdir -p $PUBLISH_FOLDER/azureiotedge-iotedged/
#cp -R $BUILD_REPOSITORY_LOCALPATH/edgelet/build/debian9 $PUBLISH_FOLDER/azureiotedge-iotedged/docker
#
## setup libiothsm build
#cmake -DBUILD_SHARED=ON -Drun_unittests=ON -Duse_emulator=OFF -DCMAKE_BUILD_TYPE=Release -S edgelet/hsm-sys/azure-iot-hsm-c -B edgelet/hsm-sys/azure-iot-hsm-c/build
#
## build libiothsm
#make -C edgelet/hsm-sys/azure-iot-hsm-c/build iothsm
#
## copy libiothsm to staging folder
#cp edgelet/hsm-sys/azure-iot-hsm-c/build/*.so* $PUBLISH_FOLDER/azureiotedge-iotedged/
#
## build iotedged
#make -C edgelet
#
## copy iotedged to staging folder
#cp edgelet/target/release/iotedged $PUBLISH_FOLDER/azureiotedge-iotedged/
#


#cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/amd64/
#cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/arm32v7/
#cp $BUILD_REPOSITORY_LOCALPATH/edge-proxy/src/run.sh $PUBLISH_FOLDER/azureiotedge-proxy/docker/linux/arm64v8/