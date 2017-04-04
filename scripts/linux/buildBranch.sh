#!/bin/bash

# This Script builds all .Net Core Solutions in the repo by recursing through
# the repo and finding any *.sln files
# This script expects that .Net Core is installed at $AGENT_WORKFOLDER\dotnet
# by a previous step.

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

SUFFIX='Microsoft.Azure.*.sln'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY/target

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
find $ROOTFOLDER -type f -name $SUFFIX | while read line; do
    echo Building Solution - $line
    $DOTNET_ROOT_PATH/dotnet clean $line
    $DOTNET_ROOT_PATH/dotnet restore $line
    $DOTNET_ROOT_PATH/dotnet build $line -o $OUTPUT_FOLDER
done

exit 0
