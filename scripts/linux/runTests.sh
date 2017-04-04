#!/bin/bash

# This script runs all the .Net Core test projects (*test*.csproj) in the
# repo by recursing from the repo root. 
# This script expects that .Net Core is installed at 
# $AGENT_WORKFOLDER/dotnet and output binaries are at $BUILD_BINARIESDIRECTORY


checkEnvVar() {
	varname=$1
	if [ -z "${!varname}" ]; then
		echo Error: Environment variable $varname is not set 1>&2
		exit 1
	fi
}

# Check if Environment variables are set.
checkEnvVar BUILD_REPOSITORY_LOCALPATH
checkEnvVar AGENT_WORKFOLDER
checkEnvVar BUILD_BINARIESDIRECTORY

SUFFIX='Microsoft.Azure*test*.csproj'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY/target

if [ ! -d "$ROOTFOLDER" ]; then
	echo Folder $ROOTFOLDER does not exist 1>&2
	exit 1
fi

if [ ! -f "$DOTNET_ROOT_PATH/dotnet" ]; then
	echo Path $DOTNET_ROOT_PATH/dotnet does not exist 1>&2
	exit 1
fi

if [ ! -d "$BUILD_BINARIESDIRECTORY" ]; then
	echo Path $BUILD_BINARIESDIRECTORY does not exist 1>&2
	exit 1
fi

echo Running tests in all Test Projects in repo
find $ROOTFOLDER -type f -iname $SUFFIX | while read line; do
    echo Running tests for project - $line
	$DOTNET_ROOT_PATH/dotnet test --logger "trx;LogFileName=result.trx" -o $OUTPUT_FOLDER --no-build $line
done

exit 0
