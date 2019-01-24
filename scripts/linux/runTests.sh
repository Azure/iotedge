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
BUILD_CONFIG="$2"

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

testFilterValue="${TEST_FILTER#--filter }"

if [ -z "$BUILD_CONFIG" ]
then
  BUILD_CONFIG="CheckInBuild"
fi

echo "Running tests in all test projects with filter: $testFilterValue and $BUILD_CONFIG configuration"

RES=0

# Find all test project dlls
testProjectDlls = ""
while read proj; do
  fileParentDirectory="$(dirname -- "$proj")"
  fileName="$(basename -- "$proj")"
  fileBaseName="${fileName%.*}"
  
  currentTestProjectDll="$fileParentDirectory/bin/$BUILD_CONFIG/netcoreapp2.1/$fileBaseName.dll"
  echo "Try to run test project:$currentTestProjectDll"
  testProjectDlls="$testProjectDlls $currentTestProjectDll"
done < <(find $ROOTFOLDER -type f -iname $SUFFIX)

testCommand="dotnet vstest /Logger:trx;LogFileName=result.trx /TestAdapterPath:\"$BUILD_REPOSITORY_LOCALPATH\" /Parallel /InIsolation"
if [ ! -z "$testFilterValue" ]
then
  testCommand+=" /TestCaseFilter:"$testFilterValue""
fi
testCommand+="$testProjectDlls"

echo "Run test command:$testCommand"
$testCommand

if [ $? -gt 0 ]
then
  RES=1
fi

echo "Edge runtime tests result RES = $RES"

exit $RES
