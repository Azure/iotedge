#!/bin/bash

#######################################
# NAME: 
#    check_required_variables
# DESCRIPTION:
#    Check the commonly used variable in the script if they are provided.
#    
# GLOBALS:
#    GITHUB_PAT
#    BRANCH_NAME
#    IOTEDGE_REPO_PATH
# ARGUMENTS:
#    None
# OUTPUTS:
#    Error message
# RETURN:
#    1 if the variable is not provided otherwise return 0.
#######################################
check_required_variables()
{
    # BEARWASHERE -- change from "return" to "exit" once done debugging
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; return 1; }
    [[ -z "$BRANCH_NAME" ]] && { echo "\$BRANCH_NAME is undefined"; return 1; }   
    [[ -z "$IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; return 1; }
}


#######################################
# NAME: 
#    send_github_request
# DESCRIPTION:
#    A helper function to send a GET request to a specified GitHub API endpoint
#    
# GLOBALS:
#    GITHUB_PAT
# ARGUMENTS:
#    $1 _________________ GitHub repository (string)
#    $2 _________________ GitHub API Endpoint (string)
# OUTPUTS:
#    A content response from the GitHub endpoint
#######################################
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
#    BRANCH_NAME
#    GITHUB_PAT
#    IOTEDGE_REPO_PATH
# ARGUMENTS:
#    None
# OUTPUTS:
#    Lastest release version string of either edgelet or docker runtime images
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
    # $2 - Lastest Release Version from the github released branch

    # The latest released version is optional argument, so fetch that if not provided.
    if [ "$#" -le "2" ]; then
        latestReleasedVersion=$(get_latest_release_per_branch_name)
    else
        latestReleasedVersion=$2
    fi

    # Use the sort to compare, if the first result from sort() is not the input, then return false.
    higherVersion=$(echo "$latestReleasedVersion $1" | tr " " "\n"  | sort --version-sort -r | head -1)
    if [[ "$higherVersion" == "$latestReleasedVersion" ]]; then
        echo "FAILED: The proposed version ($1) cannot have a lower or equal version value than the latest released version ($latestReleasedVersion)"
        return 1;
    else
        echo "PASSED: version sanity check ($1)"
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
    # $(Build.SourcesDirectory)/iotedge
    $IOTEDGE_REPO_PATH

    # $(Build.SourcesDirectory)/azure-iotedge
    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; return 1; }

    # $(Build.SourcesDirectory)/iot-identity-service
    $IIS_REPO_PATH

    branchVersion=$(echo ${BRANCH_NAME##*/})

    if [ "$BRANCH_NAME" == "refs/heads/release/1.1" ]; then
        # Set target version file to be updated
        TARGET_IE_FILE="$AZURE_IOTEDGE_REPO_PATH/latest-iotedge-lts.json"

        # Get all the relevant version to be verified
        proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
        proposedImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq ".version" | tr -d '"')

        content=$(cat $AZURE_IOTEDGE_REPO_PATH/latest-iotedge-lts.json)
        latestEdgeletVersion=$(echo $content | jq ".iotedged" | tr -d '"')
        latestImageVersion=$(echo $content | jq '."azureiotedge-agent"' | tr -d '"')

        # Verify
        echo "Sanity check iotedge version"
        version_sanity_check $proposedEdgeletVersion $latestEdgeletVersion
        echo "Sanity check docker image version"
        version_sanity_check $proposedImageVersion $latestImageVersion

        # Rewriting the latest-iotedge-lts.json
        jqQuery=".iotedged = \"$proposedEdgeletVersion\" | .\"azureiotedge-agent\" = \"$proposedImageVersion\" | .\"azureiotedge-hub\" = \"$proposedImageVersion\""
        echo $content | jq "$jqQuery" > $TARGET_IE_FILE

        # Pring log for debugging
        echo "Update $TARGET_IE_FILE:"
        cat $TARGET_IE_FILE | jq '.'

    elif [ "$BRANCH_NAME" == "refs/heads/release/1.2" ]; then

        [[ -z "$IIS_REPO_PATH" ]] && { echo "\$IIS_REPO_PATH is undefined"; return 1; }
        # Set target version file to be updated
        TARGET_IE_FILE="$AZURE_IOTEDGE_REPO_PATH/latest-aziot-edge.json"
        TARGET_IIS_FILE="$AZURE_IOTEDGE_REPO_PATH/latest-aziot-identity-service.json"

        # Get all the relevant version to be verified
        proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
        proposedImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq ".version" | tr -d '"')
        proposedIisVersion=$(grep "PACKAGE_VERSION:" $IIS_REPO_PATH/.github/workflows/packages.yaml | awk '{print $2}' | tr -d "'" | tr -d '"')

        contentIe=$(cat $TARGET_IE_FILE)
        latestEdgeletVersion=$(echo $contentIe | jq '."aziot-edge"' | tr -d '"')
        contentIis=$(cat $TARGET_IIS_FILE)
        latestIisVersion=$(echo $contentIis | jq '."aziot-identity-service"' | tr -d '"')

        # Verify
        echo "Sanity check iotedge version"
        version_sanity_check $proposedEdgeletVersion $latestEdgeletVersion
        echo "Sanity check docker image version"
        version_sanity_check $proposedImageVersion get_latest_release_per_branch_name
        echo "Sanity check iot-identity-service version"
        version_sanity_check $proposedIisVersion $latestIisVersion

        # Update the version files
        # BEARWASHERE -- TODO: Write the proper version to the azure-iotedge version files.
        jqQuery=".\"aziot-edge\" = \"$proposedEdgeletVersion\""
        echo $contentIe | jq "$jqQuery" > $TARGET_IE_FILE
        jqQuery=".\"aziot-identity-service\" = \"$proposedIisVersion\""
        echo $contentIis | jq "$jqQuery" > $TARGET_IIS_FILE
        
        # Pring log for debugging
        echo "Update $TARGET_IE_FILE:"
        cat $TARGET_IE_FILE | jq '.'
        echo ""
        echo "Update $TARGET_IIS_FILE:"
        cat $TARGET_IIS_FILE | jq '.'

    elif [ "$BRANCH_NAME" == "refs/heads/main" ]; then
        echo "I'm pretty sure you don't want to release from the main branch."
        return 1
    else
        echo "Oh dear, how did you get here?!?"
        echo "Let me not let you do the release from your pull request branch"
        return 1
    fi

    #BEARWASHERE --
    #  0. Git branch & check if git is installed
    #  1. Determine the branch name
    #  2. Read the file version from iotedge
    #  3. Read the file version from azure-iotedge
    #  4. Compare the versions
    #  5. Update the version files respective to branch  <<< HERE
    #  6. Git Commit
    #  7. Git push
    #  8. Git tag
    
    lastCommitHash=$(git log -n 1 --pretty=format:"%H")

}