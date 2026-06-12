#! /bin/bash

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################

SCRIPT_NAME=$(basename $0)

POOL_ID=123
API_VER=6.0

BUILD_ID=

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
usage()
{
    echo "$SCRIPT_NAME [options]"
    echo "This script expects a variable 'PAT' to be availalable in the environment when running."
    echo "This 'PAT' is a DevOps API PAT for booking the agents. This PAT must have permissions to read builds in devops."
    echo ""
    echo "options"
    echo " --build-id      Devops build id used to tag locked agents."
    echo " --help          Print this help message and exit."
    exit 1;
}

print_help_and_exit()
{
    echo "Run $SCRIPT_NAME --help for more information."
    exit 1
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args()
{
    save_next_arg=0
    for arg in $@
    do
        if [ $save_next_arg -eq 1 ]; then
            BUILD_ID="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--help" ) usage;;
                "--build-id" ) save_next_arg=1;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${PAT} ]]; then
        echo "Personal Access Token must be set in the environment"
        print_help_and_exit
    fi

    if [[ -z ${BUILD_ID} ]]; then
        echo "Build id is a required parameter."
        print_help_and_exit
    fi

    echo "Curl version: $(curl --version)"
    echo
}

###############################################################################
# Main Script Execution
###############################################################################

process_args $@

agentsInfo=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents?includeCapabilities=true&api-version=$API_VER")
if [ $? -ne 0 ]; then
    echo "Failed to fetch agents info"
    exit 1
fi

lockedAgents=($(
    echo $agentsInfo |
    jq -r --arg build_id "$BUILD_ID" '.value | .[] | select(.userCapabilities.status != null) | select(.userCapabilities.status | startswith("locked_\($build_id)")) | .id'
))

echo "Found locked agents:"
printf '%s\n' "${lockedAgents[@]}"

for agentId in "${lockedAgents[@]}"; do
    echo "Unlocking agent: $agentId"

    # Filter for user capabilities, access "status" field and remove buildId suffix
    agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
    if [ $? -ne 0 ]; then
        echo "Failed to fetch capabilities for agent $agentId"
        exit 1
    fi

    newAgentUserCapabilities=$(echo $agentCapabilities | jq '.userCapabilities | .status |= "unlocked"')

    # Update the user capability on the agent pool for this agent
    responseCapabilities=$(curl -s -f -u :$PAT \
        --request PUT "https://msazure.visualstudio.com/_apis/distributedtask/pools/$POOL_ID/agents/$agentId/usercapabilities" \
        -H "Content-Type:application/json" \
        -H "Accept: application/json;api-version=5.0;" \
        --max-time 15 \
        --retry 10 \
        --retry-delay 0 \
        --retry-max-time 80 \
        --retry-connrefused \
        --data @<(cat <<EOF
$newAgentUserCapabilities
EOF
))

    if [ $? -ne 0 ]; then
        echo "Failed to update capabilities for agent $agentId"
        exit 1
    fi
done
