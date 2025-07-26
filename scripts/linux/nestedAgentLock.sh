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
ASSIGN_LEVELS='false'

ARCH=
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
    echo " --assign-levels     Add level information to the lock status for each agent."
    echo " --arch              Architecture of the agents to book. Valid values are x64, arm64. If not specified, agents for both architectures will be locked."
    echo " --build-id          Devops build ID used to tag locked agents."
    echo " --group             Agent Group from which we want to book agents. This agent group is a capability named 'agent-group'."
    echo " --help              Print this help message and exit."
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
            ARCH="${arg,,}"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            BUILD_ID="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 3 ]; then
            AGENT_GROUP="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "--help" ) usage;;
                "--assign-levels" ) ASSIGN_LEVELS='true';;
                "--arch" ) save_next_arg=1;;
                "--build-id" ) save_next_arg=2;;
                "--group" ) save_next_arg=3;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z "$PAT" ]]; then
        echo "Personal Access Token must be set in the environment"
        print_help_and_exit
    fi

    if [[ -n "$ARCH" && "$ARCH" != "x64" && "$ARCH" != "arm64" ]]; then
        echo "Invalid architecture specified: $ARCH. Valid values are x64, arm64."
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
    local agents=("$@")
    # format is 'id=tag', remove tags keeping only agent IDs
    for i in "${!agents[@]}"; do
        agents[$i]="${agents[$i]%%=*}"
    done

    for agentId in "${agents[@]}"; do
        local agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        local agentName=$(echo $agentCapabilities | jq -r '.systemCapabilities."Agent.Name"')
        local lockStatus=$(echo $agentCapabilities | jq -r '.userCapabilities.status')

        echo "Locked agent: $agentName [id=$agentName, status=$lockStatus]"
    done
}

function update_user_capabilities() {
    local agentId=$1
    local newAgentUserCapabilities=$2

    # Update the user capability on the agent pool for this agent
    local responseCapabilities=$(curl -s -f -u :$PAT \
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
    local responseUserCapabilities=$(echo $responseCapabilities | jq '.userCapabilities')

    if [ "$responseUserCapabilities" != "$newAgentUserCapabilities" ]
    then
        echo "Capabilities were not updated properly. Dumping response below." >&2
        echo "$responseCapabilities" >&2
    fi
}

function attempt_agent_lock() {
    local agents=("$@")
    local amend_tags=()
    # format is 'id=tag', move tags into their own array
    for i in "${!agents[@]}"; do
        amend_tags[$i]="${agents[$i]#*=}"
        agents[$i]="${agents[$i]%%=*}"
    done

    # Lock the agents by updating the user capability 'status'.
    for i in "${!agents[@]}"; do
        local agentCapabilityTag="locked_${BUILD_ID}$([ -n "${amend_tags[$i]}" ] && echo "_${amend_tags[$i]}")"
        local agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/${agents[$i]}?includeCapabilities=true&api-version=$API_VER")
        local newAgentUserCapabilities=$(echo $agentCapabilities | jq --arg tag "$agentCapabilityTag" '.userCapabilities | .status |= $tag')

        update_user_capabilities "${agents[$i]}" "$newAgentUserCapabilities"
    done

    # Wait a while then check to make sure there were no overlapping bookings.
    sleep 10
    local agentsAllLockedCorrectly=true
    for agentId in "${agents[@]}"; do
        local agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        local lockStatus=$(echo $agentCapabilities | jq -r '.userCapabilities | .status')

        if [ ! "$lockStatus" =~ ^"locked_${BUILD_ID}" ]; then
            agentsAllLockedCorrectly=false
            break
        fi
    done

    echo $agentsAllLockedCorrectly
}

function unlock_agents() {
    local agents=("$@")
    # format is 'id=tag', remove tags keeping only agent IDs
    for i in "${!agents[@]}"; do
        agents[$i]="${agents[$i]%%=*}"
    done

    for agentId in "${agents[@]}"; do
        local agentCapabilities=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        local lockStatus=$(echo $agentCapabilities | jq '.userCapabilities | .status')

        if [ "$lockStatus" =~ ^"locked_${BUILD_ID}" ]; then
            echo "Unlocking agent $agentId"

            local newAgentUserCapabilities=$(echo $agentCapabilities | jq '.userCapabilities | .status |= "unlocked"')
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

archMsg=''
if [[ -n "$ARCH" ]]; then
    archMsg="$AGENTS_PER_ARCH $ARCH agents"
else
    archMsg="$AGENTS_PER_ARCH x64 agents and $AGENTS_PER_ARCH arm64 agents"
fi

startSeconds=$((SECONDS))
endSeconds=$((SECONDS + $TIMEOUT_SECONDS))
while true && [ $((SECONDS)) -lt $endSeconds ]; do
    # Wait to retry locking agents.
    # Random delay to avoid multiple instances of the script thrashing.
    sleep $[ ( $RANDOM % 10 ) + 60 ]s

    echo "Attempting to lock $archMsg from the agent group $AGENT_GROUP..."
    agentsInfo=$(curl -s -f -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents?includeCapabilities=true&api-version=$API_VER")
    
    if [[ -z "$ARCH" || "$ARCH" == "x64" ]]; then
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

        echo "Found these unlocked x64 agents:"
        echo ${unlockedX64Agents[@]}
    else
        unlockedX64Agents=()
    fi

    if [[ -z "$ARCH" || "$ARCH" == "arm64" ]]; then
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

        echo "Found these unlocked arm64 agents:"
        echo ${unlockedArm64Agents[@]}
    else
        unlockedArm64Agents=()
    fi

    # Check if we have enough agents based on architecture filter
    x64Needed=$([[ -z "$ARCH" || "$ARCH" == "x64" ]] && echo $AGENTS_PER_ARCH || echo 0)
    arm64Needed=$([[ -z "$ARCH" || "$ARCH" == "arm64" ]] && echo $AGENTS_PER_ARCH || echo 0)

    if [ ${#unlockedX64Agents[*]} -ge $x64Needed ] && [ ${#unlockedArm64Agents[*]} -ge $arm64Needed ]; then
        # If we have enough agents of the required architectures, get random agents and book them all.
        filteredX64Agents=()
        filteredArm64Agents=()
        
        if [ $x64Needed -gt 0 ]; then
            shuffledUnlockedX64Agents=($(shuf -e "${unlockedX64Agents[@]}"))
            filteredX64Agents=(${shuffledUnlockedX64Agents[@]:0:$AGENTS_PER_ARCH})
            if [ "$ASSIGN_LEVELS" == "true" ]; then
                for i in "${!filteredX64Agents[@]}"; do
                    filteredX64Agents[$i]="${filteredX64Agents[$i]}=L$((5 - $i))"
                done
            fi
        fi
        
        if [ $arm64Needed -gt 0 ]; then
            shuffledUnlockedArm64Agents=($(shuf -e "${unlockedArm64Agents[@]}"))
            filteredArm64Agents=(${shuffledUnlockedArm64Agents[@]:0:$AGENTS_PER_ARCH})
            if [ "$ASSIGN_LEVELS" == "true" ]; then
                for i in "${!filteredArm64Agents[@]}"; do
                    filteredArm64Agents[$i]="${filteredArm64Agents[$i]}=L$((5 - $i))"
                done
            fi
        fi

        if [ ${#filteredX64Agents[@]} -gt 0 ]; then
            echo "Locking these x64 agents:"
            echo ${filteredX64Agents[@]}
        fi
        if [ ${#filteredArm64Agents[@]} -gt 0 ]; then
            echo "Locking these arm64 agents:"
            echo ${filteredArm64Agents[@]}
        fi

        x64AgentsAllLockedCorrectly=true
        arm64AgentsAllLockedCorrectly=true
        
        if [ ${#filteredX64Agents[@]} -gt 0 ]; then
            x64AgentsAllLockedCorrectly=$(attempt_agent_lock "${filteredX64Agents[@]}")
        fi
        if [ ${#filteredArm64Agents[@]} -gt 0 ]; then
            arm64AgentsAllLockedCorrectly=$(attempt_agent_lock "${filteredArm64Agents[@]}")
        fi

        # If something went wrong and we don't have all the agents locked, release the ones we still have booked.
        # Else set the agent names as output to be used in the pipeline downstream.
        if [ $x64AgentsAllLockedCorrectly = false ] || [ $arm64AgentsAllLockedCorrectly = false ]; then
            echo "Conflicting agent lock detected. Unlocking all booked agents made here."
            allFilteredAgents=("${filteredX64Agents[@]}" "${filteredArm64Agents[@]}")
            if [ ${#allFilteredAgents[@]} -gt 0 ]; then
                unlock_agents "${allFilteredAgents[@]}"
            fi
        else
            if [ ${#filteredX64Agents[@]} -gt 0 ]; then
                echo "x64 agents:"
                print_agent_names "${filteredX64Agents[@]}"
            fi
            if [ ${#filteredArm64Agents[@]} -gt 0 ]; then
                echo "arm64 agents:"
                print_agent_names "${filteredArm64Agents[@]}"
            fi
            echo "Successfully locked agents"
            exit 0
        fi
    fi

    echo "Failed to acquire $archMsg from the pool. Will retry soon."
done

exit 1
