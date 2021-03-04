#!/bin/bash

###############################################################################
# This script moves a Helm chart image from one registry to another registry.
# It assumes that the caller is logged into both registries
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################
SCRIPT_NAME=$(basename $0)
export HELM_EXPERIMENTAL_OCI=1

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

CHARTSAVE=$(mktemp -d)
if [[ ! -d "$CHARTSAVE" ]]; then
    echo "Could not create tempdir $CHARTSAVE for moving chart"
    exit 1
fi

echo "Pulling $FROM_IMAGE"
helm chart pull $FROM_IMAGE
[ $? -eq 0 ] || exit $?

echo "Export chart: $FROM_IMAGE"
helm chart export $FROM_IMAGE -d $CHARTSAVE
[ $? -eq 0 ] || exit $?

# The helm chart export command puts the chart in a subdirectory of the target 
CHARTDIR=$"${CHARTSAVE}/*"
if [[ -d "$CHARTDIR" ]]; then
    echo "Helm export failed. [$CHARTDIR] expected to be a single directory"
    exit 1
fi

echo "Save chart: $TO_IMAGE"
helm chart save $CHARTDIR $TO_IMAGE
[ $? -eq 0 ] || exit $?

echo "Pushing chart: $TO_IMAGE"
helm chart push $TO_IMAGE
[ $? -eq 0 ] || exit $?

rm -r "$CHARTSAVE"