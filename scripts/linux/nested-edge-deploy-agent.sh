#!/usr/bin/env bash
# This script is intended to be used for nested edge tests. It deploy level 4 and level 5.
# Level 3 is deveployed by specialized script for connectivity/long haul and quick start.

function create_certificates() {
    echo "Installing test root certificate bundle."    

    echo "Generating edge device certificate"
    device_name=$(az vm show -d  -g "iotedge-deploy" -n $(hostname) --query fqdns)
    eval device_name=${device_name}
    echo "  Hostname FQDN: ${device_name}" 

    /certs/certGen.sh create_edge_device_certificate ${device_name}
}
#@TODO this might not be compatible for CENTOS
function install_and_setup_iotedge() {
    echo "Install and setup iotedge"

    echo "  Install artifacts"
    declare -a pkg_list=( $iotedged_artifact_folder/*.deb )
    iotedge_package="${pkg_list[*]}" 
    sudo dpkg -i --force-confnew ${iotedge_package}
 
    echo "  Updating IoT Edge configuration file to use the newly installed certificcates"
    device_ca_cert_path="/certs/certs/iot-edge-device-${device_name}-full-chain.cert.pem"
    device_ca_pk_path="/certs/private/iot-edge-device-${device_name}.key.pem"
    trusted_ca_certs_path="/certs/certs/azure-iot-test-only.root.ca.cert.pem"
    sudo sed -i "165s|.*|certificates:|" /etc/iotedge/config.yaml
    sudo sed -i "166s|.*|  device_ca_cert: \"$device_ca_cert_path\"|" /etc/iotedge/config.yaml
    sudo sed -i "167s|.*|  device_ca_pk: \"$device_ca_pk_path\"|" /etc/iotedge/config.yaml
    sudo sed -i "168s|.*|  trusted_ca_certs: \"$trusted_ca_certs_path\"|" /etc/iotedge/config.yaml

    echo "Updating the device connection string"
    sudo sed -i "s#\(device_connection_string: \).*#\1\"${CONNECTION_STRING}\"#g" /etc/iotedge/config.yaml

    echo "Updating the device and parent hostname"
    sudo sed -i "224s/.*/hostname: \"$device_name\"/" /etc/iotedge/config.yaml

    if [ ! -z $PARENT_NAME ]; then
        echo "Updating the parent hostname"
        sudo sed -i "237s/.*/parent_hostname: \"$PARENT_NAME\"/" /etc/iotedge/config.yaml
    fi

    echo "Updating edge Agent"
    sudo sed -i "207s|.*|    image: \"${CUSTOM_EDGE_AGENT_IMAGE}\"|" /etc/iotedge/config.yaml

    if [ -z $PARENT_NAME ]; then
        sudo sed -i "208s|.*|    auth:|" /etc/iotedge/config.yaml
        sed -i "209i\      serveraddress: \"${CONTAINER_REGISTRY}\"" /etc/iotedge/config.yaml
        sed -i "210i\      username: \"${CONTAINER_REGISTRY_USERNAME}\"" /etc/iotedge/config.yaml
        sed -i "211i\      password: \"${CONTAINER_REGISTRY_PASSWORD}\"" /etc/iotedge/config.yaml
    fi

    sudo cat /etc/iotedge/config.yaml

    #deploy the config in azure portal
    az iot edge set-modules --device-id ${DEVICE_ID} --hub-name ${IOT_HUB_NAME} --content ${deployment_working_file} --output none

    echo "Start IoT edge"
    sudo systemctl restart iotedge
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
  
    if [[ ! -z "$CUSTOM_EDGE_AGENT_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-agent:.*\"@\"image\": \"$CUSTOM_EDGE_AGENT_IMAGE\"@g" "$deployment_working_file"
    fi
    
    if [[ ! -z "$CUSTOM_EDGE_HUB_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-hub:.*\"@\"image\": \"$CUSTOM_EDGE_HUB_IMAGE\"@g" "$deployment_working_file"
    fi
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
    [[ -z "$CONNECTION_STRING" ]] && { print_error 'CONNECTION_STRING is required.'; exit 1; }
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

function test_setup() {
    local funcRet=0
    
    clean_up && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
    prepare_test_from_artifacts && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
    create_iotedge_service_config && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
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

if [ -z $CUSTOM_EDGE_AGENT_IMAGE ]; then
    if [ ! -z $PARENT_NAME ]; then
        CUSTOM_EDGE_AGENT_IMAGE="${PARENT_NAME}:443/microsoft/azureiotedge-agent:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label"
    else
        CUSTOM_EDGE_AGENT_IMAGE="${CONTAINER_REGISTRY}/microsoft/azureiotedge-agent:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label"
    fi
fi
if [ -z $CUSTOM_EDGE_HUB_IMAGE ]; then
    if [ ! -z $PARENT_NAME ]; then
        CUSTOM_EDGE_HUB_IMAGE="${PARENT_NAME}:443/microsoft/azureiotedge-hub:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label"
    else
        CUSTOM_EDGE_HUB_IMAGE="${CONTAINER_REGISTRY}/microsoft/azureiotedge-hub:$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label"
    fi
fi
working_folder="$E2E_TEST_DIR/working"
iotedged_artifact_folder="$(get_iotedged_artifact_folder $E2E_TEST_DIR)"

connectivity_deployment_artifact_file="e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
deployment_working_file="$working_folder/deployment.json"

test_setup
create_certificates
install_and_setup_iotedge
set_output_params
