#!/bin/bash

# This script builds a source distribution for the python bootstrap script
# and places it in $BUILD_BINARIESDIRECTORY.

# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
SCRIPT_NAME=$(basename $0)

# Check if environment variables are set
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
BUILD_BINARIESDIRECTORY=${BUILD_BINARIESDIRECTORY:-$BUILD_REPOSITORY_LOCALPATH/target}

EGG_INFO=
OPTIONS=
OUTPUT_DIR=
ROOT_FOLDER=$BUILD_REPOSITORY_LOCALPATH
PUBLISH_FOLDER=$BUILD_BINARIESDIRECTORY/publish

if [ ! -d "${ROOT_FOLDER}" ]; then
    echo "Folder $ROOT_FOLDER does not exist" 1>&2
    exit 1
fi

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -i, --egg-info       Parameters to pass to egg-info (e.g. \"--tag-build=dev --tag-date\")"
    echo " -o, --outputdir      Output directory for the package"
    echo " -s, --sdist-options  Additional options for sdist"
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
    for arg in "$@"
    do
        if [ $save_next_arg -eq 1 ]; then
            EGG_INFO="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            OUTPUT_DIR="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            OPTIONS="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-i" | "--egg-info" ) save_next_arg=1;;
                "-o" | "--outputdir" ) save_next_arg=2;;
                "-s" | "--sdist-options" ) save_next_arg=3;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${OUTPUT_DIR} ]]; then
        OUTPUT_DIR=$BUILD_BINARIESDIRECTORY
    fi
}

process_args "$@"

if [ ! -d "${OUTPUT_DIR}" ]; then
    mkdir $OUTPUT_DIR
fi

echo "Creating source distribution for python bootstrap script"
if [[ -z ${EGG_INFO} ]]; then
    /usr/bin/python $ROOT_FOLDER/edge-bootstrap/python/setup.py sdist --dist-dir $OUTPUT_DIR $OPTIONS
else
    /usr/bin/python $ROOT_FOLDER/edge-bootstrap/python/setup.py egg_info $EGG_INFO sdist --dist-dir $OUTPUT_DIR $OPTIONS
fi
