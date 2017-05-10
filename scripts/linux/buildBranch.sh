#!/bin/bash

# This Script builds all .Net Core Solutions in the repo by recursing through
# the repo and finding any *.sln files
# This script expects that .Net Core is installed at $AGENT_WORKFOLDER\dotnet
# by a previous step.

# Check if Environment variables are set.
TMP=${BUILD_REPOSITORY_LOCALPATH:?Env variable BUILD_REPOSITORY_LOCALPATH needs to be set and be non-empty}
TMP=${AGENT_WORKFOLDER:?Env variable AGENT_WORKFOLDER needs to be set and be non-empty}
TMP=${BUILD_BINARIESDIRECTORY:?Env variable BUILD_BINARIESDIRECTORY needs to be set and be non-empty}

CSPROJ_SUFFIX='Microsoft.Azure.*.csproj'
SLN_SUFFIX='Microsoft.Azure.*.sln'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
SRC_DOCKER_DIR=$ROOTFOLDER/docker

if [ ! -d "${ROOTFOLDER}" ]; then
    echo Folder $ROOTFOLDER does not exist 1>&2
    exit 1
fi

if [ ! -f "${DOTNET_ROOT_PATH}/dotnet" ]; then
    echo Path $DOTNET_ROOT_PATH/dotnet does not exist 1>&2
    exit 1
fi

if [ ! -d "${BUILD_BINARIESDIRECTORY}" ]; then
    mkdir $BUILD_BINARIESDIRECTORY
fi

echo Building all solutions in repo
RES=0

while read soln; do
    echo Building Solution - $soln
    $DOTNET_ROOT_PATH/dotnet clean --output $BUILD_BINARIESDIRECTORY $soln
    $DOTNET_ROOT_PATH/dotnet restore $soln
    $DOTNET_ROOT_PATH/dotnet build --output $BUILD_BINARIESDIRECTORY $soln
    if [ $? -gt 0 ]; then
        RES=1
    fi
done < <(find $ROOTFOLDER -type f -name $SLN_SUFFIX)

if [ $RES -ne 0 ]; then
    exit $RES
fi

echo Publishing all required solutions in repo
rm -fr $PUBLISH_FOLDER

while read proj; do
    echo Publishing Solution - $proj
    filename=$(basename "$proj")
    proj_publish_dir="${filename%.*}"
    $DOTNET_ROOT_PATH/dotnet publish -f netcoreapp2.0 -o $PUBLISH_FOLDER/$proj_publish_dir $proj
    if [ $? -gt 0 ]; then
        RES=1
    fi
    # This is a workaround for dotnet publish issue when it adds netstandard1.1 to dependencies path
    echo Remove netstandard1.1 from deps.json
    sed -i 's/lib\/netstandard1.1\///g' $PUBLISH_FOLDER/$proj_publish_dir/$proj_publish_dir.deps.json

done < <(find $ROOTFOLDER -type f -name $CSPROJ_SUFFIX -exec grep -l "<OutputType>Exe</OutputType>" {} +)

echo Copying $SRC_DOCKER_DIR to $PUBLISH_FOLDER/docker
rm -fr $PUBLISH_FOLDER/docker
cp -r $SRC_DOCKER_DIR $PUBLISH_FOLDER

exit $RES