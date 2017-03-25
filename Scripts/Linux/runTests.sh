#!/bin/bash

SUFFIX='*test*.csproj'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=~/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY/target
BASEDIR=$(dirname $0)

echo Building all solutions in repo
find $ROOTFOLDER -type f -iname $SUFFIX | while read line; do
    echo Running tests for project - $line
    $DOTNET_ROOT_PATH/dotnet test -o $OUTPUT_FOLDER --no-build $line
done

exit 0
