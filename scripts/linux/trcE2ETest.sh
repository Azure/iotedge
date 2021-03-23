#!/bin/bash

###############################################################################
# This script is used to run Connectivity test for Linux.
###############################################################################
set -eo pipefail

function usage() {
    echo "trcE2ETest.sh [options]"
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
    echo ' -twinUpdateSize                          Specifies the char count (i.e. size) of each twin update.'
    echo ' -twinUpdateFrequency                     Frequency to make twin updates. This should be specified in DateTime format.'
    echo ' -edgeHubRestartFailureTolerance          Specifies how close to an edgehub restart desired property callback tests will be ignored. This should be specified in DateTime format. Default is 00:01:00'
    echo " -testName                                Name of test to run. Either 'LongHaul' or 'Connectivity'"
    echo ' -connectManagementUri                    Customize connect management socket'
    echo ' -connectWorkloadUri                      Customize connect workload socket'
    echo ' -listenManagementUri                     Customize listen management socket'
    echo ' -listenWorkloadUri                       Customize listen workload socket'
    echo ' -desiredModulesToRestartCSV              CSV string of module names for long haul specifying what modules to restart. If specified, then "restartIntervalInMins" must be specified as well.'
    echo ' -restartIntervalInMins                   Value for long haul specifying how often a random module will restart. If specified, then "desiredModulesToRestartCSV" must be specified as well.'
    echo ' -sendReportFrequency                     Value for long haul specifying how often TRC will send reports to LogAnalytics.'
    echo " -testMode                                Test mode for TestResultCoordinator to start up with correct settings. Value is either 'LongHaul' or 'Connectivity'."
    echo " -repoPath                                Path of the checked-out iotedge repository for getting the deployment file."
    echo ' -cleanAll                                Do docker prune for containers, logs and volumes.'
    exit 1;
}

function print_error() {
    local message=$1
    local red='\033[0;31m'
    local color_reset='\033[0m'
    echo -e "${red}$message${color_reset}"
}

function print_highlighted_message() {
    local message=$1
    local cyan='\033[0;36m'
    local color_reset='\033[0m'
    echo -e "${cyan}$message${color_reset}"
}

function get_image_architecture_label() {
    local arch
    arch="$(uname -m)"

    case "$arch" in
        'x86_64' ) echo 'amd64';;
        'armv7l' ) echo 'arm32v7';;
        'aarch64' ) echo 'arm64v8';;
        *) print_error "Unsupported OS architecture: $arch"; exit 1;;
    esac
}

function get_artifact_file() {
    local testDir=$1
    local fileType=$2

    local filter
    case "$fileType" in
        'aziot_edge' ) filter='aziot-edge_*.deb';;
        'aziot_is' ) filter='aziot-identity-service_*.deb';;
        'quickstart' ) filter='core-linux/IotEdgeQuickstart.linux*.tar.gz';;
        *) print_error "Unknown file type: $fileType"; exit 1;;
    esac

    local path
    local path_count
    # shellcheck disable=SC2086
    path=$(ls "$testDir/artifacts/"$filter)
    path_count="$(echo "$path" | wc -w)"

    if [ "$path_count"  -eq 0 ]; then
        print_error "No files for $fileType found."
        exit 1
    fi

    if [ "$path_count" -ne 1 ]; then
        print_error "Multiple files for $fileType found."
        exit 1
    fi

    printf "$path"
}

function is_cancel_build_requested() {
    local accessToken=$1
    local buildId=$2

    if [[ ( -z "$accessToken" ) || ( -z "$buildId" ) ]]; then
        printf 0
    fi

    local output1
    local output2
    output1=$(curl -s -u :"$accessToken" --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId?api-version=5.1" | grep -oe '"status":"cancel')
    output2=$(curl -s -u :"$accessToken" --request GET "https://dev.azure.com/msazure/one/_apis/build/builds/$buildId/Timeline?api-version=5.1" | grep -oe '"result":"canceled"')

    if [[ -z "$output1" && -z "$output2" ]]; then
        printf 0
    else
        printf 1
    fi
}

function stop_aziot_edge() {
    echo 'Stop IoT Edge services'
    systemctl stop aziot-keyd aziot-certd aziot-identityd aziot-edged || true
}

function parse_result() {
    found_test_passed="$(docker logs testResultCoordinator 2>&1 | sed -n '/Test summary/,/"TestResultReports"/p' | grep '"IsPassed": true')"

    if [[ -z "$found_test_passed" ]]; then
        echo 0
    else
        echo 1
    fi
}

function prepare_test_from_artifacts() {
    print_highlighted_message 'Prepare test from artifacts'

    echo 'Clean working folder'
    rm -rf "$working_folder"
    mkdir -p "$working_folder"

    echo 'Extract quickstart to working folder'
    mkdir -p "$quickstart_working_folder"
    tar -C "$quickstart_working_folder" -xzf "$(get_artifact_file "$E2E_TEST_DIR" quickstart)"

    echo "Copy deployment artifact $DEPLOYMENT_FILE_NAME to $deployment_working_file"
    cp "$REPO_PATH/e2e_deployment_files/$DEPLOYMENT_FILE_NAME" "$deployment_working_file"

    sed -i -e "s@<Architecture>@$image_architecture_label@g" "$deployment_working_file"
    sed -i -e "s/<Build.BuildNumber>/$ARTIFACT_IMAGE_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s/<EdgeRuntime.BuildNumber>/$EDGE_RUNTIME_BUILD_NUMBER/g" "$deployment_working_file"
    sed -i -e "s@<Container_Registry>@$CONTAINER_REGISTRY@g" "$deployment_working_file"
    sed -i -e "s@<CR.Username>@$CONTAINER_REGISTRY_USERNAME@g" "$deployment_working_file"
    sed -i -e "s@<CR.Password>@$CONTAINER_REGISTRY_PASSWORD@g" "$deployment_working_file"
    sed -i -e "s@<IoTHubConnectionString>@$IOT_HUB_CONNECTION_STRING@g" "$deployment_working_file"
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

    sed -i -e "s@<TestResultCoordinator.ConsumerGroupId>@$EVENT_HUB_CONSUMER_GROUP_ID@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.EventHubConnectionString>@$EVENTHUB_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.VerificationDelay>@$VERIFICATION_DELAY@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.OptimizeForPerformance>@$optimize_for_performance@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.LogAnalyticsLogType>@$LOG_ANALYTICS_LOGTYPE@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.logUploadEnabled>@$log_upload_enabled@g" "$deployment_working_file"
    sed -i -e "s@<TestResultCoordinator.StorageAccountConnectionString>@$STORAGE_ACCOUNT_CONNECTION_STRING@g" "$deployment_working_file"
    sed -i -e "s@<TestInfo>@$TEST_INFO@g" "$deployment_working_file"

    sed -i -e "s@<NetworkController.RunProfile>@$NETWORK_CONTROLLER_RUNPROFILE@g" "$deployment_working_file"

    sed -i -e "s@<MetricsCollector.MetricsEndpointsCSV>@$METRICS_ENDPOINTS_CSV@g" "$deployment_working_file"
    sed -i -e "s@<MetricsCollector.ScrapeFrequencyInSecs>@$METRICS_SCRAPE_FREQUENCY_IN_SECS@g" "$deployment_working_file"
    sed -i -e "s@<MetricsCollector.UploadTarget>@$METRICS_UPLOAD_TARGET@g" "$deployment_working_file"

    sed -i -e "s@<TwinUpdateSize>@$TWIN_UPDATE_SIZE@g" "$deployment_working_file"
    sed -i -e "s@<TwinUpdateFrequency>@$TWIN_UPDATE_FREQUENCY@g" "$deployment_working_file"
    sed -i -e "s@<EdgeHubRestartFailureTolerance>@$EDGEHUB_RESTART_FAILURE_TOLERANCE@g" "$deployment_working_file"

    sed -i -e "s@<NetworkController.OfflineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[0]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.OnlineFrequency0>@${NETWORK_CONTROLLER_FREQUENCIES[1]}@g" "$deployment_working_file"
    sed -i -e "s@<NetworkController.RunsCount0>@${NETWORK_CONTROLLER_FREQUENCIES[2]}@g" "$deployment_working_file"

    sed -i -e "s@<TestMode>@$TEST_MODE@g" "$deployment_working_file"

    if [[ "${TEST_NAME,,}" == "${LONGHAUL_TEST_NAME,,}" ]]; then
        sed -i -e "s@<DesiredModulesToRestartCSV>@$DESIRED_MODULES_TO_RESTART_CSV@g" "$deployment_working_file"
        sed -i -e "s@<RestartIntervalInMins>@$RESTART_INTERVAL_IN_MINS@g" "$deployment_working_file"
        sed -i -e "s@<SendReportFrequency>@$SEND_REPORT_FREQUENCY@g" "$deployment_working_file"
        sed -i -e "s@<LogRotationMaxFile>@$log_rotation_max_file@g" "$deployment_working_file"
    fi

    if [[ "${TEST_NAME,,}" == "${CONNECTIVITY_TEST_NAME,,}" ]]; then
        sed -i -e "s@<TestDuration>@$TEST_DURATION@g" "$deployment_working_file"
        sed -i -e "s@<DeploymentTester1.DeploymentUpdatePeriod>@$DEPLOYMENT_TEST_UPDATE_PERIOD@g" "$deployment_working_file"
        sed -i -e "s@<EdgeHubRestartTest.RestartPeriod>@$RESTART_TEST_RESTART_PERIOD@g" "$deployment_working_file"
        sed -i -e "s@<EdgeHubRestartTest.SdkOperationTimeout>@$RESTART_TEST_SDK_OPERATION_TIMEOUT@g" "$deployment_working_file"
    fi
}

function clean_up() {
    print_highlighted_message 'Clean up'

    # TODO: Need to fix this script to deploy correct iotedge artifact.
    # Because it deploys iotedge installed from apt, we need to stop 1.0.10 service.
    echo 'Stop IoT Edge services'
    systemctl stop iotedge.socket iotedge.mgmt.socket || true
    systemctl kill iotedge || true
    systemctl stop iotedge || true

    stop_aziot_edge || true

    echo 'Remove IoT Edge and config files'
    rm -rf /var/lib/aziot/
    rm -rf /var/lib/iotedge/
    rm -rf /etc/aziot/
    rm -rf /etc/systemd/system/aziot-*.service.d/

    if [ "$CLEAN_ALL" = '1' ]; then
        echo 'Prune docker system'
        docker system prune -af --volumes || true

        echo 'Restart docker'
        systemctl restart docker # needed due to https://github.com/moby/moby/issues/23302
    else
        echo 'Remove docker containers'
        docker rm -f "$(docker ps -aq)" || true
    fi
}

function print_deployment_logs() {
    print_highlighted_message 'LOGS FROM AZIOT-EDGED'
    journalctl -u aziot-edged -u aziot-keyd -u aziot-certd -u aziot-identityd --since "$test_start_time" --no-pager || true

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
        elif [ $saveNextArg -eq 36 ]; then
            TWIN_UPDATE_SIZE="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 37 ]; then
            TWIN_UPDATE_FREQUENCY="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 38 ]; then
            EDGEHUB_RESTART_FAILURE_TOLERANCE="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 39 ]; then
            TEST_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 40 ]; then
            CONNECT_MANAGEMENT_URI="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 41 ]; then
            CONNECT_WORKLOAD_URI="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 42 ]; then
            LISTEN_MANAGEMENT_URI="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 43 ]; then
            LISTEN_WORKLOAD_URI="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 44 ]; then
            DESIRED_MODULES_TO_RESTART_CSV="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 45 ]; then
            RESTART_INTERVAL_IN_MINS="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 46 ]; then
            SEND_REPORT_FREQUENCY="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 47 ]; then
            TEST_MODE="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 48 ]; then
            REPO_PATH="$arg"
            saveNextArg=0;
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
                '-twinUpdateSize' ) saveNextArg=36;;
                '-twinUpdateFrequency' ) saveNextArg=37;;
                '-edgeHubRestartFailureTolerance' ) saveNextArg=38;;
                '-testName' ) saveNextArg=39;;
                '-connectManagementUri' ) saveNextArg=40;;
                '-connectWorkloadUri' ) saveNextArg=41;;
                '-listenManagementUri' ) saveNextArg=42;;
                '-listenWorkloadUri' ) saveNextArg=43;;
                '-desiredModulesToRestartCSV' ) saveNextArg=44;;
                '-restartIntervalInMins' ) saveNextArg=45;;
                '-sendReportFrequency' ) saveNextArg=46;;
                '-testMode' ) saveNextArg=47;;
                '-repoPath' ) saveNextArg=48;;
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
    [[ -z "$LOG_ANALYTICS_LOGTYPE" ]] && { print_error 'Log analytics log type is required'; exit 1; }
    [[ -z "$METRICS_ENDPOINTS_CSV" ]] && { print_error 'Metrics endpoints csv is required'; exit 1; }
    [[ -z "$METRICS_SCRAPE_FREQUENCY_IN_SECS" ]] && { print_error 'Metrics scrape frequency is required'; exit 1; }
    [[ -z "$METRICS_UPLOAD_TARGET" ]] && { print_error 'Metrics upload target is required'; exit 1; }
    [[ -z "$STORAGE_ACCOUNT_CONNECTION_STRING" ]] && { print_error 'Storage account connection string is required'; exit 1; }
    [[ -z "$TEST_INFO" ]] && { print_error 'Test info is required'; exit 1; }
    [[ -z "$REPO_PATH" ]] && { print_error 'Repo path is required'; exit 1; }
    [[ (-z "${TEST_NAME,,}") || ("${TEST_NAME,,}" != "${LONGHAUL_TEST_NAME,,}" && "${TEST_NAME,,}" != "${CONNECTIVITY_TEST_NAME,,}") ]] && { print_error 'Invalid test name'; exit 1; }

    echo 'Required parameters are provided'
}

function validate_test_parameters() {
    print_highlighted_message "Validate test parameters"
    echo "aziot_edge: $(get_artifact_file $E2E_TEST_DIR aziot_edge)"
    echo "aziot_identity_service: $(get_artifact_file $E2E_TEST_DIR aziot_is)"
    echo "IotEdgeQuickstart: $(get_artifact_file $E2E_TEST_DIR quickstart)"

    if [[ -z "$TEST_INFO" ]]; then
        print_error "Required test info."
        ((error++))
    fi

    if (( error > 0 )); then
        exit 1
    fi
}

function test_setup() {
    local funcRet=0

    validate_test_parameters && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    clean_up && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    prepare_test_from_artifacts && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    print_highlighted_message 'Create IoT Edge service config'
    mkdir -p /etc/systemd/system/aziot-edged.service.d/
    echo -e '[Service]\nEnvironment=IOTEDGE_LOG=edgelet=debug' > /etc/systemd/system/aziot-edged.service.d/override.conf

    if [ $funcRet -ne 0 ]; then return $funcRet; fi
}

function run_connectivity_test() {
    print_highlighted_message "Run connectivity test for $image_architecture_label"

    local funcRet=0
    test_setup && funcRet=$? || funcRet=$?
    if [ $funcRet -ne 0 ]; then return $funcRet; fi

    local hash
    hash=$(tr -dc 'a-zA-Z0-9' < /dev/urandom | head -c 8)
    local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-connect-$hash"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run connectivity test with -d '$device_id' started at $test_start_time"

    SECONDS=0

    NESTED_EDGE_TEST=$(printenv E2E_nestedEdgeTest)

    DEVICE_CA_CERT=$(printenv E2E_deviceCaCert)
    DEVICE_CA_PRIVATE_KEY=$(printenv E2E_deviceCaPrivateKey)
    TRUSTED_CA_CERTS=$(printenv E2E_trustedCaCerts)
    echo "Device CA cert=$DEVICE_CA_CERT"
    echo "Device CA private key=$DEVICE_CA_PRIVATE_KEY"
    echo "Trusted CA certs=$TRUSTED_CA_CERTS"

    if [[ ! -z "$NESTED_EDGE_TEST" ]]; then
        PARENT_HOSTNAME=$(printenv E2E_parentHostname)
        PARENT_EDGE_DEVICE=$(printenv E2E_parentEdgeDevice)

        echo "Running with nested Edge."
        echo "Parent hostname=$PARENT_HOSTNAME"
        echo "Parent Edge Device=$PARENT_EDGE_DEVICE"

        "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$E2E_TEST_DIR/artifacts/" \
        -c "$IOT_HUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        --parent-hostname "$PARENT_HOSTNAME" \
        --parent-edge-device "$PARENT_EDGE_DEVICE" \
        --device_ca_cert "$DEVICE_CA_CERT" \
        --device_ca_pk "$DEVICE_CA_PRIVATE_KEY" \
        --trusted_ca_certs "$TRUSTED_CA_CERTS" \
        --initialize-with-agent-artifact true \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        --leave-running=All \
        -l "$deployment_working_file" \
        --runtime-log-level "$TEST_RUNTIME_LOG_LEVEL" \
        --no-verify \
        --overwrite-packages && funcRet=$? || funcRet=$?
    else
        "$quickstart_working_folder/IotEdgeQuickstart" \
            -d "$device_id" \
            -a "$E2E_TEST_DIR/artifacts/" \
            -c "$IOT_HUB_CONNECTION_STRING" \
            -e "$EVENTHUB_CONNECTION_STRING" \
            -r "$CONTAINER_REGISTRY" \
            -u "$CONTAINER_REGISTRY_USERNAME" \
            -p "$CONTAINER_REGISTRY_PASSWORD" \
            -n "$(hostname)" \
            -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
            --leave-running=All \
            -l "$deployment_working_file" \
            --device_ca_cert "$DEVICE_CA_CERT" \
            --device_ca_pk "$DEVICE_CA_PRIVATE_KEY" \
            --trusted_ca_certs "$TRUSTED_CA_CERTS" \
            --runtime-log-level "$TEST_RUNTIME_LOG_LEVEL" \
            --no-verify \
            --overwrite-packages && funcRet=$? || funcRet=$?
    fi

    local elapsed_time
    elapsed_time="$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$SECONDS")"
    print_highlighted_message "Deploy connectivity test with -d '$device_id' completed in $elapsed_time"

    if [ $funcRet -ne 0 ]; then
        print_error "Deploy connectivity test failed."
        print_deployment_logs
        return $funcRet
    fi

    print_highlighted_message "Deploy connectivity test succeeded."

    # Delay for (buffer for module download + test start delay + test duration + verification delay + report generation)
    local module_download_buffer=300
    local time_for_test_to_complete=$((module_download_buffer + \
                                    $(echo $TEST_START_DELAY | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $TEST_DURATION | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $VERIFICATION_DELAY | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }') + \
                                    $(echo $TIME_FOR_REPORT_GENERATION | awk -F: '{ print ($1 * 3600) + ($2 * 60) + $3 }')))
    echo "test start delay=$TEST_START_DELAY"
    echo "test duration=$TEST_DURATION"
    echo "verification delay=$VERIFICATION_DELAY"
    echo "time for report generation=$TIME_FOR_REPORT_GENERATION"
    echo "time for test to complete in seconds=$time_for_test_to_complete"

    if [ "$WAIT_FOR_TEST_COMPLETE" -eq 1 ]; then
        local sleep_frequency_secs=60
        local total_wait=0

        while [ $total_wait -lt $time_for_test_to_complete ]
        do
            local is_build_canceled
            is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)

            if [ "$is_build_canceled" -eq '1' ]; then
                print_highlighted_message "build is canceled."
                stop_aziot_edge || true
                return 3
            fi

            sleep "${sleep_frequency_secs}s"
            total_wait=$((total_wait+sleep_frequency_secs))
            echo "total wait time=$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$total_wait")"
        done

        test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
        print_highlighted_message "Connectivity test should be completed at $test_end_time."
        testExitCode=$(parse_result)
        if [[ "$(parse_result)" -eq '0' ]]; then
            testExitCode=1
        else
            testExitCode=0
        fi

        print_test_run_logs $testExitCode

        # stop IoT Edge service after test complete to prevent sending metrics
        stop_aziot_edge
    fi

    return $testExitCode
}

function run_longhaul_test() {
    print_highlighted_message "Run Long Haul test for $image_architecture_label"
    test_setup

	NESTED_EDGE_TEST=$(printenv E2E_nestedEdgeTest)

	local hash
	hash=$(tr -dc 'a-zA-Z0-9' < /dev/urandom | head -c 8)
	local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-longhaul-$hash"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run Long Haul test with -d '$device_id' started at $test_start_time"

    SECONDS=0

    local ret=0

    DEVICE_CA_CERT=$(printenv E2E_deviceCaCert)
    DEVICE_CA_PRIVATE_KEY=$(printenv E2E_deviceCaPrivateKey)
    TRUSTED_CA_CERTS=$(printenv E2E_trustedCaCerts)
    echo "Device CA cert=$DEVICE_CA_CERT"
    echo "Device CA private key=$DEVICE_CA_PRIVATE_KEY"
    echo "Trusted CA certs=$TRUSTED_CA_CERTS"

    if [[ -z "$BYPASS_EDGE_INSTALLATION" ]]; then
        BYPASS_EDGE_INSTALLATION=--overwrite-packages
    fi

    if [[ ! -z "$NESTED_EDGE_TEST" ]]; then
        HOSTNAME=$(printenv E2E_hostname)
        PARENT_HOSTNAME=$(printenv E2E_parentHostname)
        PARENT_EDGE_DEVICE=$(printenv E2E_parentEdgeDevice)

        echo "Running with nested Edge."
        echo "HostName=$HOSTNAME"
        echo "ParentHostName=$PARENT_HOSTNAME"
        echo "ParentEdgeDevice=$PARENT_EDGE_DEVICE"

        "$quickstart_working_folder/IotEdgeQuickstart" \
            -d "$device_id" \
            -a "$E2E_TEST_DIR/artifacts/" \
            -c "$IOT_HUB_CONNECTION_STRING" \
            -e "$EVENTHUB_CONNECTION_STRING" \
            -r "$CONTAINER_REGISTRY" \
            -u "$CONTAINER_REGISTRY_USERNAME" \
            -p "$CONTAINER_REGISTRY_PASSWORD" \
            -n "$HOSTNAME" \
            --parent-hostname "$PARENT_HOSTNAME" \
            --parent-edge-device "$PARENT_EDGE_DEVICE" \
            --device_ca_cert "$DEVICE_CA_CERT" \
            --device_ca_pk "$DEVICE_CA_PRIVATE_KEY" \
            --trusted_ca_certs "$TRUSTED_CA_CERTS" \
            -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
            --initialize-with-agent-artifact "true" \
            --leave-running=All \
            -l "$deployment_working_file" \
            --runtime-log-level "$TEST_RUNTIME_LOG_LEVEL" \
            --use-connect-management-uri="$CONNECT_MANAGEMENT_URI" \
            --use-connect-workload-uri="$CONNECT_WORKLOAD_URI" \
            --use-listen-management-uri="$LISTEN_MANAGEMENT_URI" \
            --use-listen-workload-uri="$LISTEN_WORKLOAD_URI" \
            $BYPASS_EDGE_INSTALLATION \
            --no-verify && ret=$? || ret=$?
    else
        "$quickstart_working_folder/IotEdgeQuickstart" \
            -d "$device_id" \
            -a "$E2E_TEST_DIR/artifacts/" \
            -c "$IOT_HUB_CONNECTION_STRING" \
            -e "$EVENTHUB_CONNECTION_STRING" \
            -r "$CONTAINER_REGISTRY" \
            -u "$CONTAINER_REGISTRY_USERNAME" \
            -p "$CONTAINER_REGISTRY_PASSWORD" \
            -n "$(hostname)" \
            -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
            --initialize-with-agent-artifact "true" \
            --leave-running=All \
            -l "$deployment_working_file" \
            --runtime-log-level "$TEST_RUNTIME_LOG_LEVEL" \
            --use-connect-management-uri="$CONNECT_MANAGEMENT_URI" \
            --use-connect-workload-uri="$CONNECT_WORKLOAD_URI" \
            --use-listen-management-uri="$LISTEN_MANAGEMENT_URI" \
            --use-listen-workload-uri="$LISTEN_WORKLOAD_URI" \
            --device_ca_cert "$DEVICE_CA_CERT" \
            --device_ca_pk "$DEVICE_CA_PRIVATE_KEY" \
            --trusted_ca_certs "$TRUSTED_CA_CERTS" \
            $BYPASS_EDGE_INSTALLATION \
            --no-verify && ret=$? || ret=$?
    fi

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"

    if [ $ret -ne 0 ]; then
        elapsed_time="$(TZ=UTC0 printf '%(%H:%M:%S)T\n' "$elapsed_seconds")"
        print_highlighted_message "Test completed at $test_end_time, took $elapsed_time."

        print_error "Deploy longhaul test failed."

        print_deployment_logs
    fi

    return $ret
}

LONGHAUL_TEST_NAME="LongHaul"
CONNECTIVITY_TEST_NAME="Connectivity"

is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)
if [ "$is_build_canceled" -eq '1' ]; then
    print_highlighted_message "build is canceled."
    exit 3
fi

process_args "$@"

CONTAINER_REGISTRY="${CONTAINER_REGISTRY:-edgebuilds.azurecr.io}"
E2E_TEST_DIR="${E2E_TEST_DIR:-$(pwd)}"
DEPLOYMENT_TEST_UPDATE_PERIOD="${DEPLOYMENT_TEST_UPDATE_PERIOD:-00:03:00}"
EVENT_HUB_CONSUMER_GROUP_ID=${EVENT_HUB_CONSUMER_GROUP_ID:-\$Default}
EDGE_RUNTIME_BUILD_NUMBER=${EDGE_RUNTIME_BUILD_NUMBER:-$ARTIFACT_IMAGE_BUILD_NUMBER}
LOADGEN_MESSAGE_FREQUENCY="${LOADGEN_MESSAGE_FREQUENCY:-00:00:01}"
TEST_START_DELAY="${TEST_START_DELAY:-00:02:00}"
VERIFICATION_DELAY="${VERIFICATION_DELAY:-00:15:00}"
UPSTREAM_PROTOCOL="${UPSTREAM_PROTOCOL:-Amqp}"
TIME_FOR_REPORT_GENERATION="${TIME_FOR_REPORT_GENERATION:-00:10:00}"
TWIN_UPDATE_SIZE="${TWIN_UPDATE_SIZE:-1}"
TWIN_UPDATE_FREQUENCY="${TWIN_UPDATE_FREQUENCY:-00:00:15}"
EDGEHUB_RESTART_FAILURE_TOLERANCE="${EDGEHUB_RESTART_FAILURE_TOLERANCE:-00:01:00}"
NETWORK_CONTROLLER_FREQUENCIES=${NETWORK_CONTROLLER_FREQUENCIES:(null)}

working_folder="$E2E_TEST_DIR/working"
quickstart_working_folder="$working_folder/quickstart"
image_architecture_label=$(get_image_architecture_label)
optimize_for_performance=true
log_upload_enabled=true
log_rotation_max_file="125"
if [ "$image_architecture_label" = 'arm32v7' ] ||
    [ "$image_architecture_label" = 'arm64v8' ]; then
    optimize_for_performance=false
    log_upload_enabled=false
    log_rotation_max_file="7"
fi

deployment_working_file="$working_folder/deployment.json"

tracking_id=$(cat /proc/sys/kernel/random/uuid)
TEST_INFO="$TEST_INFO,TestId=$tracking_id"
TEST_INFO="$TEST_INFO,UpstreamProtocol=$UPSTREAM_PROTOCOL"
TEST_INFO="$TEST_INFO,NetworkControllerOfflineFrequency=${NETWORK_CONTROLLER_FREQUENCIES[0]}"
TEST_INFO="$TEST_INFO,NetworkControllerOnlineFrequency=${NETWORK_CONTROLLER_FREQUENCIES[1]}"
TEST_INFO="$TEST_INFO,NetworkControllerRunsCount=${NETWORK_CONTROLLER_FREQUENCIES[2]}"

testRet=0
if [[ "${TEST_NAME,,}" == "${LONGHAUL_TEST_NAME,,}" ]]; then
    DESIRED_MODULES_TO_RESTART_CSV="${DESIRED_MODULES_TO_RESTART_CSV:-,}"
    RESTART_INTERVAL_IN_MINS="${RESTART_INTERVAL_IN_MINS:-240}"
    NETWORK_CONTROLLER_RUNPROFILE=${NETWORK_CONTROLLER_RUNPROFILE:-Online}

    run_longhaul_test && testRet=$? || testRet=$?
elif [[ "${TEST_NAME,,}" == "${CONNECTIVITY_TEST_NAME,,}" ]]; then
    NETWORK_CONTROLLER_RUNPROFILE=${NETWORK_CONTROLLER_RUNPROFILE:-Offline}
    TEST_DURATION="${TEST_DURATION:-01:00:00}"

    TEST_INFO="$TEST_INFO,TestDuration=${TEST_DURATION}"

    is_build_canceled=$(is_cancel_build_requested $DEVOPS_ACCESS_TOKEN $DEVOPS_BUILDID)
    if [ "$is_build_canceled" -eq '1' ]; then
        print_highlighted_message "build is canceled."
        exit 3
    fi

    run_connectivity_test && testRet=$? || testRet=$?
fi

echo "Test exit with result code $testRet"
exit $testRet
