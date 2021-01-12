#!/bin/bash

function print_error() {
    local message=$1
    local red='\033[0;31m'
    local color_reset='\033[0m'
    echo -e "${red}$message${color_reset}"
}

function print_highlighted_message() {
    local message=$1
    local cyan='\033[0;36m'
    local color_reset='\033[0m'
    echo -e "${cyan}$message${color_reset}"
}

function create_iotedge_service_config {
    print_highlighted_message 'Create IoT Edge service config'
    mkdir /etc/systemd/system/aziot-edged.service.d/ || true
    bash -c "echo '[Service]
Environment=IOTEDGE_LOG=edgelet=debug' > /etc/systemd/system/aziot-edged.service.d/override.conf"
}

function get_image_architecture_label() {
    local arch
    arch="$(uname -m)"

    case "$arch" in
        'x86_64' ) echo 'amd64';;
        'armv7l' ) echo 'arm32v7';;
        'aarch64' ) echo 'arm64v8';;
        *) print_error "Unsupported OS architecture: $arch"; exit 1;;
    esac
}

function get_artifact_file() {
    local testDir=$1
    local fileType=$2

    local filter
    case "$fileType" in
        'aziot_edge' ) filter='aziot-edge_*.deb';;
        'aziot_is' ) filter='aziot-identity-service_*.deb';;
        'quickstart' ) filter='IotEdgeQuickstart.linux*.tar.gz';;
        'deployment' ) filter="core-linux/e2e_deployment_files/ $3";;
        *) print_error "Unknown file type: $fileType"; exit 1;;
    esac

    local path
    # shellcheck disable=SC2086
    path=$(ls "$testDir/artifacts/"$filter)

    if [ "$(echo "$path" | wc -w)" -ne 1 ]; then
        print_error "Multiple files for $fileType found."
        exit 1
    fi

    echo "$path"
}

function get_hash() {
    local length=$1
    local hash
    hash=$(tr -dc 'a-zA-Z0-9' < /dev/urandom | head -c "$length")

    echo "$hash"
}

function is_cancel_build_requested() {
    local accessToken=$1
    local buildId=$2

    if [[ ( -z "$accessToken" ) || ( -z "$buildId" ) ]]; then
        echo 0
    fi

    local output1
    local output2
    output1=$(curl -s -u :"$accessToken" --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId?api-version=5.1" | grep -oe '"status":"cancel')
    output2=$(curl -s -u :"$accessToken" --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId/Timeline?api-version=5.1" | grep -oe '"result":"canceled"')

    if [[ -z "$output1" && -z "$output2" ]]; then
        echo 0
    else
        echo 1
    fi
}

function stop_iotedge_service() {
    echo 'Stop IoT Edge services'
    systemctl stop aziot-keyd aziot-certd aziot-identityd aziot-edged || true
}

function clean_up() {
    print_highlighted_message 'Clean up'

    stop_iotedge_service || true

    echo 'Remove IoT Edge and config files'
    rm -rf /var/lib/aziot/
    rm -rf /etc/aziot/

    if [ "$CLEAN_ALL" = '1' ]; then
        echo 'Prune docker system'
        docker system prune -af --volumes || true

        echo 'Restart docker'
        systemctl restart docker # needed due to https://github.com/moby/moby/issues/23302
    else
        echo 'Remove docker containers'
        docker rm -f "$(docker ps -aq)" || true
    fi
}
