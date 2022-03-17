#! /bin/bash

#Script to check for udpates required to aziot-compatibility.sh script
#Assumes Moby-Engine/Docker is installed
#Requires AZ Login and appropriate subscription to be selected

TEMP_DEPLOYMENT_FILE="deployment.json"
BENCHMARK_OUTPUT_DIR="memory-usage-results"

set -e

function install_iotedge_local() {
    directory="$1"
    sudo apt-get update
    identityservicebinary="$(ls "$directory" | grep "aziot-identity-service")"
    if [[ -z $identityservicebinary ]]; then
        echo "No Identity Service Binary Found"
        exit 1
    fi
    iotedgebinary="$(ls "$directory" | grep "aziot-edge")"
    if [[ -z $iotedgebinary ]]; then
        echo "No IoT Edge Binary Found"
        exit 1
    fi
    echo "Binary is $identityservicebinary $iotedgebinary"
    curr_dir=$(pwd)
    cd "$directory"
    sudo apt-get install -y "./$identityservicebinary"
    sudo apt-get install -y "./$iotedgebinary"
    cd "$curr_dir"
}

begin_benchmarking() {
    mkdir -p "$BENCHMARK_OUTPUT_DIR"
    echo "Starting Script for Analyzing Container Memory with Path $BENCHMARK_OUTPUT_DIR and Script $USAGE_SCRIPT_PATH"
    sudo chmod +x "$USAGE_SCRIPT_PATH"
    "$USAGE_SCRIPT_PATH" -t "$1" -p "$BENCHMARK_OUTPUT_DIR" >&"$BENCHMARK_OUTPUT_DIR/analyze-memory-logs.out" &
}

calculate_usage() {
    usage_file="$(ls $BENCHMARK_OUTPUT_DIR | grep "usage")"
    echo "Usage File is $usage_file"
    "$USAGE_SCRIPT_PATH" -a "$BENCHMARK_OUTPUT_DIR/$usage_file" >&"$BENCHMARK_OUTPUT_DIR/analyze-memory-iotedge.out"
}

compare_usage() {

    item=$1
    type=$2
    buffer=$3

    item_var=$(uname -m)_iotedge_$item\_$type
    echo "Comparing : $item_var"
    stored_item="$(grep "$item_var" <"$COMPATIBILITY_TOOL_PATH" | sed -r "s/^$item_var=//g")"

    current_item="$(grep "$item_var" <"$BENCHMARK_OUTPUT_DIR/analyze-memory-iotedge.out" | sed -r "s/^$item_var=//g")"

    echo "Stored $item $type=$stored_item"
    echo "Stored $item Buffer $type=$buffer"
    echo "Current $item $type=$current_item"

    exceeds=$(echo "$stored_item" "$buffer" "$current_item" | awk '{if ($3 > ($1 + $2)) print 1; else print 0}')

    if [[ $exceeds -eq 1 ]]; then
        echo "$item $type : Value $current_item Exceeds Stored Value in Platform Compatibility Tool by more than $buffer MB, Please Update the tool"
        exit 1
    fi

    lower=$(echo "$stored_item" "$buffer" "$current_item" | awk '{if (($1 - $2) > $3) print 1; else print 0}')

    if [[ $lower -eq 1 ]]; then
        echo "$item $type : Value $current_item is Lower than Stored Value in Platform Compatibility Tool by $buffer, Please Update the tool"
        exit 1
    fi
}

function provision_edge_device() {
    # Provision w/ connection string
    DEVICE_ID=benchmark-device-$(echo $RANDOM | md5sum | head -c 10)
    az iot hub device-identity create --device-id "$DEVICE_ID" --edge-enabled --hub-name "$IOTHUB_NAME"
    connection_string=$(az iot hub device-identity connection-string show --device-id "$DEVICE_ID" --hub-name "$IOTHUB_NAME" -o tsv)
    sudo iotedge config mp --connection-string "$connection_string"
    sudo iotedge config apply
}

function create_edge_deployment() {
    sudo apt-get install -y uuid
    DEPLOYMENT_ID=iotedge-benchmarking-$(uuidgen)
    cp "$DEPLOYMENT_FILE_NAME" $TEMP_DEPLOYMENT_FILE
    sed -i -e "s@<CR.Address>@$REGISTRY_ADDRESS@g" "$TEMP_DEPLOYMENT_FILE"
    sed -i -e "s@<CR.Username>@$REGISTRY_USERNAME@g" "$TEMP_DEPLOYMENT_FILE"
    sed -i -e "s@<CR.Password>@$REGISTRY_PASSWORD@g" "$TEMP_DEPLOYMENT_FILE"
    sed -i -e "s@<edgeAgentImage>@$EDGEAGENT_IMAGE@g" "$TEMP_DEPLOYMENT_FILE"
    sed -i -e "s@<edgeHubImage>@$EDGEHUB_IMAGE@g" "$TEMP_DEPLOYMENT_FILE"
    sed -i -e "s@<tempSensorImage>@$TEMPSENSOR_IMAGE@g" "$TEMP_DEPLOYMENT_FILE"
    x=$(cat $TEMP_DEPLOYMENT_FILE | jq)
    echo "$x"
    az iot edge deployment create --content "$TEMP_DEPLOYMENT_FILE" --deployment-id "$DEPLOYMENT_ID" --hub-name "$IOTHUB_NAME" -t "deviceId='$DEVICE_ID'"
}

function delete_edge_deployment() {
    echo "Removing IoT Edge Device Deployment"
    az iot edge deployment delete --deployment-id $DEPLOYMENT_ID --hub-name $IOTHUB_NAME
}

function delete_iot_hub_device_identity() {
    echo "Removing IoT Edge Device Identity"
    az iot hub device-identity delete --device-id $DEVICE_ID --hub-name $IOTHUB_NAME
}

function delete_iot_edge() {
    echo "Removing IoT Edge Installation"
    sudo apt-get remove -y aziot-identity-service --purge
}

function cleanup_files() {
    rm -rf $BENCHMARK_OUTPUT_DIR || true
    rm -rf $TEMP_DEPLOYMENT_FILE || true
}

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$(basename "$0") [options]"
    echo ""
    echo "options"
    echo " -f, --deployment-filename            The deployment template filename."
    echo "-n , --hub-name                       The name of the IoT hub with which to register the device"
    echo "-u, --usage-script-path               The path to the script that calculates iot edge memory/cpu numbers"
    echo "-c, --compatibility-tool-path         The path to the platform compatibility tool script"
    echo "-b, --binaries-path                   The path to the IoT Edge Binaries"
    echo "-t, --time-to-run                     Time to run in seconds"
    echo "--edge-agent-image                    The EdgeAgent Image"
    echo "--edge-hub-image                      The EdgeHub Image"
    echo "--temp-sensor-image                   The Temp Sensor Image"
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
function process_args() {
    save_next_arg=0
    for arg in "$@"; do
        if [ ${save_next_arg} -eq 1 ]; then
            DEPLOYMENT_FILE_NAME=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 2 ]; then
            IOTHUB_NAME=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 3 ]; then
            USAGE_SCRIPT_PATH=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 4 ]; then
            COMPATIBILITY_TOOL_PATH=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 5 ]; then
            TIME_TO_RUN=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 6 ]; then
            BINARIES_PATH=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 7 ]; then
            EDGEAGENT_IMAGE=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 8 ]; then
            EDGEHUB_IMAGE=$arg
            save_next_arg=0
        elif [ ${save_next_arg} -eq 9 ]; then
            TEMPSENSOR_IMAGE=$arg
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-f" | "--deployment-filename") save_next_arg=1 ;;
            "-n" | "--hub-name") save_next_arg=2 ;;
            "-u" | "--usage-script-path") save_next_arg=3 ;;
            "-c" | "--compatibility-tool-path") save_next_arg=4 ;;
            "-t" | "--time-to-run") save_next_arg=5 ;;
            "-b" | "--binaries-path") save_next_arg=6 ;;
            "--edge-agent-image") save_next_arg=7 ;;
            "--edge-hub-image") save_next_arg=8 ;;
            "--temp-sensor-image") save_next_arg=9 ;;
            *) usage ;;
            esac
        fi
    done
}

function print_error() {
    local message=$1
    local red='\033[0;31m'
    local color_reset='\033[0m'
    echo -e "${red}$message${color_reset}"
}

process_args "$@"

[[ -z "$IOTHUB_NAME" ]] && {
    print_error 'IoT Hub name is required.'
    exit 1
}

[[ -z "$DEPLOYMENT_FILE_NAME" ]] && {
    print_error 'Deployment file name is required.'
    exit 1
}

[[ -z "$USAGE_SCRIPT_PATH" ]] && {
    print_error 'Memory Benchmarking file name is required.'
    exit 1
}

[[ -z "$COMPATIBILITY_TOOL_PATH" ]] && {
    print_error 'Platform Compatibility Tool Path is required'
    exit 1
}

[[ -z "$BINARIES_PATH" ]] && {
    print_error 'Binaries Path is required.'
    exit 1
}

[[ -z "$EDGEAGENT_IMAGE" ]] && {
    print_error 'Edge Agent Image is required.'
    exit 1
}

[[ -z "$EDGEHUB_IMAGE" ]] && {
    print_error 'Edge Hub Image is required.'
    exit 1
}

[[ -z "$TEMPSENSOR_IMAGE" ]] && {
    print_error 'Temp Sensor Image is required.'
    exit 1
}

[[ -z "$TIME_TO_RUN" ]] && {
    print_error 'Time to run is required.'
    exit 1
}

cleanup_files
#Start the Memory Usage Script so that we can capture startup memory usage
begin_benchmarking "$TIME_TO_RUN"
install_iotedge_local "$BINARIES_PATH"
provision_edge_device
echo "Provision Complete, Waiting 60 seconds"
sleep 60
create_edge_deployment
echo "Deployment Complete, Sleeping for $TIME_TO_RUN seconds"
sleep "$TIME_TO_RUN"
calculate_usage
delete_edge_deployment
delete_iot_hub_device_identity
delete_iot_edge

#Compare Usage and Exit if Current Usage exceeds recorded usage
size_buffer="$(grep "iotedge_size_buffer=" <"$COMPATIBILITY_TOOL_PATH" | sed -r "s/^iotedge_size_buffer=//g")"
memory_buffer="$(grep "iotedge_memory_buffer=" <"$COMPATIBILITY_TOOL_PATH" | sed -r "s/^iotedge_memory_buffer=//g")"
echo "Size buffer is $size_buffer"
compare_usage container size "$size_buffer"
compare_usage container memory "$memory_buffer"
compare_usage binaries size "$size_buffer"
compare_usage binaries avg_memory "$memory_buffer"
