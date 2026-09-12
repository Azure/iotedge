#!/bin/bash

###############################################################################
# Provisions everything needed to manually test the Metrics Collector module
# with UploadTarget=AzureMonitor, running on IoT Edge on an Ubuntu VM in Azure.
#
# Two modes, selected with --upload-mode:
#
#   ingestion (default)  The migrated path. Uses the Logs Ingestion API with a
#                        Data Collection Endpoint/Rule, a custom _CL table, and
#                        the VM's system-assigned managed identity. Requires an
#                        image built from the migrated source (pass --image).
#
#   legacy               The current released path, retiring 14 Sept 2026. Uses
#                        LogAnalyticsWorkspaceId/LogAnalyticsSharedKey and the
#                        OMS agent protocol, writing to the InsightsMetrics
#                        table. Use this to capture a baseline for comparison.
#
# Creates (both modes):
#   - Log Analytics workspace
#   - IoT Hub + edge device identity
#   - Network security group, virtual network, public IP, and NIC
#   - Ubuntu VM with IoT Edge installed and provisioned
#   - An applied deployment manifest running the Metrics Collector module
#     alongside SimulatedTemperatureSensor, so there is real module activity
#     for the collector to scrape
#
# Creates (ingestion mode only):
#   - Custom table with an InsightsMetrics-compatible schema
#   - Data Collection Endpoint (DCE) and Data Collection Rule (DCR)
#   - System-assigned managed identity on the VM, granted "Monitoring Metrics
#     Publisher" on the DCR. No Entra app registration is created: creating one
#     requires Microsoft Graph, which Conditional Access token-protection
#     policies commonly block for the Azure CLI regardless of account/device,
#     and managed identity is the natural fit for a workload already running on
#     an Azure VM.
#
# Prerequisites: az (logged in via `az login`), jq, openssl.
#
# The signed-in identity needs permission to create role assignments, because
# IoT Hub data-plane calls use Entra auth (hub-scoped SAS is often disabled by
# policy) and subscription Owner/Contributor does not confer data-plane access.
#
# NOTE: This provisions a disposable TEST environment. See "Security notes" in
# the summary output before reusing any of this shape in production.
#
# NOTE: This script is single-shot. Edge device identity creation is not safely
# repeatable, so it refuses to run against an environment that already uses the
# same prefix. Tear the old one down or pass a different --prefix.
###############################################################################

set -euo pipefail

SUBSCRIPTION=''
REGISTRY_SUBSCRIPTION=''
RESOURCE_GROUP=''
SSH_PUBLIC_KEY=''
UPLOAD_MODE='ingestion'
LOCATION='eastus'
PREFIX=''
METRICS_COLLECTOR_IMAGE=''
LEGACY_DEFAULT_IMAGE='mcr.microsoft.com/azureiotedge-metrics-collector:1.3'
SIMULATED_SENSOR_IMAGE='mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.5'
SCRAPE_FREQUENCY_SECS='60'
VM_SIZE='Standard_D4ds_v5'
VM_IMAGE='Ubuntu2404'
VNET_ADDRESS_PREFIX='10.0.0.0/16'
SUBNET_ADDRESS_PREFIX='10.0.0.0/24'
TABLE_NAME='InsightsMetricsCustom_CL'
RUN_OK_SENTINEL='__RUN_ON_VM_OK__'

function usage() {
    echo "setupMetricsCollectorManualTest.sh [options]"
    echo ''
    echo 'Required:'
    echo ' -s, --subscription <id|name>   Azure subscription to deploy into.'
    echo '     --registry-subscription <id|name>'
    echo '                                Azure subscription containing the image registry.'
    echo '                                Defaults to --subscription.'
    echo ' -g, --resource-group <name>    Resource group (created if it does not exist).'
    echo ' -k, --ssh-public-key <path>    Path to the SSH public key file to authorize on the VM.'
    echo ''
    echo 'Optional:'
    echo ' -m, --upload-mode <mode>       ingestion (Logs Ingestion API, default) or'
    echo '                                legacy (LogAnalyticsWorkspaceId/SharedKey).'
    echo ' -l, --location <region>        Azure region. Default: eastus'
    echo ' -p, --prefix <string>          Name prefix for created resources. Default: mcx<random>'
    echo ' -i, --image <image>            Metrics Collector image to deploy.'
    echo '                                Required for ingestion mode (must be built from the'
    echo '                                migrated source; released images do not support it).'
    echo "                                Defaults to $LEGACY_DEFAULT_IMAGE for legacy mode."
    echo ' -f, --scrape-frequency <secs>  ScrapeFrequencyInSecs for the module. Default: 60'
    echo ' -t, --table-name <name>        Custom table name, must end in _CL. Ingestion mode only.'
    echo "                                Default: $TABLE_NAME"
    echo '     --vm-size <size>           VM size. Default: Standard_D4ds_v5'
    echo ' -h, --help                     Show this help.'
    exit 1
}

function print_step() {
    printf '\n\033[0;36m==> %s\033[0m\n' "$1"
}

function print_error() {
    printf '\033[0;31m%s\033[0m\n' "$1" >&2
}

# Retries a command, to ride out RBAC propagation delays.
function retry() {
    local attempts="$1"; shift
    local delay="$1"; shift
    local i

    for (( i = 1; i <= attempts; i++ )); do
        if "$@"; then
            return 0
        fi
        if (( i < attempts )); then
            printf '    attempt %d/%d failed; retrying in %ds...\n' "$i" "$attempts" "$delay" >&2
            sleep "$delay"
        fi
    done

    return 1
}

# Resolves the object ID of whoever is running the script, so it can be granted
# the IoT Hub data-plane role. Reads the `oid` claim from the ARM access token
# rather than calling Microsoft Graph, which conditional access policies
# frequently block (AADSTS530084).
function resolve_current_principal() {
    local user_type token payload remainder

    user_type=$(az account show --query user.type -o tsv)
    if [[ "$user_type" == 'servicePrincipal' ]]; then
        CURRENT_PRINCIPAL_TYPE='ServicePrincipal'
    else
        CURRENT_PRINCIPAL_TYPE='User'
    fi

    token=$(az account get-access-token --query accessToken -o tsv)
    payload=$(echo "$token" | cut -d. -f2 | tr '_-' '/+')
    remainder=$(( ${#payload} % 4 ))
    if (( remainder == 2 )); then
        payload="${payload}=="
    elif (( remainder == 3 )); then
        payload="${payload}="
    fi

    CURRENT_PRINCIPAL_ID=$(echo "$payload" | base64 -d 2>/dev/null | jq -r '.oid // empty')
    if [[ -z "$CURRENT_PRINCIPAL_ID" ]]; then
        print_error 'Could not read the current principal object ID from the access token.'
        exit 1
    fi
}

function process_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help) usage;;
            -s|--subscription) SUBSCRIPTION="$2"; shift 2;;
            --registry-subscription) REGISTRY_SUBSCRIPTION="$2"; shift 2;;
            -g|--resource-group) RESOURCE_GROUP="$2"; shift 2;;
            -k|--ssh-public-key) SSH_PUBLIC_KEY="$2"; shift 2;;
            -m|--upload-mode) UPLOAD_MODE="$2"; shift 2;;
            -l|--location) LOCATION="$2"; shift 2;;
            -p|--prefix) PREFIX="$2"; shift 2;;
            -i|--image) METRICS_COLLECTOR_IMAGE="$2"; shift 2;;
            -f|--scrape-frequency) SCRAPE_FREQUENCY_SECS="$2"; shift 2;;
            -t|--table-name) TABLE_NAME="$2"; shift 2;;
            --vm-size) VM_SIZE="$2"; shift 2;;
            *) print_error "Unsupported argument: $1"; usage;;
        esac
    done

    [[ -z "$SUBSCRIPTION" ]] && { print_error 'Subscription is required.'; usage; }
    [[ -z "$REGISTRY_SUBSCRIPTION" ]] && REGISTRY_SUBSCRIPTION="$SUBSCRIPTION"
    [[ -z "$RESOURCE_GROUP" ]] && { print_error 'Resource group is required.'; usage; }
    [[ -z "$SSH_PUBLIC_KEY" ]] && { print_error 'SSH public key file is required.'; usage; }
    [[ ! -f "$SSH_PUBLIC_KEY" ]] && { print_error "SSH public key file not found: $SSH_PUBLIC_KEY"; exit 1; }
    [[ "$TABLE_NAME" != *_CL ]] && { print_error 'Custom table name must end with _CL.'; exit 1; }

    if [[ "$UPLOAD_MODE" != 'ingestion' && "$UPLOAD_MODE" != 'legacy' ]]; then
        print_error "Upload mode must be 'ingestion' or 'legacy', got: $UPLOAD_MODE"
        exit 1
    fi

    # Released images predate the Logs Ingestion migration and ignore the
    # DataCollection* settings, so ingestion mode needs an explicit image.
    if [[ -z "$METRICS_COLLECTOR_IMAGE" ]]; then
        if [[ "$UPLOAD_MODE" == 'legacy' ]]; then
            METRICS_COLLECTOR_IMAGE="$LEGACY_DEFAULT_IMAGE"
        else
            print_error 'Ingestion mode requires --image pointing at a build of the migrated module.'
            exit 1
        fi
    fi

    for tool in az jq openssl; do
        command -v "$tool" >/dev/null 2>&1 || { print_error "Required tool not found: $tool"; exit 1; }
    done

    if [[ -z "$PREFIX" ]]; then
        # `... | head -c` would SIGPIPE the producer and trip pipefail, so read a
        # fixed number of bytes instead.
        PREFIX="mcx$(od -An -tx1 -N3 /dev/urandom | tr -d ' \n')"
    fi
}

# az vm run-command returns a single blob containing [stdout] and [stderr]
# sections; this pulls out just the stdout portion.
function run_on_vm() {
    local description="$1"
    local script_file="$2"
    shift 2

    # The extension reports "Enable succeeded" even when the script itself fails,
    # so require the script to reach a sentinel on its last line.
    local wrapped="$WORK_DIR/run-on-vm.sh"
    cat "$script_file" > "$wrapped"
    printf '\necho %s\n' "$RUN_OK_SENTINEL" >> "$wrapped"

    local raw
    raw=$(az vm run-command invoke \
        --resource-group "$RESOURCE_GROUP" \
        --name "$VM_NAME" \
        --command-id RunShellScript \
        --scripts "@$wrapped" \
        "$@" \
        --query 'value[0].message' -o tsv)

    if [[ "$raw" != *"$RUN_OK_SENTINEL"* ]]; then
        print_error "Remote step failed: $description"
        echo "$raw" >&2
        exit 1
    fi

    # Strip everything before [stdout] and after [stderr]
    echo "$raw" | sed -n '/\[stdout\]/,/\[stderr\]/p' | sed '1d;$d' | sed "/$RUN_OK_SENTINEL/d"
}

# Re-running against an existing environment would fail partway through and
# leave it half-modified, so refuse up front instead.
function check_for_existing_environment() {
    local conflicts=''
    local existing

    if [[ "$(az group exists --name "$RESOURCE_GROUP")" == 'true' ]]; then
        existing=$(az resource list \
            --resource-group "$RESOURCE_GROUP" \
            --query "[?starts_with(name, '$PREFIX')].name" -o tsv 2>/dev/null || true)
        if [[ -n "$existing" ]]; then
            conflicts+="Resources in resource group '$RESOURCE_GROUP' matching prefix '$PREFIX':\n"
            conflicts+="$(echo "$existing" | sed 's/^/  - /')\n"
        fi
    fi

    if [[ -z "$conflicts" ]]; then
        return 0
    fi

    print_error "An environment using prefix '$PREFIX' already exists, and this script is single-shot."
    printf '\n%b\n' "$conflicts" >&2

    {
        echo 'Tear the existing environment down:'
        echo "  az group delete --name $RESOURCE_GROUP --yes --no-wait"
        echo
        echo 'Or re-run with a different --prefix.'
    } >&2

    exit 1
}

process_args "$@"

WORKSPACE_NAME="${PREFIX}-law"
DCE_NAME="${PREFIX}-dce"
DCR_NAME="${PREFIX}-dcr"
IOT_HUB_NAME="${PREFIX}-hub"
VM_NAME="${PREFIX}-vm"
NSG_NAME="${PREFIX}-nsg"
VNET_NAME="${PREFIX}-vnet"
PUBLIC_IP_NAME="${PREFIX}-pip"
NIC_NAME="${PREFIX}-nic"
DEVICE_ID="${PREFIX}-edge"
STREAM_NAME="Custom-${TABLE_NAME}"

WORK_DIR=$(mktemp -d)
trap 'rm -rf "$WORK_DIR"' EXIT

az account set --subscription "$SUBSCRIPTION"
TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

print_step "Checking for an existing environment with prefix '$PREFIX'"
check_for_existing_environment

print_step "Ensuring extensions are installed"
az extension add --name azure-iot --upgrade --only-show-errors >/dev/null

print_step "Creating resource group '$RESOURCE_GROUP' in '$LOCATION' (if needed)"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --only-show-errors >/dev/null

# Created first so that policy-driven rules (for example the ones permitting
# SSH) have time to land while the remaining resources are provisioned.
print_step "Creating network security group '$NSG_NAME'"
az network nsg create \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION" \
    --name "$NSG_NAME" \
    --location "$LOCATION" \
    --only-show-errors >/dev/null

print_step "Creating Log Analytics workspace '$WORKSPACE_NAME'"
az monitor log-analytics workspace create \
    --resource-group "$RESOURCE_GROUP" \
    --workspace-name "$WORKSPACE_NAME" \
    --location "$LOCATION" \
    --only-show-errors >/dev/null
WORKSPACE_ID=$(az monitor log-analytics workspace show \
    --resource-group "$RESOURCE_GROUP" \
    --workspace-name "$WORKSPACE_NAME" \
    --query id -o tsv)

if [[ "$UPLOAD_MODE" == 'legacy' ]]; then

print_step "Retrieving workspace ID and shared key (legacy upload path)"
WORKSPACE_CUSTOMER_ID=$(az monitor log-analytics workspace show \
    --resource-group "$RESOURCE_GROUP" \
    --workspace-name "$WORKSPACE_NAME" \
    --query customerId -o tsv)
WORKSPACE_SHARED_KEY=$(az monitor log-analytics workspace get-shared-keys \
    --resource-group "$RESOURCE_GROUP" \
    --workspace-name "$WORKSPACE_NAME" \
    --query primarySharedKey -o tsv)

else

print_step "Creating custom table, DCE, and DCR"
# The module emits TimeGenerated/Origin/Namespace/Name/Value/Tags/ResourceId.
# The DCR transform below maps ResourceId to Log Analytics resource attribution.
cat > "$WORK_DIR/monitor.json" <<'TEMPLATE_EOF'
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "location": { "type": "string" },
    "workspaceName": { "type": "string" },
    "workspaceResourceId": { "type": "string" },
    "tableName": { "type": "string" },
    "dceName": { "type": "string" },
    "dcrName": { "type": "string" },
    "streamName": { "type": "string" }
  },
  "resources": [
    {
      "type": "Microsoft.OperationalInsights/workspaces/tables",
      "apiVersion": "2022-10-01",
      "name": "[concat(parameters('workspaceName'), '/', parameters('tableName'))]",
      "properties": {
        "schema": {
          "name": "[parameters('tableName')]",
          "columns": [
            { "name": "TimeGenerated", "type": "datetime" },
            { "name": "Origin", "type": "string" },
            { "name": "Namespace", "type": "string" },
            { "name": "Name", "type": "string" },
            { "name": "Value", "type": "real" },
            { "name": "Tags", "type": "string" },
            { "name": "ResourceId", "type": "string" }
          ]
        },
        "retentionInDays": 30
      }
    },
    {
      "type": "Microsoft.Insights/dataCollectionEndpoints",
      "apiVersion": "2022-06-01",
      "name": "[parameters('dceName')]",
      "location": "[parameters('location')]",
      "properties": { "networkAcls": { "publicNetworkAccess": "Enabled" } }
    },
    {
      "type": "Microsoft.Insights/dataCollectionRules",
      "apiVersion": "2022-06-01",
      "name": "[parameters('dcrName')]",
      "location": "[parameters('location')]",
      "dependsOn": [
        "[resourceId('Microsoft.Insights/dataCollectionEndpoints', parameters('dceName'))]",
        "[resourceId('Microsoft.OperationalInsights/workspaces/tables', parameters('workspaceName'), parameters('tableName'))]"
      ],
      "properties": {
        "dataCollectionEndpointId": "[resourceId('Microsoft.Insights/dataCollectionEndpoints', parameters('dceName'))]",
        "streamDeclarations": {
          "[parameters('streamName')]": {
            "columns": [
              { "name": "Origin", "type": "string" },
              { "name": "Namespace", "type": "string" },
              { "name": "Name", "type": "string" },
              { "name": "Value", "type": "real" },
              { "name": "TimeGenerated", "type": "datetime" },
              { "name": "Tags", "type": "string" },
              { "name": "ResourceId", "type": "string" }
            ]
          }
        },
        "destinations": {
          "logAnalytics": [
            {
              "workspaceResourceId": "[parameters('workspaceResourceId')]",
              "name": "laDestination"
            }
          ]
        },
        "dataFlows": [
          {
            "streams": [ "[parameters('streamName')]" ],
            "destinations": [ "laDestination" ],
            "transformKql": "source",
            "outputStream": "[parameters('streamName')]"
          }
        ]
      }
    }
  ],
  "outputs": {
    "dcrImmutableId": {
      "type": "string",
      "value": "[reference(resourceId('Microsoft.Insights/dataCollectionRules', parameters('dcrName'))).immutableId]"
    },
    "dceIngestionEndpoint": {
      "type": "string",
      "value": "[reference(resourceId('Microsoft.Insights/dataCollectionEndpoints', parameters('dceName'))).logsIngestion.endpoint]"
    },
    "dcrResourceId": {
      "type": "string",
      "value": "[resourceId('Microsoft.Insights/dataCollectionRules', parameters('dcrName'))]"
    }
  }
}
TEMPLATE_EOF

DEPLOY_OUTPUT=$(az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "${PREFIX}-monitor" \
    --template-file "$WORK_DIR/monitor.json" \
    --parameters \
        location="$LOCATION" \
        workspaceName="$WORKSPACE_NAME" \
        workspaceResourceId="$WORKSPACE_ID" \
        tableName="$TABLE_NAME" \
        dceName="$DCE_NAME" \
        dcrName="$DCR_NAME" \
        streamName="$STREAM_NAME" \
    --query properties.outputs -o json)

DCR_IMMUTABLE_ID=$(echo "$DEPLOY_OUTPUT" | jq -r '.dcrImmutableId.value')
DCE_ENDPOINT=$(echo "$DEPLOY_OUTPUT" | jq -r '.dceIngestionEndpoint.value')
DCR_RESOURCE_ID=$(echo "$DEPLOY_OUTPUT" | jq -r '.dcrResourceId.value')

fi

print_step "Creating IoT Hub '$IOT_HUB_NAME'"
az iot hub create \
    --name "$IOT_HUB_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku S1 \
    --only-show-errors >/dev/null
HUB_RESOURCE_ID=$(az iot hub show --name "$IOT_HUB_NAME" --query id -o tsv)

print_step "Granting 'IoT Hub Data Contributor' on the hub to the current principal"
# Owner/Contributor cover control-plane actions only, so a data-plane role is
# required for the device registry calls below.
resolve_current_principal
az role assignment create \
    --assignee-object-id "$CURRENT_PRINCIPAL_ID" \
    --assignee-principal-type "$CURRENT_PRINCIPAL_TYPE" \
    --role 'IoT Hub Data Contributor' \
    --scope "$HUB_RESOURCE_ID" \
    --only-show-errors >/dev/null

print_step "Creating edge device identity '$DEVICE_ID'"
# Hub-scoped SAS is frequently disabled by policy (disableLocalAuth), so all
# data-plane calls use Entra auth. Device SAS keys are unaffected and still
# provision the device below.
retry 10 15 az iot hub device-identity create \
    --hub-name "$IOT_HUB_NAME" \
    --device-id "$DEVICE_ID" \
    --edge-enabled \
    --auth-type login \
    --only-show-errors >/dev/null
DEVICE_CONNECTION_STRING=$(az iot hub device-identity connection-string show \
    --hub-name "$IOT_HUB_NAME" \
    --device-id "$DEVICE_ID" \
    --auth-type login \
    --query connectionString -o tsv)

print_step "Creating virtual network '$VNET_NAME'"
az network vnet create \
    --name "$VNET_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION" \
    --location "$LOCATION" \
    --address-prefixes "$VNET_ADDRESS_PREFIX" \
    --only-show-errors >/dev/null

# The subnet is created separately because --default-outbound-access is not
# available on `vnet create`, and policy rejects a subnet that lacks it. The VM
# still reaches the internet through the Standard SKU public IP on its NIC.
print_step "Creating subnet 'default' with default outbound access disabled"
az network vnet subnet create \
    --name 'default' \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION" \
    --vnet-name "$VNET_NAME" \
    --address-prefixes "$SUBNET_ADDRESS_PREFIX" \
    --network-security-group "$NSG_NAME" \
    --default-outbound-access false \
    --only-show-errors >/dev/null

print_step "Creating public IP '$PUBLIC_IP_NAME'"
az network public-ip create \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION" \
    --name "$PUBLIC_IP_NAME" \
    --location "$LOCATION" \
    --sku Standard \
    --allocation-method Static \
    --ip-tags 'FirstPartyUsage=/NonProd' \
    --only-show-errors >/dev/null

print_step "Creating network interface '$NIC_NAME'"
az network nic create \
    --resource-group "$RESOURCE_GROUP" \
    --subscription "$SUBSCRIPTION" \
    --name "$NIC_NAME" \
    --location "$LOCATION" \
    --vnet-name "$VNET_NAME" \
    --subnet 'default' \
    --public-ip-address "$PUBLIC_IP_NAME" \
    --only-show-errors >/dev/null

print_step "Creating VM '$VM_NAME'"
az vm create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --image "$VM_IMAGE" \
    --size "$VM_SIZE" \
    --admin-username azureuser \
    --ssh-key-values "$SSH_PUBLIC_KEY" \
    --nics "$NIC_NAME" \
    --only-show-errors >/dev/null

VM_PUBLIC_IP=$(az network public-ip show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PUBLIC_IP_NAME" \
    --query ipAddress -o tsv)

print_step "Enabling system-assigned managed identity on '$VM_NAME'"
# No Entra app registration is created: that requires Microsoft Graph, which
# Conditional Access token-protection policies commonly block for the Azure
# CLI regardless of account or device compliance. Managed identity is an ARM
# operation and is the natural fit for a workload already running on a VM.
VM_IDENTITY_PRINCIPAL_ID=$(az vm identity assign \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VM_NAME" \
    --query systemAssignedIdentity -o tsv)

if [[ "$UPLOAD_MODE" == 'ingestion' ]]; then

print_step "Granting 'Monitoring Metrics Publisher' on the DCR to the VM identity"
retry 10 15 az role assignment create \
    --assignee-object-id "$VM_IDENTITY_PRINCIPAL_ID" \
    --assignee-principal-type ServicePrincipal \
    --role 'Monitoring Metrics Publisher' \
    --scope "$DCR_RESOURCE_ID" \
    --only-show-errors >/dev/null

fi

# Registry hostname if the module image is hosted on Azure Container Registry;
# empty for public registries like mcr.microsoft.com (nothing further needed).
ACR_HOSTNAME=''
if [[ "$METRICS_COLLECTOR_IMAGE" == *.azurecr.io/* ]]; then
    ACR_HOSTNAME="${METRICS_COLLECTOR_IMAGE%%/*}"
fi

if [[ -n "$ACR_HOSTNAME" ]]; then
    ACR_SHORT_NAME="${ACR_HOSTNAME%%.*}"

    print_step "Granting 'AcrPull' on '$ACR_HOSTNAME' to the VM identity"
    ACR_RESOURCE_ID=$(az acr show \
        --name "$ACR_SHORT_NAME" \
        --subscription "$REGISTRY_SUBSCRIPTION" \
        --query id -o tsv)
    retry 10 15 az role assignment create \
        --assignee-object-id "$VM_IDENTITY_PRINCIPAL_ID" \
        --assignee-principal-type ServicePrincipal \
        --role 'AcrPull' \
        --scope "$ACR_RESOURCE_ID" \
        --subscription "$REGISTRY_SUBSCRIPTION" \
        --only-show-errors >/dev/null
fi

print_step "Installing and provisioning IoT Edge on the VM (this takes a few minutes)"
cat > "$WORK_DIR/install-iotedge.sh" <<'INSTALL_EOF'
#!/bin/bash
set -euo pipefail
# Arrives base64-encoded because run-command truncates parameter values at the
# first ';', which a connection string is full of.
CONNECTION_STRING="$(printf '%s' "$1" | base64 -d)"

export DEBIAN_FRONTEND=noninteractive
source /etc/os-release
curl -fsSL "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" -o /tmp/packages-microsoft-prod.deb
dpkg -i /tmp/packages-microsoft-prod.deb
rm -f /tmp/packages-microsoft-prod.deb

apt-get update

# aziot-edge's preinst aborts unless the `docker` group already exists, so the
# container runtime needs its own earlier transaction to get configured first.
apt-get install -y moby-engine
apt-get install -y aziot-edge

iotedge config mp --connection-string "$CONNECTION_STRING" --force
iotedge config apply

echo "IoT Edge installed and provisioned"
INSTALL_EOF

run_on_vm 'install IoT Edge' "$WORK_DIR/install-iotedge.sh" \
    --parameters "connectionString=$(printf '%s' "$DEVICE_CONNECTION_STRING" | base64 -w0)" >/dev/null

REGISTRY_CREDENTIALS='{}'
if [[ -n "$ACR_HOSTNAME" ]]; then
    print_step "Fetching an ACR access token on the VM via its managed identity"
    # The token is scoped to AcrPull only and expires in a few hours; that's fine
    # since edgeAgent only needs it for the initial pull.
    cat > "$WORK_DIR/acr-login.sh" <<'ACR_EOF'
#!/bin/bash
set -euo pipefail
ACR_HOSTNAME="$1"

export DEBIAN_FRONTEND=noninteractive
# azure-cli isn't in the packages-microsoft-prod repo already added for
# aziot-edge; it lives in its own repo.
. /etc/os-release
mkdir -p /etc/apt/keyrings
curl -sLS https://packages.microsoft.com/keys/microsoft.asc \
    | gpg --dearmor | tee /etc/apt/keyrings/microsoft.gpg >/dev/null
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/azure-cli/ ${VERSION_CODENAME} main" \
    > /etc/apt/sources.list.d/azure-cli.list

apt-get update >/dev/null
apt-get install -y azure-cli >/dev/null

for attempt in 1 2 3 4 5; do
    if az login --identity --allow-no-subscriptions --only-show-errors >/dev/null; then
        break
    fi
    if [[ "$attempt" == 5 ]]; then
        echo "Managed identity login failed after $attempt attempts." >&2
        exit 1
    fi
    echo "Managed identity login attempt $attempt failed; retrying..." >&2
    sleep 10
done

for attempt in 1 2 3 4 5; do
    if az acr login --name "${ACR_HOSTNAME%%.*}" --expose-token --only-show-errors --query accessToken -o tsv; then
        break
    fi
    if [[ "$attempt" == 5 ]]; then
        echo "ACR token acquisition failed after $attempt attempts." >&2
        exit 1
    fi
    echo "ACR token acquisition attempt $attempt failed; retrying..." >&2
    sleep 10
done
ACR_EOF

    ACR_TOKEN=$(run_on_vm 'fetch ACR token' "$WORK_DIR/acr-login.sh" --parameters "$ACR_HOSTNAME")
    REGISTRY_CREDENTIALS=$(jq -n \
        --arg key "$ACR_SHORT_NAME" \
        --arg host "$ACR_HOSTNAME" \
        --arg token "$ACR_TOKEN" \
        '{ ($key): { address: $host, username: "00000000-0000-0000-0000-000000000000", password: $token } }')
fi

print_step 'Applying the IoT Edge deployment manifest'
if [[ "$UPLOAD_MODE" == 'ingestion' ]]; then
    MODULE_ENV=$(jq -n \
        --arg hub "$HUB_RESOURCE_ID" \
        --arg dce "$DCE_ENDPOINT" \
        --arg dcr "$DCR_IMMUTABLE_ID" \
        --arg stream "$STREAM_NAME" \
        --arg freq "$SCRAPE_FREQUENCY_SECS" \
        '{
            UploadTarget: { value: "AzureMonitor" },
            ResourceID: { value: $hub },
            DataCollectionEndpoint: { value: $dce },
            DataCollectionRuleId: { value: $dcr },
            DataCollectionStreamName: { value: $stream },
            ScrapeFrequencyInSecs: { value: $freq }
        }')
    MODULE_CREATE_OPTIONS='{}'
else
    MODULE_ENV=$(jq -n \
        --arg hub "$HUB_RESOURCE_ID" \
        --arg workspaceId "$WORKSPACE_CUSTOMER_ID" \
        --arg sharedKey "$WORKSPACE_SHARED_KEY" \
        --arg freq "$SCRAPE_FREQUENCY_SECS" \
        '{
            UploadTarget: { value: "AzureMonitor" },
            ResourceID: { value: $hub },
            LogAnalyticsWorkspaceId: { value: $workspaceId },
            LogAnalyticsSharedKey: { value: $sharedKey },
            ScrapeFrequencyInSecs: { value: $freq }
        }')
    MODULE_CREATE_OPTIONS='{}'
fi

jq -n \
    --arg agentImage 'mcr.microsoft.com/azureiotedge-agent:1.5' \
    --arg hubImage 'mcr.microsoft.com/azureiotedge-hub:1.5' \
    --arg mcImage "$METRICS_COLLECTOR_IMAGE" \
    --arg sensorImage "$SIMULATED_SENSOR_IMAGE" \
    --argjson mcEnv "$MODULE_ENV" \
    --argjson mcCreateOptions "$MODULE_CREATE_OPTIONS" \
    --argjson registryCredentials "$REGISTRY_CREDENTIALS" \
    '{
        modulesContent: {
            "$edgeAgent": {
                "properties.desired": {
                    schemaVersion: "1.1",
                    runtime: {
                        type: "docker",
                        settings: { minDockerVersion: "v1.25", loggingOptions: "", registryCredentials: $registryCredentials }
                    },
                    systemModules: {
                        edgeAgent: {
                            type: "docker",
                            settings: { image: $agentImage, createOptions: "{}" }
                        },
                        edgeHub: {
                            type: "docker",
                            status: "running",
                            restartPolicy: "always",
                            settings: {
                                image: $hubImage,
                                createOptions: ({ HostConfig: { PortBindings: {
                                    "5671/tcp": [ { HostPort: "5671" } ],
                                    "8883/tcp": [ { HostPort: "8883" } ]
                                } } } | tojson)
                            }
                        }
                    },
                    modules: {
                        metricsCollector: {
                            version: "1.0",
                            type: "docker",
                            status: "running",
                            restartPolicy: "always",
                            settings: { image: $mcImage, createOptions: ($mcCreateOptions | tojson) },
                            env: $mcEnv
                        },
                        SimulatedTemperatureSensor: {
                            version: "1.0",
                            type: "docker",
                            status: "running",
                            restartPolicy: "always",
                            settings: { image: $sensorImage, createOptions: "{}" }
                        }
                    }
                }
            },
            "$edgeHub": {
                "properties.desired": {
                    schemaVersion: "1.1",
                    routes: {
                        SimulatedTemperatureSensorToIoTHub: "FROM /messages/modules/SimulatedTemperatureSensor/outputs/* INTO $upstream"
                    },
                    storeAndForwardConfiguration: { timeToLiveSecs: 7200 }
                }
            }
        }
    }' > "$WORK_DIR/deployment.json"

az iot edge set-modules \
    --hub-name "$IOT_HUB_NAME" \
    --device-id "$DEVICE_ID" \
    --content "$WORK_DIR/deployment.json" \
    --auth-type login \
    --only-show-errors >/dev/null

if [[ "$UPLOAD_MODE" == 'ingestion' ]]; then
    MODE_DETAILS="Custom table:          $TABLE_NAME
DCE ingestion URL:     $DCE_ENDPOINT
DCR immutable ID:      $DCR_IMMUTABLE_ID
DCR stream name:       $STREAM_NAME
Module identity:       VM system-assigned managed identity ($VM_IDENTITY_PRINCIPAL_ID)"
    QUERY_TABLE="$TABLE_NAME"
    MODE_NOTES="  - Auth is the VM's system-assigned managed identity; no Entra app registration,
    certificate, or secret was created. Deleting the resource group removes it."
else
    MODE_DETAILS="Destination table:     InsightsMetrics (built-in)
Workspace ID:          $WORKSPACE_CUSTOMER_ID
Auth:                  workspace shared key (retiring 14 Sept 2026)"
    QUERY_TABLE='InsightsMetrics'
    MODE_NOTES="  - The workspace shared key is stored in the module's env vars, which are readable
    from the IoT Hub module twin. This is inherent to the legacy path and is one of
    the reasons it is being replaced.
  - If InsightsMetrics stays empty, the workspace may need the VM Insights or
    Container Insights solution enabled before that table accepts data."
fi

cat <<SUMMARY_EOF

$(printf '\033[0;32m%s\033[0m' '=== Provisioning complete ===')

Upload mode:           $UPLOAD_MODE
Resource group:        $RESOURCE_GROUP
Log Analytics:         $WORKSPACE_NAME
$MODE_DETAILS
IoT Hub:               $IOT_HUB_NAME
Edge device:           $DEVICE_ID
VM:                    $VM_NAME
VM public IP:          $VM_PUBLIC_IP
Module image:          $METRICS_COLLECTOR_IMAGE
Sensor image:          $SIMULATED_SENSOR_IMAGE

Allow a few minutes for the module to pull, start, scrape, and upload
(first upload lands roughly ScrapeFrequencyInSecs=$SCRAPE_FREQUENCY_SECS seconds after start,
and Log Analytics ingestion adds its own latency of a minute or more).

SSH to the VM:
  ssh azureuser@$VM_PUBLIC_IP

Check module status and logs:
  az vm run-command invoke -g $RESOURCE_GROUP -n $VM_NAME \\
    --command-id RunShellScript --scripts 'iotedge list; iotedge logs metricsCollector --tail 50'
Query the ingested metrics:
  az monitor log-analytics query \\
    --workspace \$(az monitor log-analytics workspace show -g $RESOURCE_GROUP -n $WORKSPACE_NAME --query customerId -o tsv) \\
    --analytics-query "$QUERY_TABLE | take 20"

Tear everything down:
  az group delete --name $RESOURCE_GROUP --yes --no-wait

Security notes (this is a disposable test environment):
  - The device connection string was passed to the VM via 'az vm run-command', which
    may surface in activity logs. Fine for a throwaway hub; do not reuse this pattern
    for long-lived credentials.
$MODE_NOTES

SUMMARY_EOF
