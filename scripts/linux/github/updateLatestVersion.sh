#!/bin/bash

#######################################
# NAME: 
#    send_github_request
# DESCRIPTION:
#    A helper function to send a GET request to a specified GitHub API endpoint
# GLOBALS:
#    GITHUB_PAT ___________ Github Personal Access Token (string)
# ARGUMENTS:
#    $1 ___________________ GitHub repository   (string)
#    $2 ___________________ GitHub API Endpoint (string)
# OUTPUTS:
#    A content response from the GitHub endpoint
#######################################
send_github_request()
{
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; exit 1; }

    url="https://api.github.com/repos/$1/$2"
    header_content="Accept:application/vnd.github.v3+json"
    header_auth="Authorization:token $GITHUB_PAT"
    content=$(curl -s -X GET -H "$header_content" -H "$header_auth" "$url")
    echo $content
}


#######################################
# NAME: 
#    version_ge
# DESCRIPTION:
#    Verify that the left version is greater than or equal to the right version
# ARGUMENTS:
#    $1 __________ Name of versioned thing          (string)
#    $2 __________ Left version                     (string)
#    $3 __________ Right version                    (string)
#    $4 __________ Optional(true): Print warning    (boolean)
# OUTPUTS:
#    Exit status is 0 if left >= right, 1 otherwise. A warning is printed to
#    stdout if left == right, unless the optional 4th argument is set to false.
#######################################
version_ge()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }
    [[ -z "$3" ]] && { echo "$FUNCNAME: \$3 is undefined"; exit 1; }

    local lhs=$2
    local rhs=$3
    local warning=${4:-true}

    echo -n "$1: $lhs > $rhs ? "

    if [[ "$rhs" == "$lhs" ]]; then
        if [[ "$warning" != 'true' ]]; then
            echo 'Ok'
        else
            echo 'Warning - versions are equal, was that intended?'
        fi
    else
        highVersion=$(echo -e "$rhs\n$lhs" | sort --version-sort -r | head -1)
        if [[ "$highVersion" == "$lhs" ]]; then # lhs > rhs
            echo 'Ok'
        else
            echo 'Failed'
            return 1
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
    if [[ "$newVersion" != "$curVersion" && "$highVersion" == "$curVersion" ]]; then
        echo "Error: New version ($newVersion) is less than current version ($curVersion)"
        return 1
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
#    get_version_from_json
# DESCRIPTION:
#    Get the version of a component from product-versions.json
# GLOBALS:
#    AZURE_IOTEDGE_REPO_PATH __________ Path to /azure-iotedge (string)
#        i.e. $(Build.SourcesDirectory)/azure-iotedge
#    JQ _______________________________ jq command with search path for "product-versions" module  (string)
#        i.e. jq -L $(Build.SourcesDirectory)/iotedge/scripts/linux/github
# ARGUMENTS:
#    $1 _______________________________ Version to search for (string)
#    $2 _______________________________ Component name to search for (string)
# OUTPUTS:
#    The version of the component from product-versions.json will be printed to stdout
#######################################
get_version_from_json()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }
    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$AZURE_IOTEDGE_REPO_PATH is undefined"; exit 1; }
    [[ -z "$JQ" ]] && { echo "\$JQ is undefined"; exit 1; }

    local version=$1
    local component=$2
    local json="$AZURE_IOTEDGE_REPO_PATH/product-versions.json"

    $JQ -r --arg version "$version" --arg name "$component" '
        include "product-versions";
        [ aziotedge_component_version($version; $name) ] | first
    ' $json
}


#######################################
# NAME:
#    version_is_lts
# DESCRIPTION:
#    Determine if the given version matches a product in the LTS channel in product-versions.json
# GLOBALS:
#    AZURE_IOTEDGE_REPO_PATH __________ Path to /azure-iotedge (string)
# ARGUMENTS:
#    $1 _______________________________ Version to search for (string)
# OUTPUTS:
#    Prints 'true' to stdout if the given version matches a product in the LTS channel, 'false'
#    otherwise.
#######################################
version_is_lts()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "\$AZURE_IOTEDGE_REPO_PATH is undefined"; exit 1; }

    local version=$1

    jq -r --arg version "$version" '
        .channels[]
        | .products[]
        | select(.id=="aziot-edge" and .version==$version)
        | .name
        | contains("LTS")
    ' $AZURE_IOTEDGE_REPO_PATH/product-versions.json
}   

#######################################
# NAME: 
#    update_product_versions_json
# DESCRIPTION:
#    Update product-versions.json files in the product repo, then commits the change, tags it, and pushes the change to
#    the remote repository.
# GLOBALS:
#    AZURE_IOTEDGE_REPO_REMOTE __ Optional, defaults to 'origin'        (string)
#    AZURE_IOTEDGE_REPO_BRANCH __ Optional, defaults to current branch  (string)
#    AZURE_IOTEDGE_REPO_PATH ____ Path to /azure-iotedge                (string)
#        i.e. $(Build.SourcesDirectory)/azure-iotedge
#    IIS_REPO_PATH ______________ Path to /iot-identity-service         (string)
#        i.e. $(Build.SourcesDirectory)/iot-identity-service
#    IOTEDGE_REPO_PATH __________ Path to /iotedge directory            (string)
#        i.e. $(Build.SourcesDirectory)/iotedge
#    GIT_EMAIL __________________ Email address for commit              (string)
#    GITHUB_PAT _________________ Github Personal Access Token          (string)
# OUTPUTS:
#    PRODUCT_VERSION ____________ The product version that was added/updated (string)
#######################################
update_product_versions_json()
{
    [[ ! -d "$AZURE_IOTEDGE_REPO_PATH" ]] && { echo "Path '$AZURE_IOTEDGE_REPO_PATH' not found"; exit 1; }
    [[ ! -d "$IIS_REPO_PATH" ]] && { echo "Path '$IIS_REPO_PATH' not found"; exit 1; }
    [[ ! -d "$IOTEDGE_REPO_PATH" ]] && { echo "Path '$IOTEDGE_REPO_PATH' not found"; exit 1; }
    [[ -z "$GIT_EMAIL" ]] && { echo "\$GIT_EMAIL is undefined"; exit 1; }
    [[ -z "$GITHUB_PAT" ]] && { echo "\$GITHUB_PAT is undefined"; exit 1; }

    cd "$AZURE_IOTEDGE_REPO_PATH"

    AZURE_IOTEDGE_REPO_REMOTE=${AZURE_IOTEDGE_REPO_REMOTE:-origin}
    AZURE_IOTEDGE_REPO_BRANCH=${AZURE_IOTEDGE_REPO_BRANCH:-$(git branch --show-current)}

    # in case commits were made after this pipeline started but before we arrived here, sync to
    # the tip of the branch
    git checkout "$AZURE_IOTEDGE_REPO_BRANCH"

    # Set target version file to be updated
    TARGET_FILE="$AZURE_IOTEDGE_REPO_PATH/product-versions.json"

    # Get new versions. The new product version comes from the latest tag in the iotedge repo. The other versions come
    # from the respective version files.
    proposedProductVersion=$(cd $IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match "[0-9].[0-9]*" HEAD)
    proposedEdgeletVersion=$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)
    proposedCoreImageVersion=$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq -r '.version')
    proposedIisVersion=$(
        grep "PACKAGE_VERSION:" $IIS_REPO_PATH/.github/workflows/packages.yaml \
        | awk '{print $2}' \
        | tr -d "'" \
        | tr -d '"'
    )

    JQ="jq -L $IOTEDGE_REPO_PATH/scripts/linux/github"

    # Get current versions. The current product version comes from the latest tag in the azure-iotedge repo. The other
    # versions come from product-versions.json.
    latestProductVersion=$(cd $AZURE_IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match "[0-9].[0-9]*" HEAD)
    latestEdgeletVersion=$(get_version_from_json $latestProductVersion "aziot-edge")
    latestIisVersion=$(get_version_from_json $latestProductVersion "aziot-identity-service")
    latestAgentVersion=$(get_version_from_json $latestProductVersion "azureiotedge-agent")
    latestHubVersion=$(get_version_from_json $latestProductVersion "azureiotedge-hub")
    latestTempSensorVersion=$(get_version_from_json $latestProductVersion "azureiotedge-simulated-temperature-sensor")
    latestDiagnosticsVersion=$(get_version_from_json $latestProductVersion "azureiotedge-diagnostics")

    # Verify new versions
    version_ge "product" $proposedProductVersion $latestProductVersion
    version_ge "aziot-edge" $proposedEdgeletVersion $latestEdgeletVersion
    version_ge "aziot-identity-service" $proposedIisVersion $latestIisVersion
    version_ge "azureiotedge-agent" $proposedCoreImageVersion $latestAgentVersion
    version_ge "azureiotedge-hub" $proposedCoreImageVersion $latestHubVersion
    version_ge "azureiotedge-simulated-temperature-sensor" $proposedCoreImageVersion $latestTempSensorVersion
    # The diagnostics image is always versioned to match edgelet, not the other core images
    version_ge "azureiotedge-diagnostics" $proposedEdgeletVersion $latestDiagnosticsVersion
    # The product version should be >= all the other versions
    version_ge "product v. aziot-edge & diagnostics" $proposedProductVersion $proposedEdgeletVersion false
    version_ge "product v. aziot-identity-service" $proposedProductVersion $proposedIisVersion false
    version_ge "product v. core images" $proposedProductVersion $proposedCoreImageVersion false

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

    git add product-versions.json

    # configure git
    git config user.name 'IoT Edge Bot'
    git config user.email "$GIT_EMAIL"

    remote_url="$(git config --get "remote.$AZURE_IOTEDGE_REPO_REMOTE.url")"
    # TODO: vvv remove temporary logging
    echo ">>> REMOTE URL FROM GIT: $remote_url"
    remote_url="${remote_url/#https:\/\//https:\/\/$GITHUB_PAT@}" # add token to URL
    echo ">>> UPDATED REMOTE URL: ${remote_url/${GITHUB_PAT:6}/******}"

    # commit changes, tag, and push
    git commit -m "Prepare for release $proposedProductVersion"
    git tag "$proposedProductVersion"
    git push "$remote_url" "HEAD:$AZURE_IOTEDGE_REPO_BRANCH"
    git push "$remote_url" "$proposedProductVersion"

    PRODUCT_VERSION="$proposedProductVersion"
}
