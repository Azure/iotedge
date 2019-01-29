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

# Find all test project dlls
testProjectRunSerially=( "Microsoft.Azure.Devices.Edge.Agent.Docker.Test.dll" )
testProjectDllsRunSerially=()
testProjectDlls=""

while read testDll; do
  echo "Try to run test project:$testDll"
    
  if (for t in "${testProjectRunSerially[@]}"; do [[ $testDll == */$t ]] && exit 0; done)
  then
    echo "Run Serially for $testDll"
    testProjectDllsRunSerially+=($testDll)
  else
    testProjectDlls="$testProjectDlls $testDll"
  fi  
done < <(find $OUTPUT_FOLDER -type f -iname $SUFFIX)

testCommandPrefix="$DOTNET_ROOT_PATH/dotnet vstest /Logger:trx;LogFileName=result.trx /TestAdapterPath:\"$OUTPUT_FOLDER\" /Parallel /InIsolation"
if [ ! -z "$testFilterValue" ]
then
  testCommandPrefix+=" /TestCaseFilter:"$testFilterValue""
fi

for testDll in ${testProjectDllsRunSerially[@]}
do
  testCommand="$testCommandPrefix $testDll"
  echo "Run test command serially:$testCommand"
  $testCommand
  
  if [ $? -gt 0 ]
  then
    exit 1
  fi
done

testCommand="$testCommandPrefix$testProjectDlls"
echo "Run test command:$testCommand"
$testCommand

if [ $? -gt 0 ]
then
  exit 1
fi

exit 0
