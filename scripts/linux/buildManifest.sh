#!/bin/bash

###############################################################################
# This script builds a multi-architecture manifest image using the
# manifest tool in the bin directory.
# This script expects that the individual images have been built, tagged, and
# pushed to the registry.
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
BINDIR=$DIR/../../bin

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -r, --registry       Docker registry required to build, tag and run the module"
    echo " -u, --username       Docker Registry Username"
    echo " -p, --password       Docker Username's password"
    echo " -v, --image-version  Docker Image Version."
    echo " -t, --template       Yaml file template for manifest definition."
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DOCKER_USERNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_PASSWORD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            YAML_TEMPLATE="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "-t" | "--template" ) save_next_arg=5;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${DOCKER_REGISTRY} ]]; then
        echo "Registry Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_USERNAME} ]]; then
        echo "Docker Username Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_PASSWORD} ]]; then
        echo "Docker Password Parameter Invalid"
        print_help_and_exit
    fi

    if [[ -z ${DOCKER_IMAGEVERSION} ]]; then
        echo "Docker image version not found."
        print_help_and_exit
    fi

    if [[ -z ${YAML_TEMPLATE} ]]; then
        echo "Template file not found."
        print_help_and_exit
    fi

}

###############################################################################
# Main Script Execution
###############################################################################
process_args $@

#echo Logging in to Docker registry
docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
if [ $? -ne 0 ]; then
    echo "Docker Login Failed!"
    exit 1
fi

# Create temp file to store modified yaml file
manifest=$(mktemp /tmp/manifest.yaml.XXXXXX)
[ $? -eq 0 ] || exit $?

sed "s/__REGISTRY__/${DOCKER_REGISTRY}/g; s/__VERSION__/${DOCKER_IMAGEVERSION}/g;" $YAML_TEMPLATE > $manifest
[ $? -eq 0 ] || exit $?

echo "Build image with following manifest:"
cat $manifest

echo "Done Building And Pushing Docker Images"
$BINDIR/manifest-tool --debug push from-spec $manifest
[ $? -eq 0 ] || exit $?

# Remove the temp file
rm $manifest
[ $? -eq 0 ] || exit $?
