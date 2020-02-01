#!/bin/bash

###############################################################################
# This script is used to run connectivity test for Linux.
###############################################################################
set -e

# Import test-related functions
. $(dirname "$0")/testHelper.sh

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

    sed -i -e "s@<TestResultCoordinator.ConsumerGroupId>@$EVENT_HUB_CONSUMER_GROUP_ID@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.EventHubConnectionString>@$EVENTHUB_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<Architecture>@$image_architecture_label@g" "$deployment_working_file"
    sed -i -e "s/<Build.BuildNumber>/$ARTIFACT_IMAGE_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s@<Container_Registry>@$CONTAINER_REGISTRY@g" "$deployment_working_file"
    sed -i -e "s@<CR.Username>@$CONTAINER_REGISTRY_USERNAME@g" "$deployment_working_file"
    sed -i -e "s@<CR.Password>@$CONTAINER_REGISTRY_PASSWORD@g" "$deployment_working_file"

    local tracking_Id=$(cat /proc/sys/kernel/random/uuid)

    sed -i -e "s@<LoadGen.MessageFrequency>@$LOADGEN_MESSAGE_FREQUENCY@g" "$deployment_working_file"
    sed -i -e "s@<TestDuration>@$TEST_DURATION@g" "$deployment_working_file"
    sed -i -e "s@<TestStartDelay>@$TEST_START_DELAY@g" "$deployment_working_file"
    sed -i -e "s@<TrackingId>@$tracking_Id@g" "$deployment_working_file"
    sed -i -e "s@<UpstreamProtocol>@$UPSTREAM_PROTOCOL@g" "$deployment_working_file"

    sed -i -e "s@<TestResultCoordinator.VerificationDelay>@$VERIFICATION_DELAY@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.OptimizeForPerformance>@$optimize_for_performance@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.LogAnalyticsWorkspaceId>@$LOG_ANALYTICS_WORKSPACEID@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.LogAnalyticsSharedKey>@$LOG_ANALYTICS_SHAREDKEY@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.LogAnalyticsLogType>@$LOG_ANALYTICS_LOGTYPE@g" "$deployment_working_file"

    sed -i -e "s@<NetworkController.OfflineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[0]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.OnlineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[1]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.RunsCount0>@${NETWORK_CONTROLLER_FREQUENCIES[2]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.Mode>@$NETWORK_CONTROLLER_MODE@g" "$deployment_working_file"
}

function print_logs() {
    local ret=$1
    local test_end_time=$2
    local elapsed_seconds=$3

    elapsed_time="$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$elapsed_seconds")"
    print_highlighted_message "Test completed at $test_end_time, took $elapsed_time."

    if (( ret < 1 )); then
        return;
    fi

    print_highlighted_message 'Print logs'
    print_highlighted_message 'LOGS FROM IOTEDGED'
    journalctl -u iotedge -u docker --since "$test_start_time" --no-pager || true

    print_highlighted_message 'EDGE AGENT LOGS'
    docker logs edgeAgent || true

    print_highlighted_message 'EDGE HUB LOGS'
    docker logs edgeHub || true
    
    print_highlighted_message 'LoadGen1 LOGS'
    docker logs loadGen1 || true
    
    print_highlighted_message 'LoadGen2 LOGS'
    docker logs loadGen2 || true
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
            IOTHUB_CONNECTION_STRING="$arg"
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
            NETWORK_CONTROLLER_MODE="$arg"
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
                '-networkControllerMode' ) saveNextArg=14;;
                '-logAnalyticsWorkspaceId' ) saveNextArg=15;;
                '-logAnalyticsSharedKey' ) saveNextArg=16;;
                '-logAnalyticsLogType' ) saveNextArg=17;;
                '-verificationDelay' ) saveNextArg=18;;
                '-upstreamProtocol' ) saveNextArg=19;;

                '-cleanAll' ) CLEAN_ALL=1;;
                * ) usage;;
            esac
        fi
    done

    # Required parameters
    [[ -z "$RELEASE_LABEL" ]] && { print_error 'Release label is required.'; exit 1; }
    [[ -z "$ARTIFACT_IMAGE_BUILD_NUMBER" ]] && { print_error 'Artifact image build number is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_USERNAME" ]] && { print_error 'Container registry username is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_PASSWORD" ]] && { print_error 'Container registry password is required'; exit 1; }
    [[ -z "$IOTHUB_CONNECTION_STRING" ]] && { print_error 'IoT hub connection string is required'; exit 1; }
    [[ -z "$LOG_ANALYTICS_WORKSPACEID" ]] && { print_error 'Log analytics workspace id is required'; exit 1; }
    [[ -z "$LOG_ANALYTICS_SHAREDKEY" ]] && { print_error 'Log analytics shared key is required'; exit 1; }

    echo 'Required parameters are provided'
}

function run_connectivity_test() {
    print_highlighted_message "Run connectivity test for $image_architecture_label"

    local funcRet=0
    test_setup && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-connect"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run connectivity test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        --leave-running=All \
        -l "$deployment_working_file" \
        --runtime-log-level "Debug" \
        --no-verify && funcRet=$? || funcRet=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $funcRet "$test_end_time" $elapsed_seconds

    return $funcRet
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

    if (( error > 0 )); then
        exit 1
    fi
}

function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ''
    echo 'options'
    echo ' -testDir                        Path of E2E test directory which contains artifacts and certs folders; defaul to current directory.'
    echo ' -releaseLabel                   Release label is used as part of Edge device id to make it unique.'
    echo ' -artifactImageBuildNumber       Artifact image build number is used to construct path of docker images, pulling from docker registry. E.g. 20190101.1.'
    echo " -containerRegistry              Host address of container registry."
    echo " -containerRegistryUsername      Username of container registry."
    echo ' -containerRegistryPassword      Password of given username for container registory.'
    echo ' -iotHubConnectionString         IoT hub connection string for creating edge device.'
    echo ' -eventHubConnectionString       Event hub connection string for receive D2C messages.'
    echo ' -eventHubConsumerGroupId        Event hub consumer group for receive D2C messages.'
    echo ' -testDuration                   Connectivity test duration'
    echo ' -testStartDelay                 Connectivity test start after delay'
    echo ' -loadGenMessageFrequency        Message frequency sent by load gen' 
    echo ' -networkControllerFrequency     Frequency for controlling the network with offlineFrequence, onlineFrequence, runsCount. Example "00:05:00 00:05:00 6"' 
    echo ' -networkControllerMode          OfflineNetworkInterface, OfflineTrafficController or SatelliteTrafficController' 
    echo ' -logAnalyticsWorkspaceId        Log Analytics Workspace Id'
    echo ' -logAnalyticsSharedKey          Log Analytics shared key'
    echo ' -logAnalyticsLogType            Log Analytics log type'
    echo ' -verificationDelay              Delay before starting the verification after test finished'
    echo ' -upstreamProtocol               Upstream protocol used to connect to IoT Hub'

    echo ' -cleanAll                       Do docker prune for containers, logs and volumes.'
    exit 1;
}

process_args "$@"

CONTAINER_REGISTRY="${CONTAINER_REGISTRY:-edgebuilds.azurecr.io}"
E2E_TEST_DIR="${E2E_TEST_DIR:-$(pwd)}"
TEST_DURATION="${TEST_DURATION:-01:00:00}"
EVENT_HUB_CONSUMER_GROUP_ID=${EVENT_HUB_CONSUMER_GROUP_ID:-\$Default}
LOADGEN_MESSAGE_FREQUENCY="${LOADGEN_MESSAGE_FREQUENCY:-00:00:01}"
NETWORK_CONTROLLER_FREQUENCIES=${NETWORK_CONTROLLER_FREQUENCIES:(null)}
NETWORK_CONTROLLER_MODE=${NETWORK_CONTROLLER_MODE:-OfflineTrafficController}
TEST_START_DELAY="${TEST_START_DELAY:-00:02:00}"
LOG_ANALYTICS_LOGTYPE="${LOG_ANALYTICS_LOGTYPE:-connectivity}"
VERIFICATION_DELAY="${VERIFICATION_DELAY:-00:15:00}"
UPSTREAM_PROTOCOL="${UPSTREAM_PROTOCOL:-Amqp}"

working_folder="$E2E_TEST_DIR/working"
quickstart_working_folder="$working_folder/quickstart"
get_image_architecture_label
optimize_for_performance=true
if [ "$image_architecture_label" = 'arm32v7' ] ||
   [ "$image_architecture_label" = 'arm64v8' ]; then
    optimize_for_performance=false
fi

iotedged_artifact_folder="$(get_iotedged_artifact_folder $E2E_TEST_DIR)"
iotedge_quickstart_artifact_file="$(get_iotedge_quickstart_artifact_file $E2E_TEST_DIR)"
connectivity_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/connectivity_deployment.template.json"
deployment_working_file="$working_folder/deployment.json"

testRet=0
run_connectivity_test && testRet=$? || testRet=$?
echo "Test exit with result code $testRet"
exit $testRet