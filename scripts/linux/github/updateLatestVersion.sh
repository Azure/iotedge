#!/bin/bash

check_required_variables()
{
    # BEARWASHERE -- change from "return" to "exit" once done debugging
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; return 1; }
    [[ -z "$BRANCH_NAME" ]] && { echo "\$BRANCH_NAME is undefined"; return 1; }   
    [[ -z "$IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; return 1; }   
}


send_github_request()
{
    # BEARWASHERE -- change from "return" to "exit" once done debugging
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; return 1; }

    # $1 - Repo (Azure/azure-iotedge)
    # $2 - Endpoint
    url="https://api.github.com/repos/$1/$2"
    header_content="Accept:application/vnd.github.v3+json"
    header_auth="Authorization:token $GITHUB_PAT"
    content=$(curl -s -X GET -H "$header_content" -H "$header_auth" "$url")
    echo $content
}


#######################################
# NAME: 
#    get_latest_release_per_branch_name
# DESCRIPTION:
#    Get latest release version given the branch name
# GLOBALS:
#    A_STRING_PREFIX
# ARGUMENTS:
#    String to print
# OUTPUTS:
#    Write String to stdout
# RETURN:
#    0 if print succeeds, non-zero on error.
#######################################
get_latest_release_per_branch_name()
{
    check_required_variables

    # $BRANCH_NAME="refs/heads/release/1.1"  (Build.SourceBranch)
    # Get the MAJOR.MINOR version
    branchVersion=$(echo ${BRANCH_NAME##*/})

    # Get the list of released version
    content=$(send_github_request "Azure/azure-iotedge" "releases")
    jqQuery=".[].name | select(startswith(\"$branchVersion\"))"
    versionList=$(echo $content | jq "$jqQuery")
    
    # Beware of the "N.N.N~rcN"
    # We are not gonna handle the 'rc' in the tag
    echo $versionList | tr " " "\n"  | sort --version-sort -r | head -1 | tr -d '"'
}


version_sanity_check()
{
    # $1 - Proposed version

    # BEARWASHERE -- TODO: Make $2 optional for $latestReleasedVersion

    # Use the sort to compare, if the first result from sort() is not the input, then return false.
    latestReleasedVersion=$(get_latest_release_per_branch_name)
    higherVersion=$(echo "$latestReleasedVersion $1" | tr " " "\n"  | sort --version-sort -r | head -1)
    if [[ "$higherVersion" == "$latestReleasedVersion" ]]; then
        echo "FAILED: The proposed version ($1) cannot have a lower version value than the latest released version ($latestReleasedVersion)"
        exit 1;
    else
        echo "PASSED: version sanity check"
    fi
}



# Simple logic: 
# Check the current branch of the current source code
# 1. Depending on 1.1 or 1.2, correspondingly update the necessary version file. 
# 2. Open a PR
# 3. Push and merge the commit
# 4. Tag the repository
update_latest_version_json()
{
    check_required_variables

    latestReleasedVersion=$(get_latest_release_per_branch_name)

    #sudo chmod +x $(Build.SourcesDirectory)/scripts/linux/publishReleasePackages.sh
    # BEARWASHERE -- Hmmm... We need to checkout azure-iotedge repo here to modify it. Let's take a look tmr
    $IOTEDGE_REPO_PATH

    #The /s for source that we can use
    # $(Build.SourcesDirectory)/azure-iotedge
    # $(Build.SourcesDirectory)/iotedge
}