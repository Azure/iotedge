#!/bin/bash

check_required_variables()
{
    # BEARWASHERE -- change from "return" to "exit" once done debugging
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; return 1; }
    [[ -z "$BRANCH_NAME" ]] && { echo "\$BRANCH_NAME is undefined"; return 1; }   
}

#    $1 is GitHub API endpoint
send_github_request()
{
    # BEARWASHERE -- change from "return" to "exit" once done debugging
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; return 1; }

    # $1 - Repo (Azure/azure-iotedge)
    # $2 - Endpoint
    url="https://api.github.com/repos/$1/$2"
    header_content="Accept:application/vnd.github.v3+json"
    header_auth="Authorization:token $GITHUB_PAT"
    content=$(curl -X GET -H "$header_content" -H "$header_auth" "$url")
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

    content=$(send_github_request "Azure/azure-iotedge" "releases")
    echo $content | jq '.[].name'
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

    lastest_version=$(get_latest_release_per_branch_name)
}
