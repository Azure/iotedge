#!/bin/bash

# This script runs all the .Net Core test projects (*test*.csproj) in the
# repo by looking up build output from previous build step.
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

SUFFIX='Microsoft.Azure*test.dll'
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet
OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY

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
while read testDll; do
  echo "Try to run test project:$testDll"
  testProjectDlls="$testProjectDlls $testDll"
done < <(find $OUTPUT_FOLDER -type f -iname $SUFFIX)

testCommand="$DOTNET_ROOT_PATH/dotnet vstest /Logger:trx;LogFileName=result.trx /TestAdapterPath:\"$OUTPUT_FOLDER\" /Parallel /InIsolation"
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
