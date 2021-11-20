###############################################################################
# This script installs and configures vsdbg in a specified container module.
# After running this script, Visual Studio or VSCode can be attached locally
# or remotely to debug processes running in the container.
###############################################################################

###############################################################################
# Define Environment Variables
###############################################################################
CONTAINER_NAME="edgeAgent"
USERNAME="edgeagentuser"
SCRIPT_NAME=$(basename "$0")

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -c, --container-name             The name of the container in which to setup the debugger"
    echo " -h, --help                       Print this help and exit."
    exit 1
}

function print_help_and_exit() {
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args() {
    save_next_arg=0
    for arg in "$@"; do
        if [ ${save_next_arg} -eq 1 ]; then
            CONTAINER_NAME=$arg
            save_next_arg=0                 
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-c" | "--container-name") save_next_arg=1 ;;
            *) usage ;;
            esac
        fi
    done
}

process_args "$@"

sudo docker exec -it $CONTAINER_NAME apk add curl icu procps shadow sudo
sudo docker exec -it $CONTAINER_NAME curl -sSL https://aka.ms/getvsdbgsh  -o /root/GetVsDbg.sh
sudo docker exec -it $CONTAINER_NAME sh -C /root/GetVsDbg.sh -v latest -l /root/vsdbg
sudo docker exec -it $CONTAINER_NAME mv /root/vsdbg/vsdbg /root/vsdbg/vsdbg-bin
sudo docker exec -it $CONTAINER_NAME sh -c "echo \#\!/bin/sh > /root/vsdbg/vsdbg"
sudo docker exec -it $CONTAINER_NAME sh -c "echo /root/vsdbg/vsdbg-bin \$\@ >> /root/vsdbg/vsdbg"
sudo docker exec -it $CONTAINER_NAME chmod 770 /root 
sudo docker exec -it $CONTAINER_NAME chmod 770 /root/vsdbg
sudo docker exec -it $CONTAINER_NAME chmod 770 /root/vsdbg/vsdbg
sudo docker exec -it $CONTAINER_NAME chmod 770 /root/vsdbg/vsdbg-bin
sudo docker exec -it $CONTAINER_NAME usermod $USERNAME -G root

