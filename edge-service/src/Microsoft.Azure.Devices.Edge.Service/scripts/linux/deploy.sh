#!/bin/bash

###############################################################################
# This script pulls Edge service docker image from the repository, stops the 
# container which runs the Edge service and starts it with the new image
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
    echo " -r, --registry       Docker registry required to pull the Edge service image"
    echo " -u, --username       Docker registry username"
    echo " -p, --password       Docker registry password"
    echo " -v, --image-version  Docker image version. Either use this option or set env variable BUILD_BUILDNUMBER"
    echo " --iothub-hostname    IoTHub hostname"
    echo " --device-id          Edge device ID"
    echo " --access-key         Shared access key used to authenticate the device with IoTHub"
    echo " --routes             Edge routes separated by , (comma)"
    echo " --arch               The architecture of the host machine (armv7hf|x64)"
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

process_routes() {
    if [ ! -z "$ROUTES" ]; then
        echo "Process routes"
        OIFS=$IFS
        IFS=','
        routes2=$ROUTES
        i=0
        docker_routes=()
        for element in $routes2
        do
            docker_routes+=(-e routes__$i=$element)
            i=$((i + 1))
        done
        IFS=$OIFS                              
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
            DOCKER_REGISTRY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DOCKER_USERNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            DOCKER_PASSWORD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            DOCKER_IMAGEVERSION="$arg"
            save_next_arg=0       
        elif [ $save_next_arg -eq 5 ]; then
            IOTHUB_HOSTNAME="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            DEVICEID="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 7 ]; then
            SHARED_ACCESS_KEY="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            ROUTES="$arg"
            process_routes
            save_next_arg=0
        elif [ $save_next_arg -eq 9 ]; then
            ARCH="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" | "--help" ) usage;;
                "-r" | "--registry" ) save_next_arg=1;;
                "-u" | "--username" ) save_next_arg=2;;
                "-p" | "--password" ) save_next_arg=3;;
                "-v" | "--image-version" ) save_next_arg=4;;
                "--iothub-hostname" ) save_next_arg=5;;
                "--device-id" ) save_next_arg=6;;
                "--access-key" ) save_next_arg=7;;
                "--routes" ) save_next_arg=8;;
                "--arch" ) save_next_arg=9;;
                * ) usage;;
            esac
        fi
    done

    if [ -z ${DOCKER_REGISTRY} ]; then
        echo "Registry Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${ARCH} ]; then
        if [ "x86_64" == "$(uname -m)" ]; then
            ARCH="x64"
        else
            ARCH="armv7hf"
        fi
        echo "Detected architecture: $ARCH"
    fi

    if [ -z ${DOCKER_USERNAME} ]; then
        echo "Docker Username Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${DOCKER_PASSWORD} ]; then
        echo "Docker Password Parameter Invalid"
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

    if [ -z ${DEVICEID} ]; then
        echo "DeviceID Parameter Invalid"
        print_help_and_exit
    fi

    if [ -z ${SHARED_ACCESS_KEY} ]; then
        echo "Shared access key Parameter Invalid"
        print_help_and_exit
    fi
}

###############################################################################
# Main Script Execution
###############################################################################
process_args "$@"

image_name="edge-service"
mma_connection="HostName=$IOTHUB_HOSTNAME;DeviceId=$DEVICEID;SharedAccessKey=$SHARED_ACCESS_KEY"

#echo Logging in to Docker registry
sudo docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD
if [ $? -ne 0 ]; then
    echo "Docker Login Failed!"
    exit 1
fi

# Stop and remove any existing edge-service containers
if [ $(sudo docker ps -a | grep $image_name | wc -l) -ne 0 ]; then
    sudo docker stop $image_name
    sudo docker rm $image_name
fi

# Remove any existing edge-service images
if [ $(sudo docker images | grep "edgebuilds.azurecr.io/azedge-edge-service-$ARCH" | wc -l) -ne 0 ]; then
    sudo docker rmi $(sudo docker images | grep "edgebuilds.azurecr.io/azedge-edge-service-$ARCH" | egrep -o '[a-fA-F0-9]{12}')
fi

# Pull current release version
sudo docker pull edgebuilds.azurecr.io/azedge-edge-service-$ARCH:$DOCKER_IMAGEVERSION
if [ $? -ne 0 ]; then
    echo "Docker Pull Failed!"
    exit 1
fi

# Run current release version
sudo docker run -d -v /var/run/docker.sock:/var/run/docker.sock --name $image_name -p 8883:8883 -p 443:443 -e DockerUri=unix:///var/run/docker.sock -e DeviceConnectionString=$mma_connection -e IotHubHostName=$IOTHUB_HOSTNAME -e EdgeDeviceId=$DEVICEID "${docker_routes[@]}" edgebuilds.azurecr.io/azedge-edge-service-$ARCH:$DOCKER_IMAGEVERSION
if [ $? -ne 0 ]; then
    echo "Docker run Failed!"
    exit 1
fi

exit 0
