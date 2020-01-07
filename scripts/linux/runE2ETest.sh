#!/bin/bash

###############################################################################
# This script is used to streamline running E2E tests for Linux.
###############################################################################
set -e

function clean_up() {
    print_highlighted_message 'Clean up'

    echo 'Stop IoT Edge services'
    systemctl stop iotedge.socket iotedge.mgmt.socket || true
    systemctl kill iotedge || true
    systemctl stop iotedge || true

    echo 'Remove IoT Edge and config file'
    apt-get purge libiothsm-std --yes || true
    rm -rf /var/lib/iotedge/
    rm -rf /var/run/iotedge/
    rm -rf /etc/iotedge/config.yaml

    if [ "$CLEAN_ALL" = '1' ]; then
        echo 'Prune docker system'
        docker system prune -af --volumes || true
    else
        echo 'Remove docker containers'
        docker rm -f $(docker ps -aq) || true
    fi
}

function create_iotedge_service_config {
    print_highlighted_message 'Create IoT Edge service config'
    mkdir /etc/systemd/system/iotedge.service.d/ || true
    bash -c "echo '[Service]
Environment=IOTEDGE_LOG=edgelet=debug' > /etc/systemd/system/iotedge.service.d/override.conf"
}

function set_certificate_generation_tools_dir() {
    if [[ -z $CERT_SCRIPT_DIR ]]; then
        CERT_SCRIPT_DIR="$E2E_TEST_DIR/artifacts/core-linux/CACertificates"
    fi
}

function get_image_architecture_label() {
    local arch
    arch="$(uname -m)"

    case "$arch" in
        'x86_64' ) image_architecture_label='amd64';;
        'armv7l' ) image_architecture_label='arm32v7';;
        'aarch64' ) image_architecture_label='arm64v8';;
        *) print_error "Unsupported OS architecture: $arch"; exit 1;;
    esac
}

function get_iotedge_quickstart_artifact_file() {
    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$E2E_TEST_DIR/artifacts/core-linux/IotEdgeQuickstart.linux-x64.tar.gz"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$E2E_TEST_DIR/artifacts/core-linux/IotEdgeQuickstart.linux-arm64.tar.gz"
    else
        path="$E2E_TEST_DIR/artifacts/core-linux/IotEdgeQuickstart.linux-arm.tar.gz"
    fi

    echo "$path"
}

function get_iotedged_artifact_folder() {
    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$E2E_TEST_DIR/artifacts/iotedged-ubuntu16.04-amd64"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$E2E_TEST_DIR/artifacts/iotedged-ubuntu18.04-aarch64"
    else
        path="$E2E_TEST_DIR/artifacts/iotedged-debian9-arm32v7"
    fi

    echo "$path"
}

function get_leafdevice_artifact_file() {
    local path
    if [ "$image_architecture_label" = 'amd64' ]; then
        path="$E2E_TEST_DIR/artifacts/core-linux/LeafDevice.linux-x64.tar.gz"
    elif [ "$image_architecture_label" = 'arm64v8' ]; then
        path="$E2E_TEST_DIR/artifacts/core-linux/LeafDevice.linux-arm64.tar.gz"
    else
        path="$E2E_TEST_DIR/artifacts/core-linux/LeafDevice.linux-arm.tar.gz"
    fi

    echo "$path"
}

function get_long_haul_deployment_artifact_file() {
    local path
    path="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/long_haul_deployment.template.json"

    echo "$path"
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

    if [[ "${TEST_NAME,,}" == 'quickstartcerts' ]]; then
        echo 'Extract leaf device to working folder'
        mkdir -p "$leafdevice_working_folder"
        tar -C "$leafdevice_working_folder" -xzf "$leafdevice_artifact_file"
    fi

    if [[ "${TEST_NAME,,}" == directmethod* ]] ||
       [[ "${TEST_NAME,,}" == 'longhaul' ]] ||
       [[ "${TEST_NAME,,}" == 'stress' ]] ||
       [[ "${TEST_NAME,,}" == 'tempfilter' ]] ||
       [[ "${TEST_NAME,,}" == 'tempfilterfunctions' ]]; then
        case "${TEST_NAME,,}" in
            directmethod*)
                echo "Copy deployment file from $dm_module_to_module_deployment_artifact_file"
                cp "$dm_module_to_module_deployment_artifact_file" "$deployment_working_file"

                case "${TEST_NAME,,}" in
                    'directmethodamqp')
                        sed -i -e "s@<UpstreamProtocol>@Amqp@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Amqp_Tcp_Only@g" "$deployment_working_file";;
                    'directmethodamqpmqtt')
                        sed -i -e "s@<UpstreamProtocol>@Amqp@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Mqtt_Tcp_Only@g" "$deployment_working_file";;
                    'directmethodamqpws')
                        sed -i -e "s@<UpstreamProtocol>@Amqpws@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Amqp_WebSocket_Only@g" "$deployment_working_file";;
                    'directmethodmqtt')
                        sed -i -e "s@<UpstreamProtocol>@Mqtt@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Mqtt_Tcp_Only@g" "$deployment_working_file";;
                    'directmethodmqttamqp')
                        sed -i -e "s@<UpstreamProtocol>@Mqtt@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Amqp_Tcp_Only@g" "$deployment_working_file";;
                    'directmethodmqttws')
                        sed -i -e "s@<UpstreamProtocol>@MqttWs@g" "$deployment_working_file"
                        sed -i -e "s@<ClientTransportType>@Mqtt_WebSocket_Only@g" "$deployment_working_file";;
                esac;;
            'longhaul' | 'stress')
                if [[ "${TEST_NAME,,}" == 'longhaul' ]]; then
                    echo "Copy deployment file from $long_haul_deployment_artifact_file"
                    cp "$long_haul_deployment_artifact_file" "$deployment_working_file"
                    sed -i -e "s@<DesiredModulesToRestartCSV>@$DESIRED_MODULES_TO_RESTART_CSV@g" "$deployment_working_file"
                    sed -i -e "s@<RestartIntervalInMins>@$RESTART_INTERVAL_IN_MINS@g" "$deployment_working_file"
                else
                    echo "Copy deployment file from $stress_deployment_artifact_file"
                    cp "$stress_deployment_artifact_file" "$deployment_working_file"
                    sed -i -e "s@<TransportType1>@$TRANSPORT_TYPE_1@g" "$deployment_working_file"
                    sed -i -e "s@<TransportType2>@$TRANSPORT_TYPE_2@g" "$deployment_working_file"
                    sed -i -e "s@<TransportType3>@$TRANSPORT_TYPE_3@g" "$deployment_working_file"
                    sed -i -e "s@<TransportType4>@$TRANSPORT_TYPE_4@g" "$deployment_working_file"
                    sed -i -e "s@<amqpSettings__enabled>@$AMQP_SETTINGS_ENABLED@g" "$deployment_working_file"
                    sed -i -e "s@<mqttSettings__enabled>@$MQTT_SETTINGS_ENABLED@g" "$deployment_working_file"
                fi

                local escapedSnitchAlertUrl
                local escapedBuildId
                sed -i -e "s@<Analyzer.ConsumerGroupId>@$EVENT_HUB_CONSUMER_GROUP_ID@g" "$deployment_working_file"
                sed -i -e "s@<Analyzer.EventHubConnectionString>@$EVENTHUB_CONNECTION_STRING@g" "$deployment_working_file"
                sed -i -e "s@<Analyzer.LogAnalyticsEnabled>@$LOG_ANALYTICS_ENABLED@g" "$deployment_working_file"
                sed -i -e "s@<Analyzer.LogAnalyticsLogType>@$LOG_ANALYTICS_LOG_TYPE@g" "$deployment_working_file"
                sed -i -e "s@<LoadGen.MessageFrequency>@$LOADGEN_MESSAGE_FREQUENCY@g" "$deployment_working_file"
                sed -i -e "s@<LogAnalyticsSharedKey>@$LOG_ANALYTICS_SHARED_KEY@g" "$deployment_working_file"
                sed -i -e "s@<LogAnalyticsWorkspaceId>@$LOG_ANALYTICS_WORKSPACE_ID@g" "$deployment_working_file"
                sed -i -e "s@<MetricsCollector.MetricsEndpointsCSV>@$METRICS_ENDPOINTS_CSV@g" "$deployment_working_file"
                sed -i -e "s@<MetricsCollector.ScrapeFrequencyInSecs>@$METRICS_SCRAPE_FREQUENCY_IN_SECS@g" "$deployment_working_file"
                sed -i -e "s@<MetricsCollector.UploadTarget>@$METRICS_UPLOAD_TARGET@g" "$deployment_working_file"
                escapedSnitchAlertUrl="${SNITCH_ALERT_URL//&/\\&}"
                escapedBuildId="${ARTIFACT_IMAGE_BUILD_NUMBER//./}"
                sed -i -e "s@<ServiceClientConnectionString>@$IOTHUB_CONNECTION_STRING@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.AlertUrl>@$escapedSnitchAlertUrl@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.BuildNumber>@$SNITCH_BUILD_NUMBER@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.BuildId>@$RELEASE_LABEL-$image_architecture_label-linux-$escapedBuildId@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.ReportingIntervalInSecs>@$SNITCH_REPORTING_INTERVAL_IN_SECS@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.StorageAccount>@$SNITCH_STORAGE_ACCOUNT@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.StorageMasterKey>@$SNITCH_STORAGE_MASTER_KEY@g" "$deployment_working_file"
                sed -i -e "s@<Snitch.TestDurationInSecs>@$SNITCH_TEST_DURATION_IN_SECS@g" "$deployment_working_file"
                sed -i -e "s@<TwinUpdateSize>@$TWIN_UPDATE_SIZE@g" "$deployment_working_file"
                sed -i -e "s@<TwinUpdateFrequency>@$TWIN_UPDATE_FREQUENCY@g" "$deployment_working_file"
                sed -i -e "s@<TwinUpdateFailureThreshold>@$TWIN_UPDATE_FAILURE_THRESHOLD@g" "$deployment_working_file";;
            'tempfilter')
                echo "Copy deployment file from $module_to_module_deployment_artifact_file"
                cp "$module_to_module_deployment_artifact_file" "$deployment_working_file";;
            'tempfilterfunctions')
                echo "Copy deployment file from $module_to_functions_deployment_artifact_file"
                cp "$module_to_functions_deployment_artifact_file" "$deployment_working_file";;
        esac

        sed -i -e "s@<Architecture>@$image_architecture_label@g" "$deployment_working_file"
        sed -i -e "s/<Build.BuildNumber>/$ARTIFACT_IMAGE_BUILD_NUMBER/g" "$deployment_working_file"
        sed -i -e "s@<CR.Username>@$CONTAINER_REGISTRY_USERNAME@g" "$deployment_working_file"
        sed -i -e "s@<CR.Password>@$CONTAINER_REGISTRY_PASSWORD@g" "$deployment_working_file"
        sed -i -e "s@<Container_Registry>@$CONTAINER_REGISTRY@g" "$deployment_working_file"
    fi
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

    if [[ "${TEST_NAME,,}" == 'tempsensor' ]]; then
        print_highlighted_message 'TEMP SENSOR LOGS'
        docker logs tempSensor || true
    fi

    if [[ "${TEST_NAME,,}" == 'tempfilter' ]]; then
        print_highlighted_message 'TEMP FILTER LOGS'
        docker logs tempFilter || true
    fi

    if [[ "${TEST_NAME,,}" == 'tempfilterfunctions' ]]; then
        print_highlighted_message 'TEMP FILTER FUNCTIONS LOGS'
        docker logs tempFilterFunctions || true
    fi

    if [[ "${TEST_NAME,,}" == directmethod* ]]; then
        print_highlighted_message 'DIRECT MTEHOD SENDER LOGS'
        docker logs DirectMethodSender || true

        print_highlighted_message 'DIRECT MTEHOD RECEIVER LOGS'
        docker logs DirectMethodReceiver || true
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
            TEST_NAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 5 ]; then
            CONTAINER_REGISTRY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 6 ]; then
            CONTAINER_REGISTRY_USERNAME="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 7 ]; then
            CONTAINER_REGISTRY_PASSWORD="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 8 ]; then
            IOTHUB_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 9 ]; then
            EVENTHUB_CONNECTION_STRING="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 10 ]; then
            LOADGEN_MESSAGE_FREQUENCY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 11 ]; then
            SNITCH_ALERT_URL="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 12 ]; then
            SNITCH_BUILD_NUMBER="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 13 ]; then
            SNITCH_REPORTING_INTERVAL_IN_SECS="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 14 ]; then
            SNITCH_STORAGE_ACCOUNT="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 15 ]; then
            SNITCH_STORAGE_MASTER_KEY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 16 ]; then
            SNITCH_TEST_DURATION_IN_SECS="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 17 ]; then
            TRANSPORT_TYPE_1="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 18 ]; then
            TRANSPORT_TYPE_2="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 19 ]; then
            TRANSPORT_TYPE_3="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 20 ]; then
            TRANSPORT_TYPE_4="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 21 ]; then
            AMQP_SETTINGS_ENABLED="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 22 ]; then
            MQTT_SETTINGS_ENABLED="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 23 ]; then
            CERT_SCRIPT_DIR="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 24 ]; then
            ROOT_CA_CERT_PATH="$arg"
            INSTALL_CA_CERT=1
            saveNextArg=0
        elif [ $saveNextArg -eq 25 ]; then
            ROOT_CA_KEY_PATH="$arg"
            INSTALL_CA_CERT=1
            saveNextArg=0
        elif [ $saveNextArg -eq 26 ]; then
            ROOT_CA_PASSWORD="$arg"
            INSTALL_CA_CERT=1
            saveNextArg=0
        elif [ $saveNextArg -eq 27 ]; then
            DPS_SCOPE_ID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 28 ]; then
            DPS_MASTER_SYMMETRIC_KEY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 29 ]; then
            EVENT_HUB_CONSUMER_GROUP_ID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 30 ]; then
            DESIRED_MODULES_TO_RESTART_CSV="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 31 ]; then
            RESTART_INTERVAL_IN_MINS="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 32 ]; then
            LOG_ANALYTICS_ENABLED="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 33 ]; then
            LOG_ANALYTICS_WORKSPACE_ID="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 34 ]; then
            LOG_ANALYTICS_SHARED_KEY="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 35 ]; then
            LOG_ANALYTICS_LOG_TYPE="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 36 ]; then
            TWIN_UPDATE_SIZE="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 37 ]; then
            TWIN_UPDATE_FREQUENCY="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 38 ]; then
            TWIN_UPDATE_FAILURE_THRESHOLD="$arg"
            saveNextArg=0;
        elif [ $saveNextArg -eq 39 ]; then
            METRICS_ENDPOINTS_CSV="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 40 ]; then
            METRICS_SCRAPE_FREQUENCY_IN_SECS="$arg"
            saveNextArg=0
        elif [ $saveNextArg -eq 41 ]; then
            METRICS_UPLOAD_TARGET="$arg"
            saveNextArg=0
        else
            case "$arg" in
                '-h' | '--help' ) usage;;
                '-testDir' ) saveNextArg=1;;
                '-releaseLabel' ) saveNextArg=2;;
                '-artifactImageBuildNumber' ) saveNextArg=3;;
                '-testName' ) saveNextArg=4;;
                '-containerRegistry' ) saveNextArg=5;;
                '-containerRegistryUsername' ) saveNextArg=6;;
                '-containerRegistryPassword' ) saveNextArg=7;;
                '-iotHubConnectionString' ) saveNextArg=8;;
                '-eventHubConnectionString' ) saveNextArg=9;;
                '-loadGenMessageFrequency' ) saveNextArg=10;;
                '-snitchAlertUrl' ) saveNextArg=11;;
                '-snitchBuildNumber' ) saveNextArg=12;;
                '-snitchReportingIntervalInSecs' ) saveNextArg=13;;
                '-snitchStorageAccount' ) saveNextArg=14;;
                '-snitchStorageMasterKey' ) saveNextArg=15;;
                '-snitchTestDurationInSecs' ) saveNextArg=16;;
                '-transportType1' ) saveNextArg=17;;
                '-transportType2' ) saveNextArg=18;;
                '-transportType3' ) saveNextArg=19;;
                '-transportType4' ) saveNextArg=20;;
                '-amqpSettingsEnabled' ) saveNextArg=21;;
                '-mqttSettingsEnabled' ) saveNextArg=22;;
                '-certScriptDir' ) saveNextArg=23;;
                '-installRootCACertPath' ) saveNextArg=24;;
                '-installRootCAKeyPath' ) saveNextArg=25;;
                '-installRootCAKeyPassword' ) saveNextArg=26;;
                '-dpsScopeId' ) saveNextArg=27;;
                '-dpsMasterSymmetricKey' ) saveNextArg=28;;
                '-eventHubConsumerGroupId' ) saveNextArg=29;;
                '-desiredModulesToRestartCSV' ) saveNextArg=30;;
                '-restartIntervalInMins' ) saveNextArg=31;;
                '-logAnalyticsEnabled' ) saveNextArg=32;;
                '-logAnalyticsWorkspaceId' ) saveNextArg=33;;
                '-logAnalyticsSharedKey' ) saveNextArg=34;;
                '-logAnalyticsLogType' ) saveNextArg=35;;
                '-twinUpdateSize' ) saveNextArg=36;;
                '-twinUpdateFrequency' ) saveNextArg=37;;
                '-twinUpdateFailureThreshold' ) saveNextArg=38;;
                '-metricsEndpointsCSV' ) saveNextArg=39;;
                '-metricsScrapeFrequencyInSecs' ) saveNextArg=40;;
                '-metricsUploadTarget' ) saveNextArg=41;;
                '-cleanAll' ) CLEAN_ALL=1;;
                * ) usage;;
            esac
        fi
    done

    # Required parameters
    [[ -z "$RELEASE_LABEL" ]] && { print_error 'Release label is required.'; exit 1; }
    [[ -z "$ARTIFACT_IMAGE_BUILD_NUMBER" ]] && { print_error 'Artifact image build number is required'; exit 1; }
    [[ -z "$TEST_NAME" ]] && { print_error 'Test name is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_USERNAME" ]] && { print_error 'Container registry username is required'; exit 1; }
    [[ -z "$CONTAINER_REGISTRY_PASSWORD" ]] && { print_error 'Container registry password is required'; exit 1; }
    [[ -z "$IOTHUB_CONNECTION_STRING" ]] && { print_error 'IoT hub connection string is required'; exit 1; }
    [[ -z "$EVENTHUB_CONNECTION_STRING" ]] && { print_error 'Event hub connection string is required'; exit 1; }
    [[ -z "$LOG_ANALYTICS_ENABLED" ]] && { LOG_ANALYTICS_ENABLED="false"; }
    [[ "$LOG_ANALYTICS_ENABLED" == true ]] && \
    {  [[ -z "$LOG_ANALYTICS_WORKSPACE_ID" ]] && { print_error 'Log Analytics Workspace ID is required'; exit 1; }; \
       [[ -z "$LOG_ANALYTICS_SHARED_KEY" ]] && { print_error 'Log Analytics secret is required'; exit 1; }; \
       [[ -z "$LOG_ANALYTICS_LOG_TYPE" ]] && { print_error 'Log Analytics Log Type is required'; exit 1; }; }

    echo 'Required parameters are provided'
}

function run_all_tests()
{
    local funcRet=0
    local testRet=0

    TEST_NAME='DirectMethodAmqp'
    run_directmethodamqp_test && funcRet=$? || funcRet=$?

    TEST_NAME='DirectMethodAmqpMqtt'
    run_directmethodamqpmqtt_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DirectMethodAmqpws'
    run_directmethodamqpws_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DirectMethodMqtt'
    run_directmethodmqtt_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DirectMethodMqttAmqp'
    run_directmethodmqttamqp_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DirectMethodMqttws'
    run_directmethodmqttws_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DpsSymmetricKeyProvisioning'
    run_dps_provisioning_test "SymmetricKey" && funcRet=$? || funcRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DpsTpmProvisioning'
    run_dps_provisioning_test "Tpm" && funcRet=$? || funcRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='DpsX509Provisioning'
    run_dps_provisioning_test "X509" && funcRet=$? || funcRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='QuickstartCerts'
    run_quickstartcerts_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='TempFilter'
    run_tempfilter_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='TempFilterFunctions'
    run_tempfilterfunctions_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    TEST_NAME='TempSensor'
    run_tempsensor_test && testRet=$? || testRet=$?
    if [ $funcRet -eq 0 ]; then funcRet=$testRet; fi

    return $funcRet
}

function run_directmethod_test()
{
    SECONDS=0
    local ret=0
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
        --verify-data-from-module "DirectMethodSender" \
        -l "$deployment_working_file" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_directmethodamqp_test() {
    print_highlighted_message "Run DirectMethod test with Amqp upstream protocol and Amqp client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMAmqp"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod test with Amqp upstream protocol and Amqp client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_directmethodamqpmqtt_test() {
    print_highlighted_message "Run DirectMethod test with Amqp upstream protocol and Mqtt client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMAmqpMqtt"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod test with Amqp upstream protocol and Mqtt client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_directmethodamqpws_test() {
    print_highlighted_message "Run DirectMethod test with AmqpWs upstream protocol and AmqpWs client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMAmqpws"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod  test with AmqpWs upstream protocol and AmqpWs client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_directmethodmqtt_test() {
    print_highlighted_message "Run DirectMethod test with Mqtt upstream protocol and Mqtt client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMMqtt"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod test with Mqtt upstream protocol and Mqtt client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_directmethodmqttamqp_test() {
    print_highlighted_message "Run DirectMethod test with Mqtt upstream protocol and Amqp client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMMqttAmqp"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod test with Mqtt upstream protocol and Amqp client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_directmethodmqttws_test() {
    print_highlighted_message "Run DirectMethod test with MqttWs upstream protocol and MqttWs client transport type for $image_architecture_label"
    test_setup

    device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DMMqttws"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DirectMethod test with MqttWs upstream protocol and MqttWs client transport type on '$device_id' started at $test_start_time"

    run_directmethod_test && ret=$? || ret=$?

    return $ret
}

function run_dps_provisioning_test() {
    local provisioning_type="${1}"

    print_highlighted_message "Run DPS provisioning test using $provisioning_type for $image_architecture_label"
    test_setup

    local registration_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-DPS-$provisioning_type"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run DPS provisioning test $provisioning_type for registration id '$registration_id' started at $test_start_time"

    local dps_command_flags=""
    if [[ $provisioning_type == "SymmetricKey" ]]; then
        dps_command_flags="--dps-scope-id=$DPS_SCOPE_ID \
                           --dps-registration-id=$registration_id \
                           --dps-master-symmetric-key=$DPS_MASTER_SYMMETRIC_KEY"
    elif [[ $provisioning_type == "Tpm"  ]]; then
        dps_command_flags="--dps-scope-id=$DPS_SCOPE_ID \
                           --dps-registration-id=$registration_id"
    else # x.509 provisioning
        # generate the edge device identity primary certificate and key
        FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_device_certificate "${registration_id}"
        local edge_device_id_cert=$(readlink -f ${CERT_SCRIPT_DIR}/certs/iot-device-${registration_id}-full-chain.cert.pem)
        local edge_device_id_key=$(readlink -f ${CERT_SCRIPT_DIR}/private/iot-device-${registration_id}.key.pem)
        dps_command_flags="--dps-scope-id=$DPS_SCOPE_ID \
                           --device_identity_pk=$edge_device_id_key \
                           --device_identity_cert=$edge_device_id_cert"
    fi

    SECONDS=0
    local ret=0
    # note the registration id is the expected device id to be provisioned by DPS
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$registration_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        -tw "$E2E_TEST_DIR/artifacts/core-linux/e2e_test_files/twin_test_tempSensor.json" \
        --optimize_for_performance="$optimize_for_performance" \
        $dps_command_flags \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_longhaul_test() {
    print_highlighted_message "Run Long Haul test for $image_architecture_label"
    test_setup

    local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-longhaul"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run Long Haul test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
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
        --runtime-log-level "Info" \
        --no-verify && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_quickstartcerts_test() {
    print_highlighted_message "Run Quickstart Certs test for $image_architecture_label"
    test_setup

    local device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-QuickstartCerts"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run Quickstart Certs test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "doesNotNeed" \
        -n "$(hostname)" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        --leave-running=Core \
        --optimize_for_performance="$optimize_for_performance" \
        --no-verify && ret=$? || ret=$?

    declare -a certs=( /var/lib/iotedge/hsm/certs/edge_owner_ca*.pem )
    echo "cert: ${certs[0]}"
    # Workaround for multiple certificates in the x509store - remove this after quick start certs have Authority Key Identifier
    rm -rf ~/.dotnet/corefx/cryptography/x509stores/root/

    "$leafdevice_working_folder/LeafDevice" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -d "$device_id-leaf" \
        -ct "${certs[0]}" \
        -ed "$(hostname)" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_stress_test() {
    print_highlighted_message "Run Stress test for $image_architecture_label"
    test_setup

    local device_id="$RELEASE_LABEL-Linux-$image_architecture_label-stress"

    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run Stress test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "doesNotNeed" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        --leave-running=All \
        -l "$deployment_working_file" \
        --runtime-log-level "Info" \
        --no-verify && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_tempfilter_test() {
    print_highlighted_message "Run TempFilter test for $image_architecture_label"
    test_setup

    local device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-tempFilter"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run TempFilter test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        --verify-data-from-module "tempFilter" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        -l "$deployment_working_file" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_tempfilterfunctions_test() {
    print_highlighted_message "Run TempFilterFunctions test for $image_architecture_label"
    test_setup

    local device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-tempFilterFunc"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run TempFilterFunctions test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        --verify-data-from-module "tempFilterFunctions" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" \
        -l "$deployment_working_file" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_tempsensor_test() {
    print_highlighted_message "Run TempSensor test for $image_architecture_label"
    test_setup

    local device_id="e2e-$RELEASE_LABEL-Linux-$image_architecture_label-tempSensor"
    test_start_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_highlighted_message "Run TempSensor test with -d '$device_id' started at $test_start_time"

    SECONDS=0
    local ret=0
    "$quickstart_working_folder/IotEdgeQuickstart" \
        -d "$device_id" \
        -a "$iotedge_package" \
        -c "$IOTHUB_CONNECTION_STRING" \
        -e "$EVENTHUB_CONNECTION_STRING" \
        -r "$CONTAINER_REGISTRY" \
        -u "$CONTAINER_REGISTRY_USERNAME" \
        -p "$CONTAINER_REGISTRY_PASSWORD" \
        -n "$(hostname)" \
        -tw "$E2E_TEST_DIR/artifacts/core-linux/e2e_test_files/twin_test_tempSensor.json" \
        --optimize_for_performance="$optimize_for_performance" \
        -t "$ARTIFACT_IMAGE_BUILD_NUMBER-linux-$image_architecture_label" && ret=$? || ret=$?

    local elapsed_seconds=$SECONDS
    test_end_time="$(date '+%Y-%m-%d %H:%M:%S')"
    print_logs $ret "$test_end_time" $elapsed_seconds

    return $ret
}

function run_test()
{
    if [[ $INSTALL_CA_CERT -eq 1 ]]; then
        set_certificate_generation_tools_dir
        [[ -z "$CERT_SCRIPT_DIR" ]] && { print_error 'Certificate script dir is required'; exit 1; }
        [[ ! -d "$CERT_SCRIPT_DIR" ]] && { print_error 'Certificate script dir is invalid'; exit 1; }
        FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh install_root_ca_from_files ${ROOT_CA_CERT_PATH} ${ROOT_CA_KEY_PATH} ${ROOT_CA_PASSWORD}
    fi

    local ret=0
    case "${TEST_NAME,,}" in
        'all') run_all_tests && ret=$? || ret=$?;;
        'directmethodamqp') run_directmethodamqp_test && ret=$? || ret=$?;;
        'directmethodamqpmqtt') run_directmethodamqpmqtt_test && ret=$? || ret=$?;;
        'directmethodamqpws') run_directmethodamqpws_test && ret=$? || ret=$?;;
        'directmethodmqtt') run_directmethodmqtt_test && ret=$? || ret=$?;;
        'directmethodmqttamqp') run_directmethodmqttamqp_test && ret=$? || ret=$?;;
        'directmethodmqttws') run_directmethodmqttws_test && ret=$? || ret=$?;;
        'dpssymmetrickeyprovisioning') run_dps_provisioning_test "SymmetricKey" && ret=$? || ret=$?;;
        'dpstpmprovisioning') run_dps_provisioning_test "Tpm" && ret=$? || ret=$?;;
        'dpsx509provisioning') run_dps_provisioning_test "X509" && ret=$? || ret=$?;;
        'quickstartcerts') run_quickstartcerts_test && ret=$? || ret=$?;;
        'longhaul') run_longhaul_test && ret=$? || ret=$?;;
        'stress') run_stress_test && ret=$? || ret=$?;;
        'tempfilter') run_tempfilter_test && ret=$? || ret=$?;;
        'tempfilterfunctions') run_tempfilterfunctions_test && ret=$? || ret=$?;;
        'tempsensor') run_tempsensor_test && ret=$? || ret=$?;;
        *) print_highlighted_message "Can't find any test with name '$TEST_NAME'";;
    esac

    echo "Test exit with result code $ret"
    exit $ret
}

function test_setup() {
    validate_test_parameters
    clean_up
    prepare_test_from_artifacts
    create_iotedge_service_config
}

function validate_test_parameters() {
    print_highlighted_message "Validate test parameters for $TEST_NAME"

    local required_files=()
    local required_folders=()

    required_files+=("$iotedge_quickstart_artifact_file")
    required_folders+=("$iotedged_artifact_folder")

    case "${TEST_NAME,,}" in
        'tempsensor')
            required_files+=($twin_testfile_artifact_file);;
        'tempfilter')
            required_files+=($module_to_module_deployment_artifact_file);;
        'tempfilterfunctions')
            required_files+=($module_to_functions_deployment_artifact_file);;
        'longhaul')
            required_files+=($long_haul_deployment_artifact_file);;
        'quickstartcerts')
            required_files+=($leafdevice_artifact_file);;
        'stress')
            required_files+=($stress_deployment_artifact_file);;
    esac

    if [[ "${TEST_NAME,,}" == directmethod* ]]; then
        required_files+=($dm_module_to_module_deployment_artifact_file)
    fi

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

    if [[ "${TEST_NAME,,}" == "longhaul" ]] ||
       [[ "${TEST_NAME,,}" == "stress" ]]; then
        if [[ -z "$SNITCH_ALERT_URL" ]]; then
            print_error "Required snitch alert URL."
            ((error++))
        fi

        if [[ -z "$SNITCH_STORAGE_ACCOUNT" ]]; then
            print_error "Required snitch storage account."
            ((error++))
        fi

        if [[ -z "$SNITCH_STORAGE_MASTER_KEY" ]]; then
            print_error "Required snitch storage master key."
            ((error++))
        fi
    fi

    if (( error > 0 )); then
        exit 1
    fi
}

function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ''
    echo 'options'
    echo ' -testDir                          Path of E2E test directory which contains artifacts and certs folders; defaul to current directory.'
    echo ' -releaseLabel                     Release label can be uniquely identify the build (e.g <ReleaseName>-<ReleaseAttempt>); which is used as part of Edge device name.'
    echo ' -testName                         Name of E2E test to be run.'
    echo "                                   Values are 'All', 'DirectMethodAmqp', 'DirectMethodAmqpMqtt', 'DirectMethodAmqpWs', 'DirectMethodMqtt', 'DirectMethodMqttAmqp', "
    echo "                                   'DirectMethodMqttWs', 'LongHaul', 'QuickstartCerts', 'Stress', 'TempFilter', 'TempFilterFunctions', 'TempSensor'"
    echo "                                   'DpsSymmetricKeyProvisioning', 'DpsTpmProvisioning', 'DpsX509Provisioning'"
    echo "                                   'LongHaul', 'QuickstartCerts', 'Stress', 'TempFilter', 'TempFilterFunctions', 'TempSensor'"
    echo "                                   Note: 'All' option doesn't include long hual and stress test."
    echo ' -artifactImageBuildNumber         Artifact image build number is used to construct path of docker images, pulling from docker registry. E.g. 20190101.1.'
    echo " -containerRegistry                Host address of container registry."
    echo " -containerRegistryUsername        Username of container registry."
    echo ' -containerRegistryPassword        Password of given username for container registory.'
    echo ' -iotHubConnectionString           IoT hub connection string for creating edge device.'
    echo ' -eventHubConnectionString         Event hub connection string for receive D2C messages.'
    echo ' -eventHubConsumerGroupId          Optional Event Hub Consumer Group ID for the Analyzer module.'
    echo ' -loadGenMessageFrequency          Frequency to send messages in LoadGen module for long haul and stress test. Default is 00.00.01 for long haul and 00:00:00.03 for stress test.'
    echo ' -snitchAlertUrl                   Alert Url pointing to Azure Logic App for email preparation and sending for long haul and stress test.'
    echo ' -snitchBuildNumber                Build number for snitcher docker image for long haul and stress test. Default is 1.1.'
    echo ' -snitchReportingIntervalInSecs    Reporting frequency in seconds to send status email for long hual and stress test. Default is 86400 (1 day) for long haul and 1700000 for stress test.'
    echo ' -snitchStorageAccount             Azure blob Storage account for store logs used in status email for long haul and stress test.'
    echo ' -snitchStorageMasterKey           Master key of snitch storage account for long haul and stress test.'
    echo ' -snitchTestDurationInSecs         Test duration in seconds for long haul and stress test.'
    echo ' -transportType1                   Transport type for LoadGen1 and TwinTester1 for stress test. Default is amqp.'
    echo ' -transportType2                   Transport type for LoadGen2 and TwinTester2 for stress test. Default is amqp.'
    echo ' -transportType3                   Transport type for LoadGen3 and TwinTester3 for stress test. Default is mqtt.'
    echo ' -transportType4                   Transport type for LoadGen4 and TwinTester4 for stress test. Default is mqtt.'
    echo ' -amqpSettingsEnabled              Enable amqp protocol head in Edge Hub.'
    echo ' -mqttSettingsEnabled              Enable mqtt protocol head in Edge Hub.'
    echo ' -dpsScopeId                       DPS scope id. Required only when using DPS to provision the device.'
    echo ' -dpsMasterSymmetricKey            DPS master symmetric key. Required only when using DPS symmetric key to provision the Edge device.'
    echo ' -certScriptDir                    Optional path to certificate generation script dir'
    echo ' -installRootCACertPath            Optional path to root CA certificate to be used for certificate generation'
    echo ' -installRootCAKeyPath             Optional path to root CA certificate private key to be used for certificate generation'
    echo ' -installRootCAKeyPassword         Optional password to access the root CA certificate private key to be used for certificate generation'
    echo ' -desiredModulesToRestartCSV       Optional CSV string of module names for long haul specifying what modules to restart. If specified, then "restartIntervalInMins" must be specified as well.'
    echo ' -restartIntervalInMins            Optional value for long haul specifying how often a random module will restart. If specified, then "desiredModulesToRestartCSV" must be specified as well.'
    echo ' -logAnalyticsEnabled              Optional Log Analytics enable string for the Analyzer module. If logAnalyticsEnabled is set to enable (true), the rest of Log Analytics parameters must be provided.'
    echo ' -logAnalyticsWorkspaceId          Optional Log Analytics workspace ID for metrics collection and reporting.'
    echo ' -logAnalyticsSharedKey            Optional Log Analytics shared key for metrics collection and reporting.'
    echo ' -logAnalyticsLogType              Optional Log Analytics log type for the Analyzer module.'
    echo ' -twinUpdateSize                   Specifies the char count (i.e. size) of each twin update. Default is 1 for long haul and 100 for stress test.'
    echo ' -twinUpdateFrequency              Frequency to make twin updates. This should be specified in DateTime format. Default is 00:00:15 for long haul and 00:00:05 for stress test.'
    echo ' -twinUpdateFailureThreshold       Specifies the longest period of time a twin update can take before being marked as a failure. This should be specified in DateTime format. Default is 00:01:00'
    echo ' -metricsEndpointsCSV              Optional csv of exposed endpoints for which to scrape metrics.'
    echo ' -metricsScrapeFrequencyInSecs     Optional frequency at which the MetricsCollector module will scrape metrics from the exposed metrics endpoints. Default is 300 seconds.'
    echo ' -metricsUploadTarget              Optional upload target for metrics. Valid values are AzureLogAnalytics or IoTHub. Default is AzureLogAnalytics.'
    exit 1;
}

process_args "$@"

CONTAINER_REGISTRY="${CONTAINER_REGISTRY:-edgebuilds.azurecr.io}"
E2E_TEST_DIR="${E2E_TEST_DIR:-$(pwd)}"
EVENT_HUB_CONSUMER_GROUP_ID=${EVENT_HUB_CONSUMER_GROUP_ID:-\$Default}
SNITCH_BUILD_NUMBER="${SNITCH_BUILD_NUMBER:-1.2}"
TRANSPORT_TYPE_1="${TRANSPORT_TYPE_1:-amqp}"
TRANSPORT_TYPE_2="${TRANSPORT_TYPE_2:-amqp}"
TRANSPORT_TYPE_3="${TRANSPORT_TYPE_3:-mqtt}"
TRANSPORT_TYPE_4="${TRANSPORT_TYPE_4:-mqtt}"
TWIN_UPDATE_FAILURE_THRESHOLD="${TWIN_UPDATE_FAILURE_THRESHOLD:-00:01:00}"
METRICS_SCRAPE_FREQUENCY_IN_SECS="${METRICS_SCRAPE_FREQUENCY_IN_SECS:-300}"
METRICS_UPLOAD_TARGET="${METRICS_UPLOAD_TARGET:-AzureLogAnalytics}"
if [[ "${TEST_NAME,,}" == "longhaul" ]]; then
    DESIRED_MODULES_TO_RESTART_CSV="${DESIRED_MODULES_TO_RESTART_CSV:-,}"
    LOADGEN_MESSAGE_FREQUENCY="${LOADGEN_MESSAGE_FREQUENCY:-00:00:01}"
    RESTART_INTERVAL_IN_MINS="${RESTART_INTERVAL_IN_MINS:-10}"
    SNITCH_REPORTING_INTERVAL_IN_SECS="${SNITCH_REPORTING_INTERVAL_IN_SECS:-86400}"
    SNITCH_TEST_DURATION_IN_SECS="${SNITCH_TEST_DURATION_IN_SECS:-604800}"
    TWIN_UPDATE_SIZE="${TWIN_UPDATE_SIZE:-1}"
    TWIN_UPDATE_FREQUENCY="${TWIN_UPDATE_FREQUENCY:-00:00:15}"
fi
if [[ "${TEST_NAME,,}" == "stress" ]]; then
    LOADGEN_MESSAGE_FREQUENCY="${LOADGEN_MESSAGE_FREQUENCY:-00:00:00.03}"
    SNITCH_REPORTING_INTERVAL_IN_SECS="${SNITCH_REPORTING_INTERVAL_IN_SECS:-1700000}"
    SNITCH_TEST_DURATION_IN_SECS="${SNITCH_TEST_DURATION_IN_SECS:-14400}"
    TWIN_UPDATE_SIZE="${TWIN_UPDATE_SIZE:-100}"
    TWIN_UPDATE_FREQUENCY="${TWIN_UPDATE_FREQUENCY:-00:00:01}"
fi
if [ "$AMQP_SETTINGS_ENABLED" != "false" ]; then
    AMQP_SETTINGS_ENABLED="true"
fi
if [ "$MQTT_SETTINGS_ENABLED" != "false" ]; then
    MQTT_SETTINGS_ENABLED="true"
fi

working_folder="$E2E_TEST_DIR/working"
get_image_architecture_label
optimize_for_performance=true
if [ "$image_architecture_label" = 'arm32v7' ] ||
   [ "$image_architecture_label" = 'arm64v8' ]; then
    optimize_for_performance=false
fi

iotedged_artifact_folder="$(get_iotedged_artifact_folder)"
iotedge_quickstart_artifact_file="$(get_iotedge_quickstart_artifact_file)"
leafdevice_artifact_file="$(get_leafdevice_artifact_file)"
twin_testfile_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_test_files/twin_test_tempSensor.json"
module_to_module_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/module_to_module_deployment.template.json"
module_to_functions_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/module_to_functions_deployment.template.json"
dm_module_to_module_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/dm_module_to_module_deployment.json"
long_haul_deployment_artifact_file="$(get_long_haul_deployment_artifact_file)"
stress_deployment_artifact_file="$E2E_TEST_DIR/artifacts/core-linux/e2e_deployment_files/stress_deployment.template.json"
deployment_working_file="$working_folder/deployment.json"
quickstart_working_folder="$working_folder/quickstart"
leafdevice_working_folder="$working_folder/leafdevice"

run_test
