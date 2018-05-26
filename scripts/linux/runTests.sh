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

# Process script arguments
TEST_FILTER="$1"

SUFFIX='Microsoft.Azure*test.csproj'
ROOTFOLDER=$BUILD_REPOSITORY_LOCALPATH
IOTEDGECTL_DIR=$ROOTFOLDER/edge-bootstrap/python
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY
ENVIRONMENT=${TESTENVIRONMENT:="linux"}

if [ ! -d "$ROOTFOLDER" ]; then
  echo "Folder $ROOTFOLDER does not exist" 1>&2
  exit 1
fi

if [ ! -f "$DOTNET_ROOT_PATH/dotnet" ]; then
  echo "Path $DOTNET_ROOT_PATH/dotnet does not exist" 1>&2
  exit 1
fi

if [ ! -d "$BUILD_BINARIESDIRECTORY" ]; then
  echo "Path $BUILD_BINARIESDIRECTORY does not exist" 1>&2
  exit 1
fi

echo "Running tests in all test projects with filter: ${TEST_FILTER#--filter }"

RES=0
while read proj; do
  echo "Running tests for project - $proj"
  TESTENVIRONMENT=$ENVIRONMENT $DOTNET_ROOT_PATH/dotnet test \
    $TEST_FILTER \
    -p:ParallelizeTestCollections=false \
    --logger "trx;LogFileName=result.trx" \
    -o "$OUTPUT_FOLDER" \
    $proj
  if [ $? -gt 0 ]
  then
    RES=1
    echo "Error running test $proj, RES = $RES"
  fi
done < <(find $ROOTFOLDER -type f -iname $SUFFIX)

echo "Edge runtime tests result RES = $RES"

exit $RES
