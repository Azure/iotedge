#! /bin/bash

###############################################################################
# This script is used to "lock" agents for nested tests. When we lock an agent,
# we alter the "status" user capability on the agent by appending the build ID
# of the current run.
#
# Locking agents is not an atomic operation so this script has to account for
# race conditions.
#
# This script lock 3 x64 agents and 3 arm64 agents, then waits a bit for the
# booked agents' lock-state to become non-volatile. If all agents are still
# locked with the given build ID, then we know we can proceed safely.
#
# If instead, another build somehow booked some of our previously-booked agents
# after we did, we can unlock all the agents we still have booked. Then wait a
# random duration to avoid multiple instances of the script thrashing on the
# next booking attempt.
###############################################################################

set -euo pipefail

###############################################################################
# Define Environment Variables
###############################################################################

SCRIPT_NAME=$(basename $0)

POOL_ID=123 # Devops agent pool id corresponding to "Azure-IoT-Edge-Core"
API_VER=6.0
TIMEOUT_SECONDS=$((60*60*3)) # 3 hours
AGENTS_PER_ARCH=3 # Number of agents we want to book per architecture

AGENT_GROUP=
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
    echo " --build-id      Devops build ID used to tag locked agents."
    echo " --group         Agent Group from which we want to book agents. This agent group is a capability named 'agent-group'."
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
        elif [ $save_next_arg -eq 2 ]; then
            AGENT_GROUP="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--help" ) usage;;
                "--build-id" ) save_next_arg=1;;
                "--group" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "$PAT" ]]; then
        echo "Personal Access Token must be set in the environment"
        print_help_and_exit
    fi

    if [[ -z "$AGENT_GROUP" ]]; then
        echo "Agent Group is a required parameter."
        print_help_and_exit
    fi

    if [[ -z "$BUILD_ID" ]]; then
        echo "Build id is a required parameter."
        print_help_and_exit
    fi

    echo "Curl version: $(curl --version)"
    echo
}

###############################################################################
# Main Script Execution
###############################################################################

function print_agent_names() {
    agents=("$@")

    for i in "${!agents[@]}"; do
        agentId="${agents[$i]}"

        agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        agentName=$(echo $agentCapabilities | jq -r '.systemCapabilities."Agent.Name"')
        lockStatus=$(echo $agentCapabilities | jq -r '.userCapabilities.status')

        echo "Locked agent: $agentName [id=$agentName, status=$lockStatus]"
    done
}

function update_user_capabilities() {
    agentId=$1
    newAgentUserCapabilities=$2

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

    # Validate the capability update was successful
    responseUserCapabilities=$(echo $responseCapabilities | jq '.userCapabilities')

    if [ "$responseUserCapabilities" != "$newAgentUserCapabilities" ]
    then
        echo "Capabilities were not updated properly. Dumping response below." >&2
        echo "$responseCapabilities" >&2
    fi
}

function attempt_agent_lock() {
    agents=("$@")

    # Lock the agents by updating the user capability 'status'.
    for agentId in "${agents[@]}"; do
        agentCapabilityTag="locked_${BUILD_ID}"
        agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        newAgentUserCapabilities=$(echo $agentCapabilities | jq --arg tag "$agentCapabilityTag" '.userCapabilities | .status |= $tag')

        update_user_capabilities "$agentId" "$newAgentUserCapabilities"
    done

    # Wait a while then check to make sure there were no overlapping bookings.
    sleep 10
    agentsAllLockedCorrectly=true
    for agentId in "${agents[@]}"; do
        agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        lockStatus=$(echo $agentCapabilities | jq -r '.userCapabilities | .status')

        if [ "$lockStatus" != "locked_${BUILD_ID}" ]; then
            agentsAllLockedCorrectly=false
            break
        fi
    done

    echo $agentsAllLockedCorrectly
}

function unlock_agents() {
    agents=("$@")

    for agentId in "${agents[@]}"; do
        agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        lockStatus=$(echo $agentCapabilities | jq '.userCapabilities | .status')

        if [ "$lockStatus" == "locked_${BUILD_ID}" ]; then
            echo "Unlocking agent $agentId"

            newAgentUserCapabilities=$(echo $agentCapabilities | jq '.userCapabilities | .status |= "unlocked"')
            update_user_capabilities "$agentId" "$newAgentUserCapabilities"
        fi
    done
}

process_args $@

# Install pre-requisite 'jq'
CMD=jq
echo "Validating jq"
if ! command -v $CMD &>/dev/null; then
    echo "Command '$CMD' not found, Installing '$CMD'"
    sudo add-apt-repository universe
    sudo apt-get update
    sudo apt-get install -y $CMD
fi
echo "Done validating jq"
echo

echo "Validating devops API interface"
echo "Checking for agents from the agent group $AGENT_GROUP..."
agentsInfo=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents?includeCapabilities=true&api-version=$API_VER")
agents=($(echo $agentsInfo | jq -r --arg group "$AGENT_GROUP" '.value[] | select(.userCapabilities."agent-group"==$group) | .id'))
echo ${#agents[@]}
if [ ${#agents[@]} -eq 0 ]; then
    echo "Problem interfacing with Devops API to retrieve agent data. Recommend checking PAT expiry."
    exit 1
else
    echo "Successfully interfaced with Devops API to retrieve agent data."
fi
echo "Done validating devops API interface"
echo

startSeconds=$((SECONDS))
endSeconds=$((SECONDS + $TIMEOUT_SECONDS))
while true && [ $((SECONDS)) -lt $endSeconds ]; do
    # Wait to retry locking agents.
    # Random delay to avoid multiple instances of the script thrashing.
    sleep $[ ( $RANDOM % 10 ) + 60 ]s

    echo "Attempting to lock $AGENTS_PER_ARCH x64 agents and $AGENTS_PER_ARCH arm64 agents from the agent group $AGENT_GROUP..."
    agentsInfo=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents?includeCapabilities=true&api-version=$API_VER")
    
    # Get unlocked x64 agents
    unlockedX64Agents=($(
        echo $agentsInfo |
        jq -r --arg group "$AGENT_GROUP" '
            .value[] | select(
                .enabled==true and
                .status=="online" and
                .userCapabilities.status=="unlocked" and
                .systemCapabilities."Agent.OSArchitecture"=="X64" and
                .userCapabilities."agent-group" == $group
            ).id
        '
    ))

    # Get unlocked arm64 agents
    unlockedArm64Agents=($(
        echo $agentsInfo |
        jq -r --arg group "$AGENT_GROUP" '
            .value[] | select(
                .enabled==true and
                .status=="online" and
                .userCapabilities.status=="unlocked" and
                .systemCapabilities."Agent.OSArchitecture"=="ARM64" and
                .userCapabilities."agent-group" == $group
            ).id
        '
    ))

    echo "Found these unlocked x64 agents:"
    echo ${unlockedX64Agents[@]}
    echo "Found these unlocked arm64 agents:"
    echo ${unlockedArm64Agents[@]}

    if [ ${#unlockedX64Agents[*]} -ge $AGENTS_PER_ARCH ] && [ ${#unlockedArm64Agents[*]} -ge $AGENTS_PER_ARCH ]; then
        # If we have enough agents of both architectures, get random agents and book them all.
        shuffledUnlockedX64Agents=($(shuf -e "${unlockedX64Agents[@]}"))
        filteredX64Agents=(${shuffledUnlockedX64Agents[@]:0:$AGENTS_PER_ARCH})
        
        shuffledUnlockedArm64Agents=($(shuf -e "${unlockedArm64Agents[@]}"))
        filteredArm64Agents=(${shuffledUnlockedArm64Agents[@]:0:$AGENTS_PER_ARCH})

        echo "Locking these x64 agents:"
        echo ${filteredX64Agents[@]}
        echo "Locking these arm64 agents:"
        echo ${filteredArm64Agents[@]}

        x64AgentsAllLockedCorrectly=$(attempt_agent_lock "${filteredX64Agents[@]}")
        arm64AgentsAllLockedCorrectly=$(attempt_agent_lock "${filteredArm64Agents[@]}")

        # If something went wrong and we don't have all the agents locked, release the ones we still have booked.
        # Else set the agent names as output to be used in the pipeline downstream.
        if [ $x64AgentsAllLockedCorrectly = false ] || [ $arm64AgentsAllLockedCorrectly = false ]; then
            echo "Conflicting agent lock detected. Unlocking all booked agents made here."
            allFilteredAgents=("${filteredX64Agents[@]}" "${filteredArm64Agents[@]}")
            unlock_agents "${allFilteredAgents[@]}"
        else
            echo "x64 agents:"
            print_agent_names "${filteredX64Agents[@]}"
            echo "arm64 agents:"
            print_agent_names "${filteredArm64Agents[@]}"
            echo "Successfully locked agents"
            exit 0
        fi
    fi

    echo "Failed to acquire $AGENTS_PER_ARCH x64 agents and $AGENTS_PER_ARCH arm64 agents from pool. Will retry soon."
done

exit 1