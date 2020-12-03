#!/usr/bin/env bash

function prepare_test_from_artifacts() {
    
    print_highlighted_message 'Prepare test from artifacts'

    echo 'Remove working folder'
    rm -rf "$working_folder"
    mkdir -p "$working_folder"

    declare -a pkg_list=( $iotedged_artifact_folder/*.deb )
    iotedge_package="${pkg_list[*]}"
    echo "iotedge_package=$iotedge_package"

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
            EVENTHUB_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 9 ]; then
            EVENT_HUB_CONSUMER_GROUP_ID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 10 ]; then
            TEST_DURATION="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 11 ]; then
            TEST_START_DELAY="$arg"
            saveNextArg=0 
        elif [ $saveNextArg -eq 12 ]; then
            LOADGEN_MESSAGE_FREQUENCY="$arg"
            saveNextArg=0   
        elif [ $saveNextArg -eq 13 ]; then
            NETWORK_CONTROLLER_FREQUENCIES=($arg)
            saveNextArg=0
        elif [ $saveNextArg -eq 14 ]; then
            NETWORK_CONTROLLER_RUNPROFILE="$arg"
            saveNextArg=0    
        elif [ $saveNextArg -eq 15 ]; then
            LOG_ANALYTICS_WORKSPACEID="$arg"
            saveNextArg=0 
        elif [ $saveNextArg -eq 16 ]; then
            LOG_ANALYTICS_SHAREDKEY="$arg"
            saveNextArg=0 
        elif [ $saveNextArg -eq 17 ]; then
            LOG_ANALYTICS_LOGTYPE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 18 ]; then
            VERIFICATION_DELAY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 19 ]; then
            UPSTREAM_PROTOCOL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 20 ]; then
            DEPLOYMENT_TEST_UPDATE_PERIOD="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 21 ]; then
            TIME_FOR_REPORT_GENERATION="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 22 ]; then
            METRICS_ENDPOINTS_CSV="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 23 ]; then
            METRICS_SCRAPE_FREQUENCY_IN_SECS="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 24 ]; then
            METRICS_UPLOAD_TARGET="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 25 ]; then
            STORAGE_ACCOUNT_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 26 ]; then
            DEVOPS_ACCESS_TOKEN="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 27 ]; then
            DEVOPS_BUILDID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 28 ]; then
            DEPLOYMENT_FILE_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 29 ]; then
            RESTART_TEST_RESTART_PERIOD="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 30 ]; then
            RESTART_TEST_SDK_OPERATION_TIMEOUT="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 31 ]; then
            EDGE_RUNTIME_BUILD_NUMBER="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 32 ]; then
            CUSTOM_EDGE_AGENT_IMAGE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 33 ]; then
            CUSTOM_EDGE_HUB_IMAGE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 34 ]; then
            TEST_RUNTIME_LOG_LEVEL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 35 ]; then
            TEST_INFO="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 36 ]; then
            SUBSCRIPTION="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 37 ]; then
            LEVEL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 38 ]; then
            PARENT_IOTEDGE_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 39 ]; then
            BLOB_STORAGE_CONNECTION_STRING="$arg"
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
                '-eventHubConnectionString' ) saveNextArg=8;;
                '-eventHubConsumerGroupId' ) saveNextArg=9;;
                '-testDuration' ) saveNextArg=10;;
                '-testStartDelay' ) saveNextArg=11;;
                '-loadGenMessageFrequency' ) saveNextArg=12;;
                '-networkControllerFrequency' ) saveNextArg=13;;
                '-networkControllerRunProfile' ) saveNextArg=14;;
                '-logAnalyticsWorkspaceId' ) saveNextArg=15;;
                '-logAnalyticsSharedKey' ) saveNextArg=16;;
                '-logAnalyticsLogType' ) saveNextArg=17;;
                '-verificationDelay' ) saveNextArg=18;;
                '-upstreamProtocol' ) saveNextArg=19;;
                '-deploymentTestUpdatePeriod' ) saveNextArg=20;;
                '-timeForReportingGeneration' ) saveNextArg=21;;
                '-metricsEndpointsCSV' ) saveNextArg=22;;
                '-metricsScrapeFrequencyInSecs' ) saveNextArg=23;;
                '-metricsUploadTarget' ) saveNextArg=24;;
                '-storageAccountConnectionString' ) saveNextArg=25;;
                '-devOpsAccessToken' ) saveNextArg=26;;
                '-devOpsBuildId' ) saveNextArg=27;;
                '-deploymentFileName' ) saveNextArg=28;;
                '-EdgeHubRestartTestRestartPeriod' ) saveNextArg=29;;
                '-EdgeHubRestartTestSdkOperationTimeout' ) saveNextArg=30;;
                '-edgeRuntimeBuildNumber' ) saveNextArg=31;;
                '-customEdgeAgentImage' ) saveNextArg=32;;
                '-customEdgeHubImage' ) saveNextArg=33;;
                '-testRuntimeLogLevel' ) saveNextArg=34;;
                '-testInfo' ) saveNextArg=35;;
                '-subscription' ) saveNextArg=36;;
                '-level' ) saveNextArg=37;;
                '-parentIoTedgeName' ) saveNextArg=38;;    
                '-blobstorageConnectionString' ) saveNextArg=39;;             
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
    [[ -z "$BLOB_STORAGE_CONNECTION_STRING" ]] && { print_error 'BLOB_STORAGE_CONNECTION_STRING is required.'; exit 1; }
    [[ -z "$SUBSCRIPTION" ]] && { print_error 'SUBSCRIPTION is required.'; exit 1; }
    [[ -z "$LEVEL" ]] && { print_error 'Level is required.'; exit 1; }
    [[ -z "$RELEASE_LABEL" ]] && { print_error 'Release label is required.'; exit 1; }
    [[ -z "$ARTIFACT_IMAGE_BUILD_NUMBER" ]] && { print_error 'Artifact image build number is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_USERNAME" ]] && { print_error 'Container registry username is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_PASSWORD" ]] && { print_error 'Container registry password is required'; exit 1; }
    [[ -z "$DEPLOYMENT_FILE_NAME" ]] && { print_error 'Deployment file name is required'; exit 1; }
    [[ -z "$EVENTHUB_CONNECTION_STRING" ]] && { print_error 'Event hub connection string is required'; exit 1; }
    [[ -z "$IOT_HUB_CONNECTION_STRING" ]] && { print_error 'IoT hub connection string is required'; exit 1; }
    [[ -z "$LOG_ANALYTICS_SHAREDKEY" ]] && { print_error 'Log analytics shared key is required'; exit 1; }
    [[ -z "$LOG_ANALYTICS_WORKSPACEID" ]] && { print_error 'Log analytics workspace id is required'; exit 1; }
    [[ -z "$METRICS_ENDPOINTS_CSV" ]] && { print_error 'Metrics endpoints csv is required'; exit 1; }
    [[ -z "$METRICS_SCRAPE_FREQUENCY_IN_SECS" ]] && { print_error 'Metrics scrape frequency is required'; exit 1; }
    [[ -z "$METRICS_UPLOAD_TARGET" ]] && { print_error 'Metrics upload target is required'; exit 1; }
    [[ -z "$STORAGE_ACCOUNT_CONNECTION_STRING" ]] && { print_error 'Storage account connection string is required'; exit 1; }

    echo 'Required parameters are provided'
}

function test_setup() {
    local funcRet=0

    #validate_test_parameters && funcRet=$? || funcRet=$?
    #if [ $funcRet -ne 0 ]; then return $funcRet; fi
    
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

is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)         
if [ "$is_build_canceled" -eq 1 ]; then
    print_highlighted_message "build is canceled."
    exit 3
fi

process_args "$@"

working_folder="$E2E_TEST_DIR/working"
#@TODO remove hardcoding
#connectivity_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
connectivity_deployment_artifact_file="e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
deployment_working_file="$working_folder/deployment.json"

get_image_architecture_label

test_setup

#extract full hub name
tmp=$(echo $IOT_HUB_CONNECTION_STRING | sed -n 's/HostName=\(.*\);SharedAccessKeyName.*/\1/p')
#remove the .azure-devices.net  from it.
iotHubName=$(echo andsmi-iotedgequickstart-hub.azure-devices.net | sed -n 's/\(.?*\)\..*/\1/p')

az account set --subscription $SUBSCRIPTION
iotEdgeDevicesName="level_${LEVEL}_${EDGE_RUNTIME_BUILD_NUMBER}"

echo "Creating ${iotEdgeDevicesName} iotedge in iothub: ${iotHubName}, in subscription $SUBSCRIPTION"
if [ "$LEVEL" = "5" ]; then
    az iot hub device-identity create -n ${iotHubName} -d ${iotEdgeDevicesName} --ee --output none
    
else
    az iot hub device-identity create -n ${iotHubName} -d ${iotEdgeDevicesName} --ee --pd ${PARENT_IOTEDGE_NAME} --output none
fi

az storage blob download --file ../here --container-name test-certificates --name test-certs.tar.bz2 --connection-string ${BLOB_STORAGE_CONNECTION_STRING}

az iot edge set-modules --device-id ${iotEdgeDevicesName} --hub-name ${iotHubName} --content ${deployment_working_file} --output none

#clean up
#az iot hub device-identity delete -n ${iotHubName} -d ${iotEdgeDevicesName}