#!/bin/bash

###############################################################################
# This script is used to run connectivity test for Linux.
###############################################################################
set -e

# Import test-related functions
. $(dirname "$0")/testHelper.sh

function examine_test_result() {
    found_test_passed="$(docker logs testResultCoordinator 2>&1 | sed -n '/Test summary/,/"TestResultReports"/p' | grep '"IsPassed": true')"

    if [[ -z "$found_test_passed" ]]; then
        echo 0
    else
        echo 1
    fi
}

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

function print_deployment_logs() {
    print_highlighted_message 'LOGS FROM IOTEDGED'
    journalctl -u iotedge -u docker --since "$test_start_time" --no-pager || true

    print_highlighted_message 'edgeAgent LOGS'
    docker logs edgeAgent || true
}

function print_test_run_logs() {
    local ret=$1

    print_highlighted_message "test run exit code=$ret"
    print_highlighted_message 'Print logs'
    print_highlighted_message 'testResultCoordinator LOGS'
    docker logs testResultCoordinator || true

    if (( ret < 1 )); then
        return;
    fi

    print_highlighted_message 'LOGS FROM IOTEDGED'
    journalctl -u iotedge -u docker --since "$test_start_time" --no-pager || true

    print_highlighted_message 'edgeAgent LOGS'
    docker logs edgeAgent || true

    print_highlighted_message 'edgeHub LOGS'
    docker logs edgeHub || true

    print_highlighted_message 'loadGen1 LOGS'
    docker logs loadGen1 || true

    print_highlighted_message 'loadGen2 LOGS'
    docker logs loadGen2 || true

    print_highlighted_message 'relayer1 LOGS'
    docker logs relayer1 || true

    print_highlighted_message 'relayer2 LOGS'
    docker logs relayer2 || true

    print_highlighted_message 'directMethodSender1 LOGS'
    docker logs directMethodSender1 || true

    print_highlighted_message 'directMethodReceiver1 LOGS'
    docker logs directMethodReceiver1 || true

    print_highlighted_message 'directMethodSender2 LOGS'
    docker logs directMethodSender2 || true

    print_highlighted_message 'directMethodReceiver2 LOGS'
    docker logs directMethodReceiver2 || true

    print_highlighted_message 'directMethodReceiver3 LOGS'
    docker logs directMethodReceiver3 || true

    print_highlighted_message 'twinTester1 LOGS'
    docker logs twinTester1 || true

    print_highlighted_message 'twinTester2 LOGS'
    docker logs twinTester2 || true

    print_highlighted_message 'twinTester3 LOGS'
    docker logs twinTester3 || true

    print_highlighted_message 'twinTester4 LOGS'
    docker logs twinTester4 || true

    print_highlighted_message 'networkController LOGS'
    docker logs networkController || true
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

function run_connectivity_test() {
    print_highlighted_message "Run connectivity test for $image_architecture_label"

    local funcRet=0
    test_setup && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-connect-$(get_hash 8)"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run connectivity test with -d '$device_id' started at $test_start_time"

    SECONDS=0

    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOT_HUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        --leave-running=All \
        -l "$deployment_working_file" \
        --runtime-log-level "$TEST_RUNTIME_LOG_LEVEL" \
        --no-verify && funcRet=$? || funcRet=$?

    local elapsed_time="$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$SECONDS")"
    print_highlighted_message "Deploy connectivity test with -d '$device_id' completed in $elapsed_time"
    
    if [ $funcRet -ne 0 ]; then
        print_highlighted_message "Deploy connectivity test failed."
        print_deployment_logs
        return $funcRet
    fi

    print_highlighted_message "Deploy connectivity test succeeded."

    # Delay for (buffer for module download + test start delay + test duration + verification delay + report generation)
    local module_download_buffer=300
    local time_for_test_to_complete=$(($module_download_buffer + \
                                    $(echo $TEST_START_DELAY | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $TEST_DURATION | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $VERIFICATION_DELAY | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $TIME_FOR_REPORT_GENERATION | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }')))
    echo "test start delay=$TEST_START_DELAY"
    echo "test duration=$TEST_DURATION"
    echo "verificaiton delay=$VERIFICATION_DELAY"
    echo "time for report generation=$TIME_FOR_REPORT_GENERATION"
    echo "time for test to complete in seconds=$time_for_test_to_complete"

    if [ $WAIT_FOR_TEST_COMPLETE -eq 1 ]; then
        local sleep_frequency_secs=60
        local total_wait=0

        while [ $total_wait -lt $time_for_test_to_complete ]
        do
            local is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)
            
            if [ $is_build_canceled -eq 1 ]; then
                print_highlighted_message "build is canceled."
                stop_iotedge_service || true
                return 3
            fi
        
            sleep "$sleep_frequency_secs"s
            total_wait=$((total_wait+sleep_frequency_secs))
            echo "total wait time=$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$total_wait")"
        done

        test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
        print_highlighted_message "Connectivity test should be completed at $test_end_time."
        testExitCode=$(examine_test_result)
        if [[ "$(examine_test_result)" -eq '0' ]]; then
            testExitCode=1
        else
            testExitCode=0
        fi

        print_test_run_logs $testExitCode

        # stop IoT Edge service after test complete to prevent sending metrics
        sudo systemctl stop iotedge
    fi

    return $testExitCode
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

function validate_test_parameters() {
    print_highlighted_message "Validate test parameters"

    local required_files=()
    local required_folders=()

    required_files+=("$iotedge_quickstart_artifact_file")
    required_folders+=("$iotedged_artifact_folder")

    required_files+=($connectivity_deployment_artifact_file)

    local error=0
    for f in "${required_files[@]}"
    do
        if [ ! -f "$f" ]; then
            print_error "Required file, $f doesn't exist."
            ((error++))
        fi
    done

    for d in "${required_folders[@]}"
    do
        if [ ! -d "$d" ]; then
            print_error "Required directory, $d doesn't exist."
            ((error++))
        fi
    done

    if [[ -z "$TEST_INFO" ]]; then
        print_error "Required test info."
        ((error++))
    fi

    if (( error > 0 )); then
        exit 1
    fi
}

function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ''
    echo 'options'
    echo ' -testDir                                 Path of E2E test directory which contains artifacts and certs folders; defaul to current directory.'
    echo ' -releaseLabel                            Release label is used as part of Edge device id to make it unique.'
    echo ' -artifactImageBuildNumber                Artifact image build number is used to construct path of docker images, pulling from docker registry. E.g. 20190101.1.'
    echo " -containerRegistry                       Host address of container registry."
    echo " -containerRegistryUsername               Username of container registry."
    echo ' -containerRegistryPassword               Password of given username for container registory.'
    echo ' -iotHubConnectionString                  IoT hub connection string for creating edge device.'
    echo ' -eventHubConnectionString                Event hub connection string for receive D2C messages.'
    echo ' -eventHubConsumerGroupId                 Event hub consumer group for receive D2C messages.'
    echo ' -testDuration                            Connectivity test duration'
    echo ' -testStartDelay                          Tests start after delay for applicable modules'
    echo ' -loadGenMessageFrequency                 Message frequency sent by load gen'
    echo ' -networkControllerFrequency              Frequency for controlling the network with offlineFrequence, onlineFrequence, runsCount. Example "00:05:00 00:05:00 6"'
    echo ' -networkControllerRunProfile             Online, Offline, SatelliteGood or Cellular3G'
    echo ' -logAnalyticsWorkspaceId                 Log Analytics Workspace Id'
    echo ' -logAnalyticsSharedKey                   Log Analytics shared key'
    echo ' -logAnalyticsLogType                     Log Analytics log type'
    echo ' -verificationDelay                       Delay before starting the verification after test finished'
    echo ' -upstreamProtocol                        Upstream protocol used to connect to IoT Hub'
    echo ' -deploymentTestUpdatePeriod              duration of updating deployment of target module in deployment test'
    echo ' -timeForReportingGeneration              Time reserved for report generation'
    echo ' -waitForTestComplete                     Wait for test to complete if this parameter is provided.  Otherwise it will finish once deployment is done.'
    echo ' -metricsEndpointsCSV                     Csv of exposed endpoints for which to scrape metrics.'
    echo ' -metricsScrapeFrequencyInSecs            Frequency at which the MetricsCollector module will scrape metrics from the exposed metrics endpoints. Default is 300 seconds.'
    echo ' -metricsUploadTarget                     Upload target for metrics. Valid values are AzureLogAnalytics or IoTHub. Default is AzureLogAnalytics.'
    echo ' -deploymentFileName                      Deployment file name'
    echo ' -EdgeHubRestartTestRestartPeriod         EdgeHub restart period (must be greater than 1 minutes)'
    echo ' -EdgeHubRestartTestSdkOperationTimeout   SDK retry timeout'
    echo ' -storageAccountConnectionString          Azure storage account connection string with privilege to create blob container.'
    echo ' -edgeRuntimeBuildNumber                  Build number for specifying edge runtime (edgeHub and edgeAgent)'
    echo ' -testRuntimeLogLevel                     RuntimeLogLevel given to Quickstart, which is given to edgeAgent and edgeHub.'
    echo ' -testInfo                                Contains comma delimiter test information, e.g. build number and id, source branches of build, edgelet and images.'
    echo ' -cleanAll                                Do docker prune for containers, logs and volumes.'
    exit 1;
}

is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)         
if [ $is_build_canceled -eq 1 ]; then
    print_highlighted_message "build is canceled."
    exit 3
fi

process_args "$@"

CONTAINER_REGISTRY="${CONTAINER_REGISTRY:-edgebuilds.azurecr.io}"
E2E_TEST_DIR="${E2E_TEST_DIR:-$(pwd)}"
TEST_DURATION="${TEST_DURATION:-01:00:00}"
DEPLOYMENT_TEST_UPDATE_PERIOD="${DEPLOYMENT_TEST_UPDATE_PERIOD:-00:03:00}"
EVENT_HUB_CONSUMER_GROUP_ID=${EVENT_HUB_CONSUMER_GROUP_ID:-\$Default}
EDGE_RUNTIME_BUILD_NUMBER=${EDGE_RUNTIME_BUILD_NUMBER:-$ARTIFACT_IMAGE_BUILD_NUMBER}
LOADGEN_MESSAGE_FREQUENCY="${LOADGEN_MESSAGE_FREQUENCY:-00:00:01}"
NETWORK_CONTROLLER_FREQUENCIES=${NETWORK_CONTROLLER_FREQUENCIES:(null)}
NETWORK_CONTROLLER_RUNPROFILE=${NETWORK_CONTROLLER_RUNPROFILE:-Offline}
TEST_START_DELAY="${TEST_START_DELAY:-00:02:00}"
LOG_ANALYTICS_LOGTYPE="${LOG_ANALYTICS_LOGTYPE:-connectivity}"
VERIFICATION_DELAY="${VERIFICATION_DELAY:-00:15:00}"
UPSTREAM_PROTOCOL="${UPSTREAM_PROTOCOL:-Amqp}"
TIME_FOR_REPORT_GENERATION="${TIME_FOR_REPORT_GENERATION:-00:10:00}"

working_folder="$E2E_TEST_DIR/working"
quickstart_working_folder="$working_folder/quickstart"
get_image_architecture_label
optimize_for_performance=true
log_upload_enabled=true
if [ "$image_architecture_label" = 'arm32v7' ] ||
   [ "$image_architecture_label" = 'arm64v8' ]; then
    optimize_for_performance=false
    log_upload_enabled=false
fi

iotedged_artifact_folder="$(get_iotedged_artifact_folder $E2E_TEST_DIR)"
iotedge_quickstart_artifact_file="$(get_iotedge_quickstart_artifact_file $E2E_TEST_DIR)"
connectivity_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/$DEPLOYMENT_FILE_NAME"
deployment_working_file="$working_folder/deployment.json"

tracking_id=$(cat /proc/sys/kernel/random/uuid)
TEST_INFO="$TEST_INFO,TestId=$tracking_id"
TEST_INFO="$TEST_INFO,UpstreamProtocol=$UPSTREAM_PROTOCOL"
TEST_INFO="$TEST_INFO,NetworkControllerOfflineFrequency=${NETWORK_CONTROLLER_FREQUENCIES[0]}"
TEST_INFO="$TEST_INFO,NetworkControllerOnlineFrequency=${NETWORK_CONTROLLER_FREQUENCIES[1]}"
TEST_INFO="$TEST_INFO,NetworkControllerRunsCount=${NETWORK_CONTROLLER_FREQUENCIES[2]}"
TEST_INFO="$TEST_INFO,TestDuration=${TEST_DURATION}"

testRet=0
run_connectivity_test && testRet=$? || testRet=$?
echo "Test exit with result code $testRet"
exit $testRet
