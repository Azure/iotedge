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
        # BEARWASHERE
        #exit 1;
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
    # $(Build.SourcesDirectory)/iotedge
    $IOTEDGE_REPO_PATH

    # $(Build.SourcesDirectory)/azure-iotedge
    $AZURE_IOTEDGE_REPO_PATH

    # $(Build.SourcesDirectory)/iot-identity-service
    $IIS_REPO_PATH

    branchVersion=$(echo ${BRANCH_NAME##*/})

    if [[ "$BRANCH_NAME" == "refs/heads/release/1.1" ]]; then
        # proposedEdgeletVersion - iotedge/edgelet/version.txt
        proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
        # proposedImageVersion - iotedge/versionInfo.json
        proposedImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq ".version" | tr -d '"')

        # latestEdgeletVersion - azure-iotedge/latest-iotedge-lts.json
        content=$(cat $AZURE_IOTEDGE_REPO_PATH/latest-iotedge-lts.json)
        latestEdgeletVersion=$(echo $content | jq ".iotedged" | tr -d '"')
        # latestImageVersion - azure-iotedge/latest-iotedge-lts.json
        latestImageVersion=$(echo $content | jq '."azureiotedge-agent"' | tr -d '"')

        version_sanity_check $proposedEdgeletVersion $latestEdgeletVersion
        version_sanity_check $proposedImageVersion $latestImageVersion

        # Rewriting the latest-iotedge-lts.json
        jqQuery=".iotedged = \"$proposedEdgeletVersion\" | .\"azureiotedge-agent\" = \"$proposedImageVersion\" | .\"azureiotedge-hub\" = \"$proposedImageVersion\""
        echo $content | jq "$jqQuery" > $AZURE_IOTEDGE_REPO_PATH/latest-iotedge-lts.json
        cat $AZURE_IOTEDGE_REPO_PATH/latest-iotedge-lts.json

    elif [[ "$BRANCH_NAME" == "refs/heads/release/1.2" ]]; then

        [[ -z "$IIS_REPO_PATH" ]] && { echo "\$IIS_REPO_PATH is undefined"; exit 1; }

        # proposedEdgeletVersion - iotedge/edgelet/version.txt
        proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
        # proposedImageVersion - iotedge/versionInfo.json
        proposedImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq ".version" | tr -d '"')
        # proposedIisVersion - iot-identity-service/
        # BEARWASHERE -- Make sure the pipeline is checked out for proper release/1.2
        proposedIisVersion=$(grep "PACKAGE_VERSION:" $IIS_REPO_PATH/.github/workflows/packages.yaml | awk '{print $2}' | tr -d "'" | tr -d '"')

        # latestEdgeletVersion - azure-iotedge/latest-aziot-edge.json
        content=$(cat $AZURE_IOTEDGE_REPO_PATH/latest-aziot-edge.json)
        latestEdgeletVersion=$(echo $content | jq '."aziot-edge"' | tr -d '"')
        # latestIisVersion - azure-iotedge/latest-aziot-identity-service.json
        content=$(cat $AZURE_IOTEDGE_REPO_PATH/latest-aziot-identity-service.json)
        latestIisVersion=$(echo $content | jq '."aziot-identity-service"' | tr -d '"')

        version_sanity_check $proposedEdgeletVersion $latestEdgeletVersion
        version_sanity_check $proposedImageVersion get_latest_release_per_branch_name
        version_sanity_check $proposedIisVersion $latestIisVersion

        # BEARWASHERE -- TODO: Write the proper version to the azure-iotedge version files.
        
    elif [[ "$BRANCH_NAME" == "refs/heads/main" ]]; then
        echo "I'm pretty sure you don't want to release the main branch."
        # BEARWASHERE
        #exit 1
    else
        echo "Oh dear, how did you get here?!?"
        # BEARWASHERE
        #exit 1
    fi

    #BEARWASHERE --
    #  1. Determine the branch name
    #  2. Read the file version from iotedge
    #  3. Read the file version from azure-iotedge
    #  4. Compare the versions
    

    version_sanity_check $proposedVersion $latestReleasedVersion
    
}