#!/bin/bash

###############################################################################
# This script creates a new deployment for the device provided as argument,  
# runs boostrap setup python script and start Edge
###############################################################################

###############################################################################                                         
# Print usage information pertaining to this script and exit                                                            
############################################################################### 
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "Note: Depending on the options you might have to run this as root or sudo."
    echo ""
    echo "options"
    echo " -v, --image-version           Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " --iothub-hostname             IoTHub hostname"
    echo " --device-id                   Edge device ID"
    echo " --device-access-key           Shared device access key used to authenticate the device with IoTHub"
    echo " --iothubowner-access-key      Shared iothubowner access key used by deployment to authenticate with IoTHub"
    echo " --iothubowner-access-key-name Shared iothubowner access key name used by deployment to authenticate with IoTHub"
    echo " --arch                        Target architecture (default: uname -m)"
    echo " --edge-hostname               Edge Runtime DNS hostname (FQDN). Optional (default: OS reported hostname)"
    echo " --docker-registries-csv       Azure Edge Modules Container" \
         "Repositories. Optional."
    echo "                                  CSV expressed as" \
         "address,username,password."
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
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0       
        elif [ $save_next_arg -eq 2 ]; then
            IOTHUB_HOSTNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DEVICEID="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DEVICE_SHARED_ACCESS_KEY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 5 ]; then
            IOTHUBOWNER_SHARED_ACCESS_KEY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            IOTHUBOWNER_SHARED_ACCESS_KEY_NAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            ARCH="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            EDGE_HOSTNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 9 ]; then
            DOCKER_REGISTRIES_CSV="$arg"
            process_docker_registries
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-v" | "--image-version" ) save_next_arg=1;;
                "--iothub-hostname" ) save_next_arg=2;;
                "--device-id" ) save_next_arg=3;;
                "--device-access-key" ) save_next_arg=4;;
                "--iothubowner-access-key" ) save_next_arg=5;;
                "--iothubowner-access-key-name" ) save_next_arg=6;;
                "--arch" ) save_next_arg=7;;
                "--edge-hostname" ) save_next_arg=8;;
                "--docker-registries-csv") save_next_arg=9;;
                * ) usage;;
            esac
        fi
    done

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

    if [ -z ${DEVICEID} ]; then
        echo "DeviceID Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${DEVICE_SHARED_ACCESS_KEY} ]; then
        echo "Device shared access key Parameter Invalid"
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

    if [ -z ${DOCKER_REGISTRIES_CSV} ]; then
        echo "Docker registries csv Parameter Invalid"
        print_help_and_exit
    fi
}

process_docker_registries() {
    if [ ! -z "$DOCKER_REGISTRIES_CSV" ]; then
        echo "Process docker registries"
        OIFS=$IFS
        IFS=', ' read -r -a docker_registries <<< "$DOCKER_REGISTRIES_CSV"
        IFS=$OIFS                              
    fi    
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"
check_arch

agent_image_name="edgebuilds.azurecr.io/azureiotedge/edge-agent-linux-$ARCH:$DOCKER_IMAGEVERSION"
iothub_image_name="edgebuilds.azurecr.io/azureiotedge/edge-hub-linux-$ARCH:$DOCKER_IMAGEVERSION"
device_connection="HostName=$IOTHUB_HOSTNAME;DeviceId=$DEVICEID;SharedAccessKey=$DEVICE_SHARED_ACCESS_KEY"
iothub_connection="HostName=$IOTHUB_HOSTNAME;SharedAccessKeyName=$IOTHUBOWNER_SHARED_ACCESS_KEY_NAME;SharedAccessKey=$IOTHUBOWNER_SHARED_ACCESS_KEY"

echo Set configuration to $DEVICEID
pushd  edgescripts/scripts/deploy
sed -e s%edge-agent-linux-$ARCH%$agent_image_name%g -e s%edge-hub-linux-$ARCH%$iothub_image_name%g edgeConfiguration.json > edgeConfiguration_release.json

./edge.sh configSet -d $DEVICEID -c edgeConfiguration_release.json -l $iothub_connection

popd

echo Bootstrap Edge

pushd edge-bootstrap

install_tar="azure-iot-edge-ctl-1.0.0rc1.dev$DOCKER_IMAGEVERSION.tar.gz"
tar xvzf $install_tar

pushd azure-iot-edge-ctl-1.0.0rc1.dev$DOCKER_IMAGEVERSION
sudo pip install .
popd

popd

edge_hostname=
if [ ! -z ${EDGE_HOSTNAME} ]; then
    edge_hostname="--edge-hostname $EDGE_HOSTNAME"
fi

RES=0

sudo iotedgectl --verbose WARNING setup --connection-string "$device_connection" --docker-edge-runtime-image "$agent_image_name" --docker-uri  "unix:///var/run/docker.sock" --docker-registries ${docker_registries[@]} $edge_hostname

if [ $? -gt 0 ]; then
    RES=1
    echo "Error running setup RES = $RES"
    exit $RES
fi

sudo iotedgectl --verbose WARNING start                                                                                 

if [ $? -gt 0 ]; then
    RES=1
    echo "Error running start RES = $RES"
fi

exit $RES