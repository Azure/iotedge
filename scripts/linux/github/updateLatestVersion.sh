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
#    version_highest
# DESCRIPTION:
#    Return the highest of two versions
# ARGUMENTS:
#    $1 __________ Left version  (string)
#    $2 __________ Right version (string)
# OUTPUTS:
#    The highest version will be printed to stdout
#######################################
version_highest()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }

    echo -e "$1\n$2" | sort --version-sort -r | head -1
}


#######################################
# NAME:
#    version_gt
# DESCRIPTION:
#    Verify that the left version is greater than the right version
# ARGUMENTS:
#    $1 __________ Name of versioned thing          (string)
#    $2 __________ Left version                     (string)
#    $3 __________ Right version                    (string)
# OUTPUTS:
#    Exit status is 0 if left > right, 1 otherwise.
#######################################
version_gt()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }
    [[ -z "$3" ]] && { echo "$FUNCNAME: \$3 is undefined"; exit 1; }

    echo -n "$1: $2 > $3 ? "

    if [[ "$2" == "$3" || "$(version_highest "$2" "$3")" == "$3" ]]; then # lhs <= rhs
        echo 'Failed'
        return 1
    fi

    echo 'Ok'
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
# OUTPUTS:
#    Exit status is 0 if left >= right, 1 otherwise.
#######################################
version_ge()
{
    [[ -z "$1" ]] && { echo "$FUNCNAME: \$1 is undefined"; exit 1; }
    [[ -z "$2" ]] && { echo "$FUNCNAME: \$2 is undefined"; exit 1; }
    [[ -z "$3" ]] && { echo "$FUNCNAME: \$3 is undefined"; exit 1; }

    echo -n "$1: $2 >= $3 ? "

    if [[ "$2" != "$3" && "$(version_highest "$2" "$3")" == "$3" ]]; then # lhs < rhs
        echo 'Failed'
        return 1
    fi

    echo 'Ok'
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

    if ! version_ge "major_minor" "$newVersion" "$curVersion"; then
        exit 1
    fi

    local newParts curParts
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
        | select(.name == "lts")
        | .products[]
        | select(.id=="aziot-edge" and .version==$version)
        | isempty(.)
        | not
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

    local remote="${AZURE_IOTEDGE_REPO_REMOTE:-origin}"
    local branch="${AZURE_IOTEDGE_REPO_BRANCH:-$(git branch --show-current)}"

    # in case commits were made after this pipeline started but before we arrived here, sync to
    # the tip of the branch
    git checkout "$branch"

    # Set target version file to be updated
    local json_file="$AZURE_IOTEDGE_REPO_PATH/product-versions.json"

    # Get new versions. The new product version comes from the latest tag in the iotedge repo. The other versions come
    # from the respective version files.
    local new_product_ver="$(cd $IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match "[0-9].[0-9]*" HEAD)"
    local new_edgelet_ver="$(cat $IOTEDGE_REPO_PATH/edgelet/version.txt)"
    local new_image_ver="$(cat $IOTEDGE_REPO_PATH/versionInfo.json | jq -r '.version')"
    local new_identity_ver="$(
        grep "PACKAGE_VERSION:" $IIS_REPO_PATH/.github/workflows/packages.yaml \
        | awk '{print $2}' \
        | tr -d "'" \
        | tr -d '"'
    )"

    local JQ="jq -L $IOTEDGE_REPO_PATH/scripts/linux/github"

    # Get current versions. The current product version comes from the latest tag in the azure-iotedge repo. The other
    # versions come from product-versions.json.
    local cur_product_ver="$(cd $AZURE_IOTEDGE_REPO_PATH; git describe --tags --abbrev=0 --match '[0-9].[0-9]*' HEAD)"
    local cur_edgelet_ver="$(get_version_from_json $cur_product_ver 'aziot-edge')"
    local cur_identity_ver="$(get_version_from_json $cur_product_ver 'aziot-identity-service')"
    local cur_agent_ver="$(get_version_from_json $cur_product_ver 'azureiotedge-agent')"
    local cur_hub_ver="$(get_version_from_json $cur_product_ver 'azureiotedge-hub')"
    local cur_tempsensor_ver="$(get_version_from_json $cur_product_ver 'azureiotedge-simulated-temperature-sensor')"
    local cur_diagnostics_ver="$(get_version_from_json $cur_product_ver 'azureiotedge-diagnostics')"

    # Verify new versions
    version_gt 'product' "$new_product_ver" "$cur_product_ver"
    version_gt 'aziot-edge' "$new_edgelet_ver" "$cur_edgelet_ver"
    version_gt 'aziot-identity-service' "$new_identity_ver" "$cur_identity_ver"
    version_gt 'azureiotedge-agent' "$new_image_ver" "$cur_agent_ver"
    version_gt 'azureiotedge-hub' "$new_image_ver" "$cur_hub_ver"
    version_gt 'azureiotedge-simulated-temperature-sensor' "$new_image_ver" "$cur_tempsensor_ver"
    # The diagnostics image is always versioned to match edgelet, not the other core images
    version_gt 'azureiotedge-diagnostics' "$new_edgelet_ver" "$cur_diagnostics_ver"
    # The product version should be >= all the other versions
    version_ge 'product v. aziot-edge & diagnostics' "$new_product_ver" "$new_edgelet_ver"
    version_ge 'product v. aziot-identity-service' "$new_product_ver" "$new_identity_ver"
    version_ge 'product v. core images' "$new_product_ver" "$new_image_ver"

    # Update product-versions.json
    if is_major_minor_bump "$new_product_ver" "$cur_product_ver"; then
        echo "Adding new product entries for aziot-edge version $new_product_ver"
        echo "$($JQ \
            --arg current_product_ver "$cur_product_ver" \
            --arg new_product_ver "$new_product_ver" \
            --arg edgelet_ver "$new_edgelet_ver" \
            --arg identity_ver "$new_identity_ver" \
            --arg core_image_ver "$new_image_ver" '
            include "product-versions";
            add_aziotedge_products($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver)
        ' $json_file)" > "$json_file"
    else
        echo "Updating existing product entries for aziot-edge version $new_product_ver"
        echo "$($JQ \
            --arg current_product_ver "$cur_product_ver" \
            --arg new_product_ver "$new_product_ver" \
            --arg edgelet_ver "$new_edgelet_ver" \
            --arg identity_ver "$new_identity_ver" \
            --arg core_image_ver "$new_image_ver" '
            include "product-versions";
            update_aziotedge_versions($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver)
        ' $json_file)" > "$json_file"
    fi

    # Print log for debugging
    echo "Updated $json_file:"
    jq '.' "$json_file"
    echo ""

    git add product-versions.json

    # configure git
    git config user.name 'IoT Edge Bot'
    git config user.email "$GIT_EMAIL"

    local remote_url="$(git config --get "remote.$remote.url")"
    remote_url="${remote_url/#https:\/\//https:\/\/$GITHUB_PAT@}" # add token to URL

    # commit changes, tag, and push
    git commit -m "Prepare for release $new_product_ver"
    git tag "$new_product_ver"
    git push "$remote_url" "HEAD:$branch"
    git push "$remote_url" "$new_product_ver"

    PRODUCT_VERSION="$new_product_ver"
}
