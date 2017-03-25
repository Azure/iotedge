#!/bin/bash

SUFFIX='*.sln'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=~/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY/target
BASEDIR=$(dirname $0)

echo Building all solutions in repo
find $ROOTFOLDER -type f -name $SUFFIX | while read line; do
    echo Building Solution - $line
    $DOTNET_ROOT_PATH/dotnet clean $line
    $DOTNET_ROOT_PATH/dotnet restore $line
    $DOTNET_ROOT_PATH/dotnet build $line -o $OUTPUT_FOLDER
done

exit 0
