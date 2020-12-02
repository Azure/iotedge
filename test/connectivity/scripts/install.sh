#!/usr/bin/env bash

function prepare_test_from_artifacts() {
    print_highlighted_message 'Prepare test from artifacts'

    echo 'Remove working folder'
    rm -rf "$working_folder"
    mkdir -p "$working_folder"

    declare -a pkg_list=( $iotedged_artifact_folder/*.deb )
    iotedge_package="${pkg_list[*]}"
    echo "iotedge_package=$iotedge_package"

    echo 'Extract quickstart to working folder'
    mkdir -p "$quickstart_working_folder"
    tar -C "$quickstart_working_folder" -xzf "$iotedge_quickstart_artifact_file"

    echo "Copy deployment file from $connectivity_deployment_artifact_file"
    cp "$connectivity_deployment_artifact_file" "$deployment_working_file"
    
    sed -i -e "s@<Architecture>@$image_architecture_label@g" "$deployment_working_file"
    sed -i -e "s/<Build.BuildNumber>/$ARTIFACT_IMAGE_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s/<EdgeRuntime.BuildNumber>/$EDGE_RUNTIME_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s@<Container_Registry>@$CONTAINER_REGISTRY@g" "$deployment_working_file"
    sed -i -e "s@<CR.Username>@$CONTAINER_REGISTRY_USERNAME@g" "$deployment_working_file"
    sed -i -e "s@<CR.Password>@$CONTAINER_REGISTRY_PASSWORD@g" "$deployment_working_file"
    sed -i -e "s@<IoTHubConnectionString>@$IOT_HUB_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<TestDuration>@$TEST_DURATION@g" "$deployment_working_file"
    sed -i -e "s@<TestStartDelay>@$TEST_START_DELAY@g" "$deployment_working_file"
    sed -i -e "s@<TrackingId>@$tracking_id@g" "$deployment_working_file"
    sed -i -e "s@<LogAnalyticsWorkspaceId>@$LOG_ANALYTICS_WORKSPACEID@g" "$deployment_working_file"
    sed -i -e "s@<LogAnalyticsSharedKey>@$LOG_ANALYTICS_SHAREDKEY@g" "$deployment_working_file"
    sed -i -e "s@<UpstreamProtocol>@$UPSTREAM_PROTOCOL@g" "$deployment_working_file"
    
    if [[ ! -z "$CUSTOM_EDGE_AGENT_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-agent:.*\"@\"image\": \"$CUSTOM_EDGE_AGENT_IMAGE\"@g" "$deployment_working_file"
    fi
    
    if [[ ! -z "$CUSTOM_EDGE_HUB_IMAGE" ]]; then
        sed -i -e "s@\"image\":.*azureiotedge-hub:.*\"@\"image\": \"$CUSTOM_EDGE_HUB_IMAGE\"@g" "$deployment_working_file"
    fi
    
    sed -i -e "s@<LoadGen.MessageFrequency>@$LOADGEN_MESSAGE_FREQUENCY@g" "$deployment_working_file"

    sed -i -e "s@<EdgeHubRestartTest.RestartPeriod>@$RESTART_TEST_RESTART_PERIOD@g" "$deployment_working_file"
    sed -i -e "s@<EdgeHubRestartTest.SdkOperationTimeout>@$RESTART_TEST_SDK_OPERATION_TIMEOUT@g" "$deployment_working_file"

    sed -i -e "s@<TestResultCoordinator.ConsumerGroupId>@$EVENT_HUB_CONSUMER_GROUP_ID@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.EventHubConnectionString>@$EVENTHUB_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.VerificationDelay>@$VERIFICATION_DELAY@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.OptimizeForPerformance>@$optimize_for_performance@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.LogAnalyticsLogType>@$LOG_ANALYTICS_LOGTYPE@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.logUploadEnabled>@$log_upload_enabled@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.StorageAccountConnectionString>@$STORAGE_ACCOUNT_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<TestInfo>@$TEST_INFO@g" "$deployment_working_file"

    sed -i -e "s@<NetworkController.OfflineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[0]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.OnlineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[1]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.RunsCount0>@${NETWORK_CONTROLLER_FREQUENCIES[2]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.RunProfile>@$NETWORK_CONTROLLER_RUNPROFILE@g" "$deployment_working_file"
    
    sed -i -e "s@<DeploymentTester1.DeploymentUpdatePeriod>@$DEPLOYMENT_TEST_UPDATE_PERIOD@g" "$deployment_working_file"

    sed -i -e "s@<MetricsCollector.MetricsEndpointsCSV>@$METRICS_ENDPOINTS_CSV@g" "$deployment_working_file"
    sed -i -e "s@<MetricsCollector.ScrapeFrequencyInSecs>@$METRICS_SCRAPE_FREQUENCY_IN_SECS@g" "$deployment_working_file"
    sed -i -e "s@<MetricsCollector.UploadTarget>@$METRICS_UPLOAD_TARGET@g" "$deployment_working_file"
}

function test_setup() {
    local funcRet=0

    validate_test_parameters && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
    clean_up && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
    prepare_test_from_artifacts && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
    create_iotedge_service_config && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi
}
set -e

# Import test-related functions
. $(dirname "$0")/testHelper.sh

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

test_setup

az account set --subscription $subscription
source ${scriptFolder}/parseConfigFile.sh $configFilePath
az iot hub device-identity create -n $iotHubName -d ${iotEdgeDevices[i]} --ee --pd ${iotEdgeParentDevices[i]} --output none
