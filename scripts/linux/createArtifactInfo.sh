#!/bin/bash

# This script is used to create artifact info file.

usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " --output-folder        output folder for artifact info file"
    echo " --build-number         build number of artifact"
    exit 1;
}

process_args() {
    local save_next_arg=0
    for arg in "$@"; 
    do
        if [ $save_next_arg -eq 1 ]; then
            OUTPUT_FOLDER="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            BUILD_NUMBER="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "--output-folder" ) save_next_arg=1;;
                "--build-number" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done

    if [ -f "$OUTPUT_FOLDER" ]; then
        echo "Please provide output folder"
        exit 1
    fi

    if [ "$BUILD_NUMBER" = "" ]; then
        echo "Please provide build number"
        exit 1
    fi
}

function create_artifact_Info() {
    artifactInfoFilePath="$OUTPUT_FOLDER/artifactInfo.txt"
    echo "BuildNumber=$BUILD_NUMBER" | tee -a "$artifactInfoFilePath"
    echo "Created artifact info file in $artifactInfoFilePath"
}

process_args "$@"

create_artifact_Info