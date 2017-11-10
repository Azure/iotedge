#!/bin/bash

###############################################################################
# This script moves a docker image from one registry to another registry.
# It assumes that the caller is logged into both registries
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -f, --from           Fully qualified source image"
    echo " -t, --to             Fully qualified destination image"
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
            FROM_IMAGE="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            TO_IMAGE="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-f" | "--from" ) save_next_arg=1;;
                "-t" | "--to" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${FROM_IMAGE} ]]; then
        echo "From image invalid"
        print_help_and_exit
    fi

    if [[ -z ${TO_IMAGE} ]]; then
        echo "To image invalid"
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

echo "Pulling $FROM_IMAGE"
docker pull $FROM_IMAGE
[ $? -eq 0 ] || exit $?

echo "Tagging image: $TO_IMAGE"
docker tag $FROM_IMAGE $TO_IMAGE
[ $? -eq 0 ] || exit $?

echo "Pushing image: $TO_IMAGE"
docker push $TO_IMAGE
[ $? -eq 0 ] || exit $?

