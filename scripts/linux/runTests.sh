#!/bin/bash

# This script runs all the .NET test projects (*test*.csproj) in the repo using
# binaries built previously (see buildBranch.sh).

TEST_FILTER="$1"
BUILD_CONFIG="$2"

if [ -z "$BUILD_CONFIG" ]
then
  BUILD_CONFIG="CheckInBuild"
fi

run_tests()
{
  local project="$1"
  dotnet test $project \
    --configuration $BUILD_CONFIG \
    --filter "$TEST_FILTER" \
    --logger "trx;LogFileName=$(basename "$testProject").trx" \
    --no-build
}

echo "Running tests with filter: '$TEST_FILTER' and configuration: '$BUILD_CONFIG'"

while read testProject; do
  testPath="$(dirname $testProject)"
  echo "Found test project: $testPath"
  run_tests $testPath
done < <(find . -iname '*.Test.csproj')
