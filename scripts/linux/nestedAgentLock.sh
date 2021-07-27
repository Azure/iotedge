#! /bin/bash

###############################################################################
# This script is used to "lock" agents for nested tests. When we lock an agent,
# we alter the agent capability named "status" by appending the build id of
# the current run.

# Locking agents is not an atomic operation so this script has to account for
# race conditions.
#
# We will attempt to lock 3 agents, then wait a bit for the booked agents
# lock-state to become non-volatile. If all agents are still locked with the 
# build id this script is using, then we know we can proceed safely. 
#
# If instead, another build somehow booked some of our previously-booked agents
# after we did, we can unlock all the agents we still have booked. Then wait a
# random duration to avoid multiple instances of the script thrashing on the
# next booking attempt.
###############################################################################

set -e

###############################################################################
# Define Environment Variables
###############################################################################

SCRIPT_NAME=$(basename $0)

POOL_ID=123 # Devops agent pool id corresponding to "Azure-IoT-Edge-Core"
API_VER=6.0
AGENTS_NEEDED=3
TIMEOUT_SECONDS=300

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
    echo " -a                 Agent Group from which we want to book agents. This agent group is a capability named 'agent-group'."
    echo " -b                 Devops build id used to tag locked agents."
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
            AGENT_GROUP="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            BUILD_ID="$arg"
            save_next_arg=0
        else
            case "$arg" in
                "-h" ) usage;;
                "-a" ) save_next_arg=1;;
                "-b" ) save_next_arg=2;;
                * ) usage;;
            esac
        fi
    done

    if [[ -z ${PAT} ]]; then
        echo "Personal Access Token is a required parameter"
        print_help_and_exit
    fi

    if [[ -z ${AGENT_GROUP} ]]; then
        echo "Agent Group is a required parameter."
        print_help_and_exit
    fi

    if [[ -z ${BUILD_ID} ]]; then
        echo "Build id is a required parameter."
        print_help_and_exit
    fi

    echo $PAT
}

###############################################################################
# Main Script Execution
###############################################################################

function set_devops_output_vars() {
    agents=("$@")
    outputAgentNames=(l5AgentName l4AgentName l3AgentName)

    echo "Setting devops vars for agent names. Needed for future unlock."

    for i in "${!agents[@]}"; do
        agentId="${agents[$i]}"

        agentCapabilities=$(curl -s -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        agentName=$(echo $agentCapabilities | jq '.systemCapabilities."Agent.Name"' | tr -d '[], "')

        echo "Setting devops var for agent name: $agentName"
        echo "##vso[task.setvariable variable=${outputAgentNames[$i]};isOutput=true]$agentName"
    done
}

function update_capabilities() {
    agentId=$1
    newAgentUserCapabilities=$2

    # Update the user capability on the agent pool for this agent
    responseCapabilities=$(curl -s -u :$PAT \
--request PUT "https://msazure.visualstudio.com/_apis/distributedtask/pools/$POOL_ID/agents/$agentId/usercapabilities" \
-H "Content-Type:application/json" \
-H "Accept: application/json;api-version=5.0;" \
--data @<(cat <<EOF
$newAgentUserCapabilities
EOF
))
    # Validate the capability update was successful
    responseUserCapabilities=$(echo $responseCapabilities | jq '.userCapabilities')

    if [ "$responseUserCapabilities" != "$newAgentUserCapabilities" ]
    then
        echo "Capabilities were not updated properly. This will be retried."
    fi
}

function attempt_agent_lock() {
    agents=("$@")

    # Lock the agents.
    for agentId in "${agents[@]}"; do
        agentCapabilities=$(curl -s -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        newAgentUserCapabilities=$(echo $agentCapabilities | jq '.userCapabilities | (.["status"]) |= sub("$"; '\"_$BUILD_ID\"')')

        update_capabilities "$agentId" "$newAgentUserCapabilities"
    done

    # Wait a while then check to make sure there were no overlapping bookings.
    sleep 10
    agentsAllLockedCorrectly=true
    for agentId in "${filteredAgents[@]}"; do
        agentCapabilities=$(curl -s -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        lockStatus=$(echo $agentCapabilities | jq '.userCapabilities | .status' | tr -d '[], "')

        if [ $lockStatus != "unlocked_$BUILD_ID" ]; then
            agentsAllLockedCorrectly=false
            break
        fi
    done

    echo $agentsAllLockedCorrectly
}

function unlock_agents() {
    agents=("$@")

    for agentId in "${agents[@]}"; do
        agentCapabilities=$(curl -s -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents/$agentId?includeCapabilities=true&api-version=$API_VER")
        lockStatus=$(echo $agentCapabilities | jq '.userCapabilities | .status')

        if [ $lockStatus = '"unlocked_$(Build.BuildId)"' ]; then
            echo "Unlocking agent $agentId"

            newAgentUserCapabilities=$(echo $agentCapabilities | jq '.userCapabilities | (.["status"]) |= "unlocked"')
            update_capabilities "$agentId" "$newAgentUserCapabilities"
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

startSeconds=$((SECONDS))
endSeconds=$((SECONDS + $TIMEOUT_SECONDS))
backoffSeconds=1
while true && [ $((SECONDS)) -lt $endSeconds ]; do
    # Wait 1-10 seconds to retry locking agents.
    # Random delay to avoid multiple instances of the script thrashing.
    sleep $[ ( $RANDOM % 10 )  + $backoffSeconds ]s
    backoffSeconds=$(($backoffSeconds * 2))

    echo "Attempting to lock $AGENTS_NEEDED agents from the agent group $AGENT_GROUP..."
    agentsInfo=$(curl -s -u :$PAT --request GET "https://dev.azure.com/msazure/_apis/distributedtask/pools/$POOL_ID/agents?includeCapabilities=true&api-version=$API_VER")
    unlockedAgents=($(echo $agentsInfo | jq '.value | .[] | select(.userCapabilities.status=="unlocked" and .userCapabilities."agent-group"=='\"$AGENT_GROUP\"') | .id' | tr -d '[], "'))

    echo "Found these unlocked agents:"
    echo ${unlockedAgents[@]}

    if [ ${#unlockedAgents[*]} -ge $AGENTS_NEEDED ]; then
        # If we have enough agents, get random agents and book them all.
        shuffledUnlockedAgents=($(shuf -e "${unlockedAgents[@]}"))
        filteredAgents=(${shuffledUnlockedAgents[@]:0:$AGENTS_NEEDED})

        echo "Locking these agents:"
        echo ${filteredAgents[@]}

        agentsAllLockedCorrectly=$(attempt_agent_lock "${filteredAgents[@]}")

        # If something went wrong and we don't have all the agents locked, release the ones we still have booked.
        # Else set the agent names as output to be used in the pipeline downstream.
        if [ $agentsAllLockedCorrectly = false ]; then
            echo "Conflicting agent lock detected. Unlocking all booked agents made here."
            unlock_agents "${filteredAgents[@]}"

        else
            echo "Successfully locked agents"
            set_devops_output_vars "${filteredAgents[@]}"
            exit 0
        fi
    fi

    echo "Failed to acquire $AGENTS_NEEDED agents from pool. Will retry soon."
done

exit 1