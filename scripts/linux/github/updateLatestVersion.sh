#!/bin/bash

#######################################
# NAME: 
#    check_required_variables
# DESCRIPTION:
#    Check the commonly used variable in the script if they are provided.
#    
# GLOBALS:
#    GITHUB_PAT ___________ Github Personal Access Token (string)
#    BRANCH_NAME __________ Git Branch name              (string)
#    IOTEDGE_REPO_PATH ____ Path to /iotedge             (string)
# ARGUMENTS:
#    None
# OUTPUTS:
#    Error message
# RETURN:
#    1 if the variable is not provided otherwise return 0.
#######################################
check_required_variables()
{
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; exit 1; }
    [[ -z "$BRANCH_NAME" ]] && { echo "\$BRANCH_NAME is undefined"; exit 1; }   
    [[ -z "$IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; exit 1; }
}


#######################################
# NAME: 
#    send_github_request
# DESCRIPTION:
#    A helper function to send a GET request to a specified GitHub API endpoint
#    
# GLOBALS:
#    GITHUB_PAT ___________ Github Personal Access Token (string)
# ARGUMENTS:
#    $1 _________________ GitHub repository   (string)
#    $2 _________________ GitHub API Endpoint (string)
# OUTPUTS:
#    A content response from the GitHub endpoint
#######################################
send_github_request()
{
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; exit 1; }

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
#    BRANCH_NAME __________ Git Branch name              (string)
#      i.e. $(Build.SourceBranch)
#    GITHUB_PAT ___________ Github Personal Access Token (string)
#    IOTEDGE_REPO_PATH ____ Path to /iotedge             (string)
# ARGUMENTS:
#    None
# OUTPUTS:
#    The lastest release version string of either edgelet or docker runtime images
#    from Github release page.
#######################################
get_latest_release_per_branch_name()
{
    check_required_variables

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


#######################################
# NAME: 
#    version_sanity_check
# DESCRIPTION:
#    Verify if the Proposed Version is a later version than the Latest Release version
# GLOBALS:
#    BRANCH_NAME
#    GITHUB_PAT
#    IOTEDGE_REPO_PATH
# ARGUMENTS:
#    $1 __________ Proposed Version        (string)
#    $2 __________ Latest Released Version (string)
# OUTPUTS:
#    If Passed, print a log message
#    If Failed, print a log error message withe SIGINT=1
#######################################
version_sanity_check()
{
    # The latest released version is optional argument, so fetch that if not provided.
    if [ "$#" -le "2" ]; then
        latestReleasedVersion=$(get_latest_release_per_branch_name)
    else
        latestReleasedVersion=$2
    fi

    # Use the sort to compare, if the first result from sort() is not the input, then return false.
    higherVersion=$(echo "$latestReleasedVersion $1" | tr " " "\n"  | sort --version-sort -r | head -1)
    if [[ "$higherVersion" == "$latestReleasedVersion" ]]; then
        # Remark: We can't really do `exit 1` here it's not always gauranteed that we will update both `iotedge` and docker images at the same release
        echo "##[warning]FAILED: The proposed version ($1) cannot have a lower or equal version value than the latest released version ($latestReleasedVersion)"
    else
        echo "PASSED: version sanity check ($1)"
    fi
}


#######################################
# NAME: 
#    update_latest_version_json
# DESCRIPTION:
#    Update the necessary version.json files for the last step of the IoTEdge release.
# GLOBALS:
#    IOTEDGE_REPO_PATH __________ Path to /iotedge directory    (string)
#        i.e. $(Build.SourcesDirectory)/iotedge
#    AZURE_IOTEDGE_REPO_PATH ____ Path to /azure-iotedge        (string)
#        i.e. $(Build.SourcesDirectory)/azure-iotedge
#    IIS_REPO_PATH ______________ Path to /iot-identity-service (string)
#        i.e. $(Build.SourcesDirectory)/iot-identity-service
# OUTPUTS:
#    Updated latest version JSON files
# REMARK:
#    Please make sure the directories provided need to have a proper branch/commit 
#    checked out.
#######################################
update_latest_version_json()
{
    check_required_variables

    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; exit 1; }

    branchVersion=$(echo ${BRANCH_NAME##*/})
    latestReleasedVersion=$(get_latest_release_per_branch_name)

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

    elif [ "$BRANCH_NAME" == "refs/heads/release/1.4" ]; then

        [[ -z "$IIS_REPO_PATH" ]] && { echo "\$IIS_REPO_PATH is undefined"; exit 1; }
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
        exit 1;
    else
        echo "Oh dear, how did you get here?!?"
        echo "Let me not let you do the release from your pull request branch"
        exit 1;
    fi
}


#######################################
# NAME: 
#    github_update_and_push
# DESCRIPTION:
#    The function commits the changes, tag, and push them to remote repository
# GLOBALS:
#    AZURE_IOTEDGE_REPO_PATH ____ Path to /azure-iotedge        (string)
#        i.e. $(Build.SourcesDirectory)/azure-iotedge
#    VERSION ____________________ Current release version       (string)
#    GITHUB_PAT _________________ Github Personal Access Token  (string)
# OUTPUTS:
#    Updated latest version JSON files
# REMARK:
#    Please make sure the directories provided need to have a proper branch/commit 
#    checked out.
#######################################
github_update_and_push()
{
    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$AZURE_IOTEDGE_REPO_PATH is undefined"; exit 1; }
    [[ -z "$VERSION" ]] && { echo "\$VERSION is undefined"; exit 1; }
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; exit 1; }

    cd $AZURE_IOTEDGE_REPO_PATH
    git config user.name iotedge1

    git checkout main
    git pull

    git commit -am "Prepare for Release $VERSION"
    lastCommitHash=$(git log -n 1 --pretty=format:"%H")
    git tag "$VERSION" $lastCommitHash

    git push --force https://$GITHUB_PAT@github.com/Azure/azure-iotedge.git
    git push --force https://$GITHUB_PAT@github.com/Azure/azure-iotedge.git "$VERSION"
}