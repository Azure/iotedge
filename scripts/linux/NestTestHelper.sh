#!/bin/bash

function clean_up() {
    print_highlighted_message 'Clean up'

    stop_iotedge_service || true

    echo 'Remove IoT Edge and config file'
    apt-get purge libiothsm-std --yes || true
    rm -rf /var/lib/iotedge/
    rm -rf /var/run/iotedge/
    rm -rf /etc/iotedge/config.yaml
    rm -rf /etc/systemd/system/aziot-*.service.d/

    if [ "$CLEAN_ALL" = '1' ]; then
        echo 'Prune docker system'
        docker system prune -af --volumes || true

        echo 'Restart docker'
        systemctl restart docker # needed due to https://github.com/moby/moby/issues/23302
    else
        echo 'Remove docker containers'
        docker rm -f $(docker ps -aq) || true
    fi
}

function create_iotedge_service_config {
    print_highlighted_message 'Create IoT Edge service config'
    mkdir /etc/systemd/system/iotedge.service.d/ || true
    bash -c "echo '[Service]
Environment=IOTEDGE_LOG=edgelet=debug' > /etc/systemd/system/iotedge.service.d/override.conf"
}

function get_connectivity_deployment_artifact_file() {
    local testDir=$1

    local path
    path="$testDir/artifacts/core-linux/e2e_deployment_files/connectivity_deployment.template.json"

    echo "$path"
}

function get_image_architecture_label() {
    local arch
    arch="$(uname -m)"

    case "$arch" in
        'x86_64' ) image_architecture_label='amd64';;
        'armv7l' ) image_architecture_label='arm32v7';;
        'aarch64' ) image_architecture_label='arm64v8';;
        *) print_error "Unsupported OS architecture: $arch"; exit 1;;
    esac
}

function get_iotedged_artifact_folder() {
    local testDir=$1
    
    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$testDir/artifacts/iotedged-ubuntu18.04-amd64"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$testDir/artifacts/iotedged-ubuntu18.04-aarch64"
    else
        path="$testDir/artifacts/iotedged-debian9-arm32v7"
    fi

    echo "$path"
}

function get_iotedge_quickstart_artifact_file() {
    local testDir=$1

    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$testDir/artifacts/core-linux/IotEdgeQuickstart.linux-x64.tar.gz"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$testDir/artifacts/core-linux/IotEdgeQuickstart.linux-arm64.tar.gz"
    else
        path="$testDir/artifacts/core-linux/IotEdgeQuickstart.linux-arm.tar.gz"
    fi

    echo "$path"
}

function get_leafdevice_artifact_file() {
    local testDir=$1

    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$testDir/artifacts/core-linux/LeafDevice.linux-x64.tar.gz"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$testDir/artifacts/core-linux/LeafDevice.linux-arm64.tar.gz"
    else
        path="$testDir/artifacts/core-linux/LeafDevice.linux-arm.tar.gz"
    fi

    echo "$path"
}

function get_hash() {
    local length=$1
    local hash=$(cat /dev/urandom | tr -dc 'a-zA-Z0-9' | head -c $length)
    
    echo "$hash"
}

function is_cancel_build_requested() {
    local accessToken=$1
    local buildId=$2
    
    if [[ ( -z "$accessToken" ) || ( -z "$buildId" ) ]]; then
        echo 0
    fi
    
    local output1=$(curl -s -u :$accessToken --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId?api-version=5.1" | grep -oe '"status":"cancel')
    local output2=$(curl -s -u :$accessToken --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId/Timeline?api-version=5.1" | grep -oe '"result":"canceled"')
    
    if [[ -z "$output1" && -z "$output2" ]]; then
        echo 0
    else
        echo 1
    fi
}

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

function stop_iotedge_service() {
    echo 'Stop IoT Edge services'
    systemctl stop iotedge.socket iotedge.mgmt.socket || true
    systemctl kill iotedge || true
    systemctl stop iotedge || true
}
