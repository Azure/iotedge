#!/bin/bash

###############################################################################
# This script creates a new deployment for the device provided as argument
###############################################################################

###############################################################################                                         
# Print usage information pertaining to this script and exit                                                            
############################################################################### 
set -e

usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " --deploy-tool                Location from where to download Deploy tool"
    echo " -v, --image-version           Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " --iothub-hostname             IoTHub hostname"
    echo " --iothubowner-access-key      Shared iothubowner access key used by deployment to authenticate with IoTHub"
    echo " --iothubowner-access-key-name Shared iothubowner access key name used by deployment to authenticate with IoTHub"
    echo " --device-id                   Edge device ID"
    echo " --arch                        Target architecture (default: uname -m)"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Function to obtain the underlying architecture and check if supported
###############################################################################
check_arch()
{
    if [ -z ${ARCH} ]; then
        if [ "x86_64" == "$(uname -m)" ]; then
            ARCH="amd64"
        else
            ARCH="arm32v7"
        fi
        echo "Detected architecture: $ARCH"
    elif [ "$ARCH" == "x86_64" ]; then
        ARCH="amd64"
    elif [ "$ARCH" == "armv7l" ]; then
        ARCH="arm32v7"
    else
        echo "Unsupported architecture"
        exit 1
    fi
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
            DEPLOY_TOOL="$arg"
            save_next_arg=0   
        elif [ $save_next_arg -eq 2 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0       
        elif [ $save_next_arg -eq 3 ]; then
            IOTHUB_HOSTNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            IOTHUBOWNER_SHARED_ACCESS_KEY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            IOTHUBOWNER_SHARED_ACCESS_KEY_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            DEVICEID="$arg"
            save_next_arg=0    
        elif [ $save_next_arg -eq 7 ]; then
            ARCH="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "--deploy-tool" ) save_next_arg=1;;
                "-v" | "--image-version" ) save_next_arg=2;;
                "--iothub-hostname" ) save_next_arg=3;;
                "--iothubowner-access-key" ) save_next_arg=4;;
                "--iothubowner-access-key-name" ) save_next_arg=5;;
                "--device-id" ) save_next_arg=6;;
                "--arch" ) save_next_arg=7;;
                * ) usage;;
            esac
        fi
    done

    if [ -z ${DEPLOY_TOOL} ]; then
        echo "Deploy tool Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${DOCKER_IMAGEVERSION} ]; then
        if [ ! -z "${BUILD_BUILDNUMBER}" ]; then
            DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
        else
            echo "Docker image version not found."
            print_help_and_exit
        fi
    fi
    
    if [ -z ${IOTHUB_HOSTNAME} ]; then
        echo "IoT hostname Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${IOTHUBOWNER_SHARED_ACCESS_KEY} ]; then
        echo "IotHub owner shared access key Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${IOTHUBOWNER_SHARED_ACCESS_KEY_NAME} ]; then
        echo "IotHub owner shared access key name Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${DEPLOY_TOOL} ]; then
        echo "Deploy tool Parameter Invalid"
        print_help_and_exit
    fi
}

function parse_config_file()
{
    local cfg_file="${1}"

    agent_image_name="edgebuilds.azurecr.io/azureiotedge/edge-agent-linux-$ARCH:$DOCKER_IMAGEVERSION"
    edgehub_image_name="edgebuilds.azurecr.io/azureiotedge/edge-hub-linux-$ARCH:$DOCKER_IMAGEVERSION"
    $(jq -r --arg var1 $agent_image_name '.moduleContent."$edgeAgent"."properties.desired".systemModules.edgeAgent.settings.image = "\($var1)"' ${cfg_file} > edgeConfiguration_release_temp1.json)

    $(jq -r --arg var1 $edgehub_image_name '.moduleContent."$edgeAgent"."properties.desired".systemModules.edgeHub.settings.image = "\($var1)"' edgeConfiguration_release_temp1.json > edgeConfiguration_release_temp2.json)

    $(jq -r '.moduleContent."$edgeAgent"."properties.desired".modules={}' edgeConfiguration_release_temp2.json > edgeConfiguration_release_temp3.json)

    cp edgeConfiguration_release_temp3.json $cfg_file 
    rm -f edgeConfiguration_release_temp1.json edgeConfiguration_release_temp2.json edgeConfiguration_release_temp3.json
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"
check_arch

agent_image_name="edgebuilds.azurecr.io/azureiotedge/edge-agent-linux-$ARCH:$DOCKER_IMAGEVERSION"
edgehub_image_name="edgebuilds.azurecr.io/azureiotedge/edge-hub-linux-$ARCH:$DOCKER_IMAGEVERSION"
iothub_connection="HostName=$IOTHUB_HOSTNAME;SharedAccessKeyName=$IOTHUBOWNER_SHARED_ACCESS_KEY_NAME;SharedAccessKey=$IOTHUBOWNER_SHARED_ACCESS_KEY"
deploy_tool_path=deploy

rm -rf $deploy_tool_path
mkdir $deploy_tool_path                                                                                                 

echo Downloading package $DEPLOY_TOOL
if wget -q $DEPLOY_TOOL; then
    echo Downloaded Deploy tool
else
    echo Error downloading Deploy tool
    exit 1
fi

echo Unzip deploy tool
deploy_file=$(basename $DEPLOY_TOOL)
tar -xvf $deploy_file
rm -f $deploy_file    

echo Set configuration to $DEVICEID
pushd $deploy_tool_path

echo Updating apt-get
sudo apt-get update

echo Installing jq
sudo apt-get install -y jq

echo Sanitize config file
sed -e s%//.*%%g edgeConfiguration.json > edgeConfiguration_release.json

parse_config_file "edgeConfiguration_release.json"

./edge.sh configSet -d $DEVICEID -c edgeConfiguration_release.json -l $iothub_connection

if [ $? -gt 0 ]; then
    RES=1
    echo "Error running deployment RES = $RES"
fi

popd

exit $RES