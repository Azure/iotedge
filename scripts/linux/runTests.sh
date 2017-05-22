#!/bin/bash

# This script runs all the .Net Core test projects (*test*.csproj) in the
# repo by recursing from the repo root.
# This script expects that .Net Core is installed at
# $AGENT_WORKFOLDER/dotnet and output binaries are at $BUILD_BINARIESDIRECTORY

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)

# Check if Environment variables are set.
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
AGENT_WORKFOLDER=${AGENT_WORKFOLDER:-/usr/share}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}

SUFFIX='Microsoft.Azure*test.csproj'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY
ENVIRONMENT=${TESTENVIRONMENT:="linux"}

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
RES=0
while read line; do
    echo Running tests for project - $line
  TESTENVIRONMENT=$ENVIRONMENT && $DOTNET_ROOT_PATH/dotnet test --filter Category!=Bvt --logger "trx;LogFileName=result.trx" -o $OUTPUT_FOLDER --no-build $line
  if [ $? -gt 0 ]
  then
    RES=1
    echo Error running test $line, RES = $RES
  fi
done < <(find $ROOTFOLDER -type f -iname $SUFFIX)

echo RES = $RES

exit $RES
