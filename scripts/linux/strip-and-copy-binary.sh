###############################################################################
# Define Environment Variables
###############################################################################
# Get directory of running script
DIR=$(cd "$(dirname "$0")" && pwd)
BUILD_REPOSITORY_LOCALPATH=${BUILD_REPOSITORY_LOCALPATH:-$DIR/../..}
SCRIPT_NAME=$(basename "$0")

cd $BUILD_REPOSITORY_LOCALPATH

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " --os                Desired os for build"
    echo " --arch              Desired arch for build"
    echo " -h, --help          Print this help and exit."
    exit 1;
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
            SRC="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DEST="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--src" ) save_next_arg=1;;
                "--dest" ) save_next_arg=2;;
                "-h" | "--help" ) usage;;
                * ) usage;;
            esac
        fi
    done
}

process_args "$@"

[[ -z "$SRC" ]] && { print_error 'Src is a required parameter'; exit 1; }
[[ -z "$DEST" ]] && { print_error 'Dest is a required paramerter'; exit 1; }

FILENAME=$(basename $SRC)
strip $SRC -o $DEST/$FILENAME
