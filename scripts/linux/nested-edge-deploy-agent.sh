#!/usr/bin/env bash
# This script is intended to be used for nested edge tests. It deploys level 4 and level 5.
# Level 3 is deployed by specialized script for connectivity/long haul and quick start.

function create_certificates() {
    echo "Installing test root certificate bundle."

    echo "Generating edge device certificate"
    device_name=$(ip route get 8.8.8.8 | sed -n '/src/{s/.*src *\([^ ]*\).*/\1/p;q}')
    eval device_name=${device_name}
    echo "  Hostname IP: ${device_name}"

    /certs/certGen.sh create_edge_device_certificate ${device_name}
}
#@TODO this might not be compatible for CENTOS
function setup_iotedge() {
    sudo touch /etc/aziot/config.toml

    device_ca_cert_path="file:///certs/certs/iot-edge-device-${device_name}-full-chain.cert.pem"
    trusted_ca_certs_path="file:///certs/certs/azure-iot-test-only.root.ca.cert.pem"
    device_key_cert_path="file:///certs/private/iot-edge-device-${device_name}.key.pem"

    echo "hostname = \"$device_name\"" | sudo tee -a /etc/aziot/config.toml
    if [ ! -z $PARENT_NAME ]; then
         echo "parent_hostname = \"$PARENT_NAME\"" | sudo tee -a /etc/aziot/config.toml
    fi
    echo "trust_bundle_cert = \"$trusted_ca_certs_path\"" | sudo tee -a  /etc/aziot/config.toml
    echo "" | sudo tee -a  /etc/aziot/config.toml

    echo "[provisioning]" | sudo tee -a  /etc/aziot/config.toml
    echo "source = \"manual\"" | sudo tee -a /etc/aziot/config.toml
    echo "connection_string = \"${CONNECTION_STRING}\"" | sudo tee -a /etc/aziot/config.toml
    echo "" | sudo tee -a  /etc/aziot/config.toml

    echo "[edge_ca]" | sudo tee -a  /etc/aziot/config.toml
    echo "cert = \"$device_ca_cert_path\"" | sudo tee -a  /etc/aziot/config.toml
    echo "pk = \"$device_key_cert_path\"" | sudo tee -a  /etc/aziot/config.toml
    echo "" | sudo tee -a /etc/aziot/config.toml

    echo "[agent]" | sudo tee -a  /etc/aziot/config.toml
    echo "name = \"edgeAgent\"" | sudo tee -a  /etc/aziot/config.toml
    echo "type = \"docker\"" | sudo tee -a /etc/aziot/config.toml

    echo "[agent.config]" | sudo tee -a /etc/aziot/config.toml
    if [ ! -z $PARENT_NAME ]; then
        echo "image = \"\$upstream:443/microsoft/azureiotedge-agent:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label\"" | sudo tee -a /etc/aziot/config.toml
    else
        echo "image = \"${CONTAINER_REGISTRY}:443/microsoft/azureiotedge-agent:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label\"" | sudo tee -a /etc/aziot/config.toml
    fi    
    echo "createOptions = { }" | sudo tee -a /etc/aziot/config.toml
    echo "" | sudo tee -a  /etc/aziot/config.toml
    
    if [ -z $PARENT_NAME ]; then
        echo "[agent.config.auth]" | sudo tee -a /etc/aziot/config.toml
        echo "serveraddress = \"${CONTAINER_REGISTRY}\"" | sudo tee -a /etc/aziot/config.toml
        echo "username = \"${CONTAINER_REGISTRY_USERNAME}\"" | sudo tee -a /etc/aziot/config.toml
        echo "password = \"${CONTAINER_REGISTRY_PASSWORD}\"" | sudo tee -a /etc/aziot/config.toml
        echo "" | sudo tee -a  /etc/aziot/config.toml
    fi

    if [ ! -z $PROXY_ADDRESS ]; then
        echo "Configuring the bootstrapping edgeAgent to use http proxy"
        echo "[agent.env]" | sudo tee -a /etc/aziot/config.toml
        echo "https_proxy = \"${PROXY_ADDRESS}\"" | sudo tee -a /etc/aziot/config.toml        
        echo "" | sudo tee -a /etc/aziot/config.toml

        echo "Adding proxy configuration to docker"
        sudo mkdir -p /etc/systemd/system/docker.service.d/
        { echo "[Service]";
        echo "Environment=HTTPS_PROXY=${PROXY_ADDRESS}";
        } | sudo tee /etc/systemd/system/docker.service.d/http-proxy.conf
        sudo systemctl daemon-reload
        sudo systemctl restart docker

        echo "Adding proxy configuration to IoT Edge daemon"
        sudo mkdir -p /etc/systemd/system/aziot-identityd.service.d/
        { echo "[Service]";
        echo "Environment=HTTPS_PROXY=${PROXY_ADDRESS}";
        } | sudo tee /etc/systemd/system/aziot-identityd.service.d/proxy.conf
        sudo systemctl daemon-reload           

        echo "Adding proxy configuration to IoT Edge daemon"
        sudo mkdir -p /etc/systemd/system/aziot-edged.service.d/
        { echo "[Service]";
        echo "Environment=HTTPS_PROXY=${PROXY_ADDRESS}";
        } | sudo tee /etc/systemd/system/aziot-edged.service.d/proxy.conf
        sudo systemctl daemon-reload
    fi

    sudo cat /etc/aziot/config.toml

    echo "Start IoT edge"
    sudo iotedge config apply
}

function prepare_test_from_artifacts() {
    print_highlighted_message 'Prepare test from artifacts'

    echo 'Remove working folder'
    rm -rf "$working_folder"
    mkdir -p "$working_folder"

    echo "Copy deployment file from $connectivity_deployment_artifact_file"
    cp "$connectivity_deployment_artifact_file" "$deployment_working_file"

    sed -i -e "s@<Architecture>@$image_architecture_label@g" "$deployment_working_file"
    sed -i -e "s/<Build.BuildNumber>/$ARTIFACT_IMAGE_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s/<EdgeRuntime.BuildNumber>/$EDGE_RUNTIME_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s@<Container_Registry>@$CONTAINER_REGISTRY@g" "$deployment_working_file"
    sed -i -e "s@<CR.Username>@$CONTAINER_REGISTRY_USERNAME@g" "$deployment_working_file"
    sed -i -e "s@<CR.Password>@$CONTAINER_REGISTRY_PASSWORD@g" "$deployment_working_file"
    sed -i -e "s@<IoTHubConnectionString>@$IOT_HUB_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<proxyAddress>@$PROXY_ADDRESS@g" "$deployment_working_file"
    sed -i -e "s@<TrcUrl>@${L3_IP_ADDRESS}:5001/api/testoperationresult@g" "$deployment_working_file"

    # TODO: Remove
    echo "-------- ${L3_IP_ADDRESS}:5001/api/testoperationresult"

    if [[ ! -z "$CUSTOM_EDGE_AGENT_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-agent:.*\"@\"image\": \"$CUSTOM_EDGE_AGENT_IMAGE\"@g" "$deployment_working_file"
    fi

    if [[ ! -z "$CUSTOM_EDGE_HUB_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-hub:.*\"@\"image\": \"$CUSTOM_EDGE_HUB_IMAGE\"@g" "$deployment_working_file"
    fi

    sudo cat ${deployment_working_file}

    #deploy the config in azure portal
    az iot edge set-modules --device-id ${DEVICE_ID} --hub-name ${IOT_HUB_NAME} --content ${deployment_working_file} --output none
}

function process_args() {
    print_highlighted_message 'Process arguments'
    saveNextArg=0
    for arg in "$@"
    do
        if [ $saveNextArg -eq 1 ]; then
            E2E_TEST_DIR="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 2 ]; then
            RELEASE_LABEL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 3 ]; then
            ARTIFACT_IMAGE_BUILD_NUMBER="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 4 ]; then
            CONTAINER_REGISTRY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 5 ]; then
            CONTAINER_REGISTRY_USERNAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 6 ]; then
            CONTAINER_REGISTRY_PASSWORD="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 7 ]; then
            IOT_HUB_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 8 ]; then
            STORAGE_ACCOUNT_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 9 ]; then
            DEPLOYMENT_FILE_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 10 ]; then
            EDGE_RUNTIME_BUILD_NUMBER="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 11 ]; then
            CUSTOM_EDGE_AGENT_IMAGE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 12 ]; then
            CUSTOM_EDGE_HUB_IMAGE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 13 ]; then
            SUBSCRIPTION="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 14 ]; then
            LEVEL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 15 ]; then
            PARENT_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 16 ]; then
            CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 17 ]; then
            DEVICE_ID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 18 ]; then
            IOT_HUB_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 19 ]; then
            PROXY_ADDRESS="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 20 ]; then
            CHANGE_DEPLOY_CONFIG_ONLY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 21 ]; then
            L3_IP_ADDRESS="$arg"
            saveNextArg=0
        else
            case "$arg" in
                '-h' | '--help' ) usage;;
                '-testDir' ) saveNextArg=1;;
                '-releaseLabel' ) saveNextArg=2;;
                '-artifactImageBuildNumber' ) saveNextArg=3;;
                '-containerRegistry' ) saveNextArg=4;;
                '-containerRegistryUsername' ) saveNextArg=5;;
                '-containerRegistryPassword' ) saveNextArg=6;;
                '-iotHubConnectionString' ) saveNextArg=7;;
                '-storageAccountConnectionString' ) saveNextArg=8;;
                '-deploymentFileName' ) saveNextArg=9;;
                '-edgeRuntimeBuildNumber' ) saveNextArg=10;;
                '-customEdgeAgentImage' ) saveNextArg=11;;
                '-customEdgeHubImage' ) saveNextArg=12;;
                '-subscription' ) saveNextArg=13;;
                '-level' ) saveNextArg=14;;
                '-parentName' ) saveNextArg=15;;
                '-connectionString' ) saveNextArg=16;;
                '-deviceId' ) saveNextArg=17;;
                '-iotHubName' ) saveNextArg=18;;
                '-proxyAddress' ) saveNextArg=19;;
                '-changeDeployConfigOnly' ) saveNextArg=20;;
                '-l3IpAddress' ) saveNextArg=21;;
                '-waitForTestComplete' ) WAIT_FOR_TEST_COMPLETE=1;;
                '-cleanAll' ) CLEAN_ALL=1;;

                * )
                    echo "Unsupported argument: $saveNextArg $arg"
                    usage
                    ;;
            esac
        fi
    done

    # Required parameters
    [[ -z "$DEVICE_ID" ]] && { print_error 'DEVICE_ID is required.'; exit 1; }
    [[ -z "$SUBSCRIPTION" ]] && { print_error 'SUBSCRIPTION is required.'; exit 1; }
    [[ -z "$LEVEL" ]] && { print_error 'Level is required.'; exit 1; }
    [[ -z "$ARTIFACT_IMAGE_BUILD_NUMBER" ]] && { print_error 'Artifact image build number is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_USERNAME" ]] && { print_error 'Container registry username is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_PASSWORD" ]] && { print_error 'Container registry password is required'; exit 1; }
    [[ -z "$DEPLOYMENT_FILE_NAME" ]] && { print_error 'Deployment file name is required'; exit 1; }
    [[ -z "$IOT_HUB_CONNECTION_STRING" ]] && { print_error 'IoT hub connection string is required'; exit 1; }
    [[ -z "$STORAGE_ACCOUNT_CONNECTION_STRING" ]] && { print_error 'Storage account connection string is required'; exit 1; }

    echo 'Required parameters are provided'
}

function set_output_params() {
    echo "##vso[task.setvariable variable=deviceName;isOutput=true]${device_name}"
}

set -e

# Import test-related functions
. $(dirname "$0")/NestTestHelper.sh

#necessary to avoid tput error
export TERM=linux

process_args "$@"

get_image_architecture_label

working_folder="$E2E_TEST_DIR/working"

#@TODO remove hardcoding
#connectivity_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
connectivity_deployment_artifact_file="e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
deployment_working_file="$working_folder/deployment.json"

prepare_test_from_artifacts


if [ "$CHANGE_DEPLOY_CONFIG_ONLY" != "true" ]; then
    create_iotedge_service_config
    create_certificates
    setup_iotedge
    set_output_params
fi

#clean up
#az iot hub device-identity delete -n ${iotHubName} -d ${iotEdgeDevicesName}
