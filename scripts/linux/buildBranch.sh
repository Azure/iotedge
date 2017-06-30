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
ANTLR_PATTERN='*.g4'
ROOT_FOLDER=$BUILD_REPOSITORY_LOCALPATH
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish
SRC_DOCKER_DIR=$ROOT_FOLDER/docker

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

echo "Cleaning and restoring all solutions in repo"

while read soln; do
    echo "Cleaning and Restoring packages for solution - $soln"
    $DOTNET_ROOT_PATH/dotnet clean --output $BUILD_BINARIESDIRECTORY $soln
    $DOTNET_ROOT_PATH/dotnet restore $soln
done < <(find $ROOT_FOLDER -type f -name $SLN_PATTERN)

echo "Generating Antlr code files"

while read g4file; do
    echo "Generating .cs files for - $g4file"
    OUTPUT_DIR=$(dirname "$g4file")/generated
    mkdir -p $OUTPUT_DIR
    java -jar ~/.nuget/packages/antlr4.codegenerator/4.6.1-beta002/tools/antlr4-csharp-4.6.1-SNAPSHOT-complete.jar $g4file -package Microsoft.Azure.Devices.Routing.Core -Dlanguage=CSharp_v4_5 -visitor -listener -o $OUTPUT_DIR
done < <(find $ROOT_FOLDER -type f -name $ANTLR_PATTERN)

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

echo "Copying $SRC_DOCKER_DIR to $PUBLISH_FOLDER/docker"
rm -fr $PUBLISH_FOLDER/docker
cp -r $SRC_DOCKER_DIR $PUBLISH_FOLDER

exit $RES
