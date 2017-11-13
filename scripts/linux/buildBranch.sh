#!/bin/bash

# This script finds and builds all .NET Core solutions in the repo, and
# publishes .NET Core apps to target/publish/.

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

# Check if environment variables are set
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}

SLN_PATTERN='Microsoft.Azure.*.sln'
CSPROJ_PATTERN='*.csproj'
TEST_CSPROJ_PATTERN='*Test.csproj'
FUNCTION_BINDING_CSPROJ_PATTERN='*Binding.csproj'
ROOT_FOLDER=$BUILD_REPOSITORY_LOCALPATH
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
RELEASE_TESTS_FOLDER=$BUILD_BINARIESDIRECTORY/release-tests
SRC_DOCKER_DIR=$ROOT_FOLDER/docker
SRC_SCRIPTS_DIR=$ROOT_FOLDER/scripts
SRC_BIN_DIR=$ROOT_FOLDER/bin
VERSIONINFO_FILE_PATH=$BUILD_REPOSITORY_LOCALPATH/versionInfo.json

# Process script arguments
PUBLISH_TESTS=${1:-""}

if [ ! -d "${ROOT_FOLDER}" ]; then
    echo "Folder $ROOT_FOLDER does not exist" 1>&2
    exit 1
fi

if [ -f "${AGENT_WORKFOLDER}/dotnet/dotnet" ]; then # VSTS Linux
    DOTNET_ROOT_PATH="$AGENT_WORKFOLDER/dotnet"
elif [ -f "/usr/share/dotnet/dotnet" ]; then        # default Linux
    DOTNET_ROOT_PATH="/usr/share/dotnet"
elif [ -f "/usr/local/share/dotnet/dotnet" ]; then   # default macOS
    DOTNET_ROOT_PATH="/usr/local/share/dotnet"
else
    echo "dotnet not found" 1>&2
    exit 1
fi

if [ ! -d "${BUILD_BINARIESDIRECTORY}" ]; then
    mkdir $BUILD_BINARIESDIRECTORY
fi

if [ -z "${CONFIGURATION}" ]; then
    CONFIGURATION="Debug"
fi

if [ -z "${BUILD_SOURCEVERSION}" ]; then
    BUILD_SOURCEVERSION=""
fi

if [ -z "${BUILD_BUILDID}" ]; then
    BUILD_BUILDID=""
fi

if [ -f "${VERSIONINFO_FILE_PATH}" ]; then
    echo "Updating versionInfo.json with build ID and commit ID"
    sed -i "s/BUILDNUMBER/$BUILD_BUILDID/" $VERSIONINFO_FILE_PATH
    sed -i "s/COMMITID/$BUILD_SOURCEVERSION/" $VERSIONINFO_FILE_PATH
else
    echo "VersionInfo.json file not found."
fi

echo "Cleaning and restoring all solutions in repo"

while read soln; do
    echo "Cleaning and Restoring packages for solution - $soln"
    $DOTNET_ROOT_PATH/dotnet clean --output $BUILD_BINARIESDIRECTORY $soln
    $DOTNET_ROOT_PATH/dotnet restore $soln
done < <(find $ROOT_FOLDER -type f -name $SLN_PATTERN)

echo "Building all solutions in repo"
RES=0
while read soln; do
    echo "Building Solution - $soln"
    $DOTNET_ROOT_PATH/dotnet build -c $CONFIGURATION --output $BUILD_BINARIESDIRECTORY $soln
    if [ $? -gt 0 ]; then
        RES=1
    fi
done < <(find $ROOT_FOLDER -type f -name $SLN_PATTERN)

if [ $RES -ne 0 ]; then
    exit $RES
fi

echo "Publishing all required solutions in repo"
rm -fr $PUBLISH_FOLDER

while read proj; do
    echo "Publishing Solution - $proj"
    PROJ_FILE=$(basename "$proj")
    PROJ_NAME="${PROJ_FILE%.*}"
    $DOTNET_ROOT_PATH/dotnet publish -f netcoreapp2.0 -c $CONFIGURATION -o $PUBLISH_FOLDER/$PROJ_NAME $proj
    if [ $? -gt 0 ]; then
        RES=1
    fi
done < <(find $ROOT_FOLDER -type f -name $CSPROJ_PATTERN -exec grep -l "<OutputType>Exe</OutputType>" {} +)

while read proj; do
    echo "Publishing Solution - $proj"
    PROJ_FILE=$(basename "$proj")
    PROJ_NAME="${PROJ_FILE%.*}"
    $DOTNET_ROOT_PATH/dotnet publish -f netstandard2.0 -c $CONFIGURATION -o $PUBLISH_FOLDER/$PROJ_NAME $proj
    if [ $? -gt 0 ]; then
        RES=1
    fi

done < <(find $ROOT_FOLDER -type f -name $FUNCTION_BINDING_CSPROJ_PATTERN)

echo "Copying $SRC_DOCKER_DIR to $PUBLISH_FOLDER/docker"
rm -fr $PUBLISH_FOLDER/docker
cp -r $SRC_DOCKER_DIR $PUBLISH_FOLDER

echo "Copying $SRC_SCRIPTS_DIR to $PUBLISH_FOLDER/scripts"
rm -fr $PUBLISH_FOLDER/scripts
cp -r $SRC_SCRIPTS_DIR $PUBLISH_FOLDER

echo "Copying $SRC_BIN_DIR to $PUBLISH_FOLDER/bin"
rm -fr $PUBLISH_FOLDER/bin
cp -r $SRC_BIN_DIR $PUBLISH_FOLDER

if [ "$PUBLISH_TESTS" == "--publish-tests" ]; then

    echo "Publishing tests"
    while read proj; do
        echo "Publishing Tests from solution - $proj"
        PROJ_FILE=$(basename "$proj")
        PROJ_NAME="${PROJ_FILE%.*}"
        $DOTNET_ROOT_PATH/dotnet publish -f netcoreapp2.0 -c $CONFIGURATION -o $RELEASE_TESTS_FOLDER/target $proj
        if [ $? -gt 0 ]; then
            RES=1
        fi

        echo "Copying $proj to $RELEASE_TESTS_FOLDER/$PROJ_NAME"
        mkdir -p $RELEASE_TESTS_FOLDER/$PROJ_NAME
        cp $proj "$RELEASE_TESTS_FOLDER/$PROJ_NAME"
    done < <(find $ROOT_FOLDER -type f -name $TEST_CSPROJ_PATTERN)

    echo "Copying $SRC_SCRIPTS_DIR to $RELEASE_TESTS_FOLDER/scripts"
    rm -fr $RELEASE_TESTS_FOLDER/scripts
    cp -r $SRC_SCRIPTS_DIR $RELEASE_TESTS_FOLDER
    cp $ROOT_FOLDER/nuget.config $RELEASE_TESTS_FOLDER
fi

exit $RES
