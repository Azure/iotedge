#!/bin/bash

# This script:
# - builds the IoT Edge runtime components (Agent and Hub)
# - builds sample modules (temperature sensor and filter)
# - builds the tests (and e2e test modules)
# - packages the smoke tests as tarballs for amd64 and arm32v7
# - publishes everything to {source_root}/target/

SCRIPT_NAME=$(basename $0)
DIR=$(cd "$(dirname "$0")" && pwd)

# Paths
AGENT_WORKFOLDER=${AGENT_WORKFOLDER:-/usr/share}
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
ROOT_FOLDER=$BUILD_REPOSITORY_LOCALPATH
SRC_BIN_DIR=$ROOT_FOLDER/bin
SRC_DOCKER_DIR=$ROOT_FOLDER/docker
SRC_SCRIPTS_DIR=$ROOT_FOLDER/scripts
SRC_STRESS_DIR=$ROOT_FOLDER/stress
SRC_E2E_TEMPLATES_DIR=$ROOT_FOLDER/e2e_deployment_files
SRC_E2E_TEST_FILES_DIR=$ROOT_FOLDER/e2e_test_files
SRC_CERT_TOOLS_DIR=$ROOT_FOLDER/tools/CACertificates
FUNCTIONS_SAMPLE_DIR=$ROOT_FOLDER/edge-modules/functions/samples
VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json

DOTNET_RUNTIME=netcoreapp2.1

usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -c, --config         Product binary configuration: Debug [default] or Release"
    echo " --no-rocksdb-bin     Do not copy the RocksDB binaries into the project's output folders"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

process_args()
{
    local save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            CONFIGURATION="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-c" | "--config" ) save_next_arg=1;;
                "--no-rocksdb-bin" ) MSBUILD_OPTIONS="-p:RocksDbAsPackage=false";;
                * ) usage;;
            esac
        fi
    done

    if [ ! -d "$ROOT_FOLDER" ]; then
        echo "Folder $ROOT_FOLDER does not exist" 1>&2
        exit 1
    fi

    if [ -f "$AGENT_WORKFOLDER/dotnet/dotnet" ]; then   # VSTS Linux
        DOTNET_ROOT_PATH="$AGENT_WORKFOLDER/dotnet"
    elif [ -f "/usr/share/dotnet/dotnet" ]; then        # default Linux
        DOTNET_ROOT_PATH="/usr/share/dotnet"
    elif [ -f "/usr/local/share/dotnet/dotnet" ]; then  # default macOS
        DOTNET_ROOT_PATH="/usr/local/share/dotnet"
    else
        echo "dotnet not found" 1>&2
        exit 1
    fi

    if [ ! -d "$BUILD_BINARIESDIRECTORY" ]; then
        mkdir $BUILD_BINARIESDIRECTORY
    fi

    if [ -z "$CONFIGURATION" ]; then
        CONFIGURATION="Debug"
    fi

    if [ -z "$BUILD_SOURCEVERSION" ]; then
        BUILD_SOURCEVERSION=""
    fi

    if [ -z "$BUILD_BUILDID" ]; then
        BUILD_BUILDID=""
    fi
}

update_version_info()
{
    if [ -f "$VERSIONINFO_FILE_PATH" ]; then
        echo "Updating versionInfo.json with build ID and commit ID"
        sed -i "s/BUILDNUMBER/$BUILD_BUILDID/" $VERSIONINFO_FILE_PATH
        sed -i "s/COMMITID/$BUILD_SOURCEVERSION/" $VERSIONINFO_FILE_PATH
    else
        echo "VersionInfo.json file not found."
    fi
}

publish_files()
{
    local src="$1"
    local dst="$2"
    echo "Publishing files from '$src'"
    cp -rv $src $dst
}

publish_project()
{
    local type="$1"
    local name="$2"
    local framework="$3"
    local config="$4"
    local output="$5"
    local option="$6"

    local path=$(find $ROOT_FOLDER -type f -name $name.csproj)
    if [ -z "$path" ]; then
        echo "Could not find project named '$name'"
        RES=1
    fi

    echo "Publishing $type '$name'"
    $DOTNET_ROOT_PATH/dotnet publish -f $framework -c $config $option -o $output $path
    if [ $? -gt 0 ]; then
        RES=1
    fi
}

publish_app()
{
    local name="$1"
    publish_project app \
        "$name" $DOTNET_RUNTIME $CONFIGURATION "$PUBLISH_FOLDER/$name" $MSBUILD_OPTIONS
}

publish_lib()
{
    local name="$1"
    publish_project library "$name" netstandard2.0 $CONFIGURATION "$PUBLISH_FOLDER/$name"
}

publish_quickstart()
{
    local rid="$1"
    echo "Publishing IotEdgeQuickstart for '$rid'"
    $DOTNET_ROOT_PATH/dotnet publish \
        -c $CONFIGURATION \
        -f $DOTNET_RUNTIME \
        -r $rid \
        $ROOT_FOLDER/smoke/IotEdgeQuickstart
    if [ $? -gt 0 ]; then
        RES=1
    fi

    tar \
        -C "$ROOT_FOLDER/smoke/IotEdgeQuickstart/bin/$CONFIGURATION/$DOTNET_RUNTIME/$rid/publish/" \
        -czf "$PUBLISH_FOLDER/IotEdgeQuickstart.$rid.tar.gz" \
        .
}

publish_leafdevice()
{
    local rid="$1"
    echo "Publishing LeafDevice for '$rid'"
    $DOTNET_ROOT_PATH/dotnet publish \
        -c $CONFIGURATION \
        -f $DOTNET_RUNTIME \
        -r $rid \
        $ROOT_FOLDER/smoke/LeafDevice
    if [ $? -gt 0 ]; then
        RES=1
    fi

    tar \
        -C "$ROOT_FOLDER/smoke/LeafDevice/bin/$CONFIGURATION/$DOTNET_RUNTIME/$rid/publish/" \
        -czf "$PUBLISH_FOLDER/LeafDevice.$rid.tar.gz" \
        .
}

build_solution()
{
    echo "Building IoT Edge solution"
    $DOTNET_ROOT_PATH/dotnet build \
        -c $CONFIGURATION \
        -o "$BUILD_BINARIESDIRECTORY" \
        "$ROOT_FOLDER/Microsoft.Azure.Devices.Edge.sln"
    if [ $? -gt 0 ]; then
        RES=1
    fi

    echo "Building IoT Edge Samples solution"
    $DOTNET_ROOT_PATH/dotnet build \
        -c $CONFIGURATION \
        -o "$BUILD_BINARIESDIRECTORY" \
        "$ROOT_FOLDER/samples/dotnet/Microsoft.Azure.Devices.Edge.Samples.sln"
    if [ $? -gt 0 ]; then
        RES=1
    fi
}

process_args "$@"

rm -fr $PUBLISH_FOLDER

update_version_info

build_solution

publish_app "Microsoft.Azure.Devices.Edge.Agent.Service"
publish_app "Microsoft.Azure.Devices.Edge.Hub.Service"
publish_app "SimulatedTemperatureSensor"
publish_app "TemperatureFilter"
publish_app "load-gen"
publish_app "MessagesAnalyzer"
publish_app "DirectMethodSender"
publish_app "DirectMethodReceiver"
publish_app "DirectMethodCloudSender"

publish_lib "Microsoft.Azure.WebJobs.Extensions.EdgeHub"
publish_lib "EdgeHubTriggerCSharp"

publish_files $SRC_DOCKER_DIR $PUBLISH_FOLDER
publish_files $SRC_SCRIPTS_DIR $PUBLISH_FOLDER
publish_files $SRC_BIN_DIR $PUBLISH_FOLDER
publish_files $SRC_STRESS_DIR $PUBLISH_FOLDER
publish_files $SRC_E2E_TEMPLATES_DIR $PUBLISH_FOLDER
publish_files $SRC_E2E_TEST_FILES_DIR $PUBLISH_FOLDER
publish_files $SRC_CERT_TOOLS_DIR $PUBLISH_FOLDER

publish_quickstart linux-arm
publish_quickstart linux-x64
publish_leafdevice linux-arm
publish_leafdevice linux-x64

exit $RES
