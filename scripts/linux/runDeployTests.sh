#!/bin/bash

###############################################################################
# This script builds and runs the tests found under 'deploy/test'.
# PRECONDITIONS:
# - Docker, Python, and pip must be installed
# - If iotedgectl is already installed, it must not be configured (run
#   'iotedgectl uninstall' to make sure)
# - The Key Vault certificate must be installed if any of the following
#   parameters are NOT specified (because the tests will use Key Vault to
#   derive default values):
#   --connection-string
#   --eventhub-endpoint
#   --username and --password, if --registry is specified
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
SCRIPT_FOLDER=$(cd "$(dirname "$0")" && pwd)
ROOT_FOLDER=${BUILD_REPOSITORY_LOCALPATH:-$SCRIPT_FOLDER/../..}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    [ -z "$1" ] || echo "Invalid argument: $1";

    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -c, --connection-string   IoT Hub connection string (hub-scoped, e.g. iothubowner)"
    echo " -e, --eventhub-endpoint   Event Hub-compatible endpoint for IoT Hub, including EntityPath"
    echo " -r, --registry            Hostname of Docker registry used to pull images"
    echo " -u, --username            Docker Registry username"
    echo " -p, --password            Docker Username's password"
    echo " -t, --tag                 Tag to append when pulling images"
    echo " -a, --iotedgectl-archive  Path to python 'azure-iot-edge-runtime-ctl' archive"
    echo "                           (path can include wildcards, but must resolve to one file)"
    echo " -f, --root-folder         Root path to test source (<root>/deploy/test/**/*.csproj)"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Proces arguments supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            CONNECTION_STRING="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            EVENTHUB_ENDPOINT="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_USERNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            DOCKER_PASSWORD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            IMAGE_TAG="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            declare -a archives=( $arg )
            IOTEDGECTL_ARCHIVE="${archives[0]}"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            ROOT_FOLDER="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-c" | "--connection-string" ) save_next_arg=1;;
                "-e" | "--eventhub-endpoint" ) save_next_arg=2;;
                "-r" | "--registry" ) save_next_arg=3;;
                "-u" | "--username" ) save_next_arg=4;;
                "-p" | "--password" ) save_next_arg=5;;
                "-t" | "--tag" ) save_next_arg=6;;
                "-a" | "--iotedgectl-archive" ) save_next_arg=7;;
                "-f" | "--root-folder" ) save_next_arg=8;;
                * ) usage "$arg";;
            esac
        fi
    done
}

process_args "$@"

if [ ! -d "$ROOT_FOLDER" ]; then
  echo "Path $ROOT_FOLDER does not exist" 1>&2
  exit 1
fi

command -v dotnet >/dev/null 2>&1 || {
  echo "'dotnet' isn't installed, or not in PATH" 1>&2
  exit 1
}

[ -z "$CONNECTION_STRING" ] || export iothubConnectionString="$CONNECTION_STRING"
[ -z "$EVENTHUB_ENDPOINT" ] || export eventhubCompatibleEndpointWithEntityPath="$EVENTHUB_ENDPOINT"
[ -z "$DOCKER_REGISTRY" ] || export registryAddress="$DOCKER_REGISTRY"
[ -z "$DOCKER_USERNAME" ] || export registryUser="$DOCKER_USERNAME"
[ -z "$DOCKER_PASSWORD" ] || export registryPassword="$DOCKER_PASSWORD"
[ -z "$IMAGE_TAG" ] || export imageTag="$IMAGE_TAG"
[ -z "$IOTEDGECTL_ARCHIVE" ] || export iotedgectlArchivePath="$IOTEDGECTL_ARCHIVE"

dotnet test \
    -c Release \
    --filter "Category=Deploy" \
    -p:ParallelizeTestCollections=false \
    $ROOT_FOLDER/deploy/test/**/*.csproj
