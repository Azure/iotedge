#!/usr/bin/env bash

while :; do
    case $1 in
        -h|-\?|--help)
            show_help
            exit;;
        -c=?*)
            configFilePath=${1#*=}
            if [ ! -f "${configFilePath}" ]; then
              echo "Configuration file not found. Exiting."
              exit 1
            fi;;
        -c=)
            echo "Missing configuration file path. Exiting."
            exit;;
        -hubrg=?*)
            iotHubResourceGroup=${1#*=}
            ;;
        -hubrg=)
            echo "Missing IoT Hub resource group. Exiting."
            exit;;
        -connectionString=?*)
            connectionString=${1#*=}
            ;;
        -connectionString=)
            echo "Missing connection String. Exiting."
            exit;;
        -s=?*)
            subscription=${1#*=}
            ;;
        -s=)
            echo "Missing subscription. Exiting."
            exit;;
        --)
            shift
            break;;
        *)
            break
    esac
    shift
done
hubname=$(echo $connectionString | sed -n 's/HostName=\(.*\);SharedAccessKeyName.*/\1/p')
az account set --subscription $subscription
source ${scriptFolder}/parseConfigFile.sh $configFilePath
az iot hub device-identity create -n $iotHubName -d ${iotEdgeDevices[i]} --ee --pd ${iotEdgeParentDevices[i]} --output none
