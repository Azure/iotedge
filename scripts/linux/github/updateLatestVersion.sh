#!/bin/bash

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
#    is_version_greater_than
# DESCRIPTION:
#    Verify that the new version is greater than (or equal to) the current version
# ARGUMENTS:
#    $1 __________ Name of versioned thing  (string)
#    $2 __________ New version              (string)
#    $3 __________ Current version          (string)
# OUTPUTS:
#    Exit status is 0 if new >= cur, 1 otherwise. A warning is printed to stdout if new == cur.
#######################################
is_version_greater_than()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }
    [[ -z "$3" ]] && { echo "$FUNCNAME: \$3 is undefined"; exit 1; }

    local newVersion=$2
    local curVersion=$3

    echo -n "$1: $newVersion > $curVersion ? "

    if [[ "$curVersion" == "$newVersion" ]]; then
        echo "Warning"
        echo "  Versions are equal, was that intended?"
    else
        highVersion=$(echo -e "$curVersion\n$newVersion" | sort --version-sort -r | head -1)
        if [[ "$highVersion" == "$newVersion" ]]; then # new > cur
            echo "Ok"
        else
            echo "Error"
            echo "  New version is less than current version"
            exit 1
        fi
    fi  
}

#######################################
# NAME:
#    is_major_minor_bump
# DESCRIPTION:
#    Determine if the new version bumps the major or minor version
# ARGUMENTS:
#    $1 __________ New version              (string)
#    $2 __________ Current version          (string)
# OUTPUTS:
#    Exit status is 0 if a major or minor version bump is detected, 1 otherwise
#######################################
is_major_minor_bump()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }

    local newVersion=$1
    local curVersion=$2
    local newParts curParts

    # validate that the new version is higher than the current version
    local highVersion=$(echo -e "$curVersion\n$newVersion" | sort --version-sort -r | head -1)
    if [[ "$highVersion" == "$curVersion" ]]; then
        echo "Error: New version ($newVersion) is less than current version ($curVersion)"
        exit 1
    fi

    IFS='.' read -a newParts <<< "$newVersion"
    IFS='.' read -a curParts <<< "$curVersion"

    newVersion="${newParts[0]}.${newParts[1]}"
    curVersion="${curParts[0]}.${curParts[1]}"

    if [[ "$newVersion" == "$curVersion" ]]; then
        return 1
    else
        return 0
    fi
}


#######################################
# NAME: 
#    update_product_versions_json
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
#    Updated product-versions.json
# REMARKS:
#    Make sure the directories provided have the proper branch/commit checked out
#######################################
update_product_versions_json()
{
    [[ -z "$IOTEDGE_REPO_PATH" ]] && { echo "\$IOTEDGE_REPO_PATH is undefined"; exit 1; }
    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$AZURE_IOTEDGE_REPO_PATH is undefined"; exit 1; }
    [[ -z "$IIS_REPO_PATH" ]] && { echo "\$IIS_REPO_PATH is undefined"; exit 1; }

    # Set target version file to be updated
    TARGET_FILE="$AZURE_IOTEDGE_REPO_PATH/product-versions.json"

    # Get new versions. The new product version comes from the latest tag in the iotedge repo. The other versions come
    # from the respective version files.
    proposedProductVersion=$(cd $IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match "[0-9].[0-9]*" HEAD)
    proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
    proposedCoreImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq -r '.version')
    proposedIisVersion=$(grep "PACKAGE_VERSION:" $IIS_REPO_PATH/.github/workflows/packages.yaml | awk '{print $2}' | tr -d "'" | tr -d '"')

    JQ="jq -L $IOTEDGE_REPO_PATH/scripts/linux/github"
    IFS='.' read -a parts <<< "$proposedProductVersion"
    prefix="${parts[0]}.${parts[1]}."

    # Get current versions. The current product version comes from the latest tag in the azure-iotedge repo. The other
    # versions come from product-versions.json.
    latestProductVersion=$(cd $AZURE_IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match "[0-9].[0-9]*" HEAD)
    latestEdgeletVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "aziot-edge")' $TARGET_FILE)
    latestIisVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "aziot-identity-service")' $TARGET_FILE)
    latestAgentVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "azureiotedge-agent")' $TARGET_FILE)
    latestHubVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "azureiotedge-hub")' $TARGET_FILE)
    latestTempSensorVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "azureiotedge-simulated-temperature-sensor")' $TARGET_FILE)
    latestDiagnosticsVersion=$($JQ -r --arg version "$latestProductVersion" 'include "product-versions"; aziotedge_component_version($version; "azureiotedge-diagnostics")' $TARGET_FILE)

    # Verify new versions
    is_version_greater_than "product" $proposedProductVersion $latestProductVersion
    is_version_greater_than "aziot-edge" $proposedEdgeletVersion $latestEdgeletVersion
    is_version_greater_than "aziot-identity-service" $proposedIisVersion $latestIisVersion
    is_version_greater_than "azureiotedge-agent" $proposedCoreImageVersion $latestAgentVersion
    is_version_greater_than "azureiotedge-hub" $proposedCoreImageVersion $latestHubVersion
    is_version_greater_than "azureiotedge-simulated-temperature-sensor" $proposedCoreImageVersion $latestTempSensorVersion
    # The diagnostics image is always versioned to match edgelet, not the other core images
    is_version_greater_than "azureiotedge-diagnostics" $proposedEdgeletVersion $latestDiagnosticsVersion

    # Update product-versions.json
    if is_major_minor_bump $proposedProductVersion $latestProductVersion; then
        echo "Adding new product entries for aziot-edge version $proposedProductVersion"
        echo "$($JQ \
            --arg current_product_ver "$latestProductVersion" \
            --arg new_product_ver "$proposedProductVersion" \
            --arg edgelet_ver "$proposedEdgeletVersion" \
            --arg identity_ver "$proposedIisVersion" \
            --arg core_image_ver "$proposedCoreImageVersion" '
            include "product-versions";
            add_aziotedge_products($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver)
        ' $TARGET_FILE)" > $TARGET_FILE
    else
        echo "Updating existing product entries for aziot-edge version $proposedProductVersion"
        echo "$($JQ \
            --arg current_product_ver "$latestProductVersion" \
            --arg new_product_ver "$proposedProductVersion" \
            --arg edgelet_ver "$proposedEdgeletVersion" \
            --arg identity_ver "$proposedIisVersion" \
            --arg core_image_ver "$proposedCoreImageVersion" '
            include "product-versions";
            update_aziotedge_versions($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver)
        ' $TARGET_FILE)" > $TARGET_FILE
    fi

    # Print log for debugging
    echo "Updated $TARGET_FILE:"
    jq '.' $TARGET_FILE
    echo ""
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