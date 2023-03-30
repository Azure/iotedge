#!/bin/bash

# This script is intended to be sourced from other scripts. It expects that 'set -euo pipefail' was
# invoked by the caller.

DEBUG="${DEBUG:-}"

DEFAULT_PLATFORM_MAP='[
  {
    "platform": "linux/amd64",
    "tag_suffix": "linux-amd64"
  },
  {
    "platform": "linux/arm64",
    "tag_suffix": "linux-arm64v8"
  },
  {
    "platform": "linux/arm/v7",
    "tag_suffix": "linux-arm32v7"
  }
]'

#
# Retrieves the login credentials for the given registry from Docker's local config. This function
# can get credentials directly from config.json, or by querying the configured credential manager.
#
# Globals
#   DOCKER_CONFIG   Optional. The path to Docker's config.json. Default value is $HOME/.docker
#   REGISTRY        Required. The registry that hosts the image
#
# Outputs
#   OUTPUTS         Credentials in the form 'username:secret'
#
get_docker_credentials() {
    local docker_config="$(cat "${DOCKER_CONFIG:-$HOME/.docker}/config.json")"
    local cred

    if $(echo "$docker_config" | jq --arg reg "$REGISTRY" '.auths | .[$reg] | has("auth")'); then
        # Get credentials directly from config.json
        cred=$(echo "$docker_config" |
            jq --arg reg "$REGISTRY" -r '.auths | .[$reg].auth' | base64 --decode)
    elif $(echo "$docker_config" | jq 'has("credsStore")'); then
        # Get credentials from store
        local store=$(echo "$docker_config" | jq -r '.credsStore')
        cred=$(echo "$REGISTRY" | docker-credential-$store get | jq -r '"\(.Username):\(.Secret)"')
    fi

    OUTPUTS="$cred"    
}

#
# Given a WWW-Authenticate header containing a "Bearer" challenge as input, parsed the realm and
# service parameters.
#
# Globals
#   RESPONSE_401    Required. The "401 Unauthorized" response headers which include the
#                   WWW-Authenticate header to be parsed
#
# Outputs
#   OUTPUTS         REALM and SERVICE in NAME=value format, suitable for sourcing into the
#                   current environment
#
parse_authenticate_header() {
    local auth_header=$(
        echo "$RESPONSE_401" | grep -i 'WWW-Authenticate: Bearer ' | sed -e 's/[[:space:]]*$//')
    local -a challenge_vars

    local key
    for key in realm service
    do
        challenge_vars+=( "local $(echo "$auth_header" | grep -Eo "$key=\"[^\"]+\"")" )
    done

    source <(printf '%s\n' "${challenge_vars[@]}")

    OUTPUTS="REALM='$realm'; SERVICE='$service'"
}

#
# Queries the registry's authorization service for a bearer token that can be used to perform
# operations at the given scopes.
#
# This function assumes that the needed credentials to acquire the bearer token are available in the
# Docker configuration file at $DOCKER_CONFIG/config.json.
#
# Globals
#   REGISTRY        Required. The registry from which to request a token
#   SCOPES          Required. Space-delimited list of scopes for which a token will be requested
#                   See https://docs.docker.com/registry/spec/auth/token/#requesting-a-token
#
# Outputs
#   OUTPUTS         The bearer token
#
get_bearer_token() {
    # Make unauthorized request to discover the authorization service
    local result=$(curl \
        --head \
        --include \
        --show-error \
        --silent \
        --write-out '\n%{http_code}' \
        "https://$REGISTRY/v2/")
    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 401 ]]; then
        echo 'Failed to discover authorization service.' \
            "Expected status=401, actual status=$status, details="
        echo "$result"
        return 1
    fi

    # Get the REALM and SERVICE values from the WWW-Authenticate header
    RESPONSE_401="$result" parse_authenticate_header
    source <(echo "$OUTPUTS")

    # Get credentials from Docker config
    REGISTRY="$SERVICE" get_docker_credentials
    local cred="$OUTPUTS"

    # Make the authorization request for the given scopes
    result=$(curl \
        --show-error \
        --silent \
        --user "$cred" \
        --write-out '\n%{http_code}' \
        "$REALM?service=$SERVICE&scope=${SCOPES// /&scope=}")

    status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 200 ]]; then
        echo "Failed to get bearer token, status=$status, details="
        echo "$result"
        return 1
    fi

    [[ "$DEBUG" -eq 1 ]] && echo "Authorized for $SERVICE, scope='$SCOPES'"

    OUTPUTS="$(echo "$result" | jq -r '.access_token')"
}

#
# Merge two space-delimited lists of scopes into a single list. Scopes for the same repository will
# be merged into a single entry, so each repository will be represented exactly once.
#
# Globals
#   SCOPES1         Required. Space-delimited list of scopes
#   SCOPES2         Required. Another space-delimeted list of scopes to merge with the first
#
# Outputs
#   OUTPUTS         The merged list
#
merge_scopes() {
    local scopes
    local -a json
    for scopes in "$SCOPES1" "$SCOPES2"; do
        # Convert each list to this format: [{"repository":"repo1","scopes":["pull","push"]},...]
        json+=( $(echo "$scopes" | jq -Rc '
            split(" ") | [
                .[] | ltrimstr("repository:") | split(":") | 
                if length == 2 then . else "Invalid scope: \"\(join(":"))\"\n" | halt_error end | 
                { repository: .[0], scopes: .[1] | split(",") }
            ]') )
    done

    # Combine scopes for each repository
    local merged="$(echo "${json[@]}" | jq -sc '
        flatten as $source | [
            [ $source[] | .repository ] | unique[] as $repository |
            [ $source[] | select(.repository == $repository) | .scopes[] ] | unique | 
            { repository: $repository, scopes: . }
        ]')"

    # Return to the original space-delimited scopes format
    OUTPUTS="$(echo "$merged" |
        jq -r '[ .[] | "repository:\(.repository):\(.scopes | join(","))" ] | join(" ")')"
}

#
# Internal routine to update SCOPES and acquire a new TOKEN if needed.
#
# Inputs
#   $1              The required scope
#
# Outputs
#   SCOPES          The list of scopes to update
#   TOKEN           The current bearer token. Will be updated if SCOPES changes
#
__get_token_with_scope() {
    SCOPES=${SCOPES:-}
    TOKEN=${TOKEN:-}

    # Get a new authorization token if necessary
    SCOPES1="$SCOPES" SCOPES2="$1" merge_scopes
    if [[ -z "$TOKEN" ]] || [[ "$SCOPES" != "$OUTPUTS" ]]; then
        SCOPES="$OUTPUTS"
        get_bearer_token
        TOKEN="$OUTPUTS"
    fi
}

#
# Pulls the given manifest from the registry.
#
# Globals
#   REFERENCE       Required. The tag or digest for which a manifest will be retreived
#   REGISTRY        Required. The registry from which a manifest will be retreived
#   REPOSITORY      Required. The image repository for which a manifest will be retreived
#   SCOPES          Optional. If not given, a scope with be generated to perform this operation
#   TOKEN           Optional. The bearer token to use. If not given, a new token will be generated
#
# Outputs
#   OUTPUTS         The retreived manifest
#   TOKEN           Unchanged if set by caller, otherwise it will contain a valid bearer token
#
pull_manifest() {
    __get_token_with_scope "repository:$REPOSITORY:pull"

    local accept_types=(
        'application/vnd.docker.distribution.manifest.list.v2+json'
        'application/vnd.docker.distribution.manifest.v2+json'
        'application/vnd.oci.image.index.v1+json'
        'application/vnd.oci.image.manifest.v1+json'
    )
    local accept=$(printf '%s,' "${accept_types[@]}")
    accept="${accept:0:-1}"

    # make the pull request with authorization
    local result=$(curl \
        --header "Accept: $accept" \
        --header "Authorization: Bearer $TOKEN" \
        --show-error \
        --silent \
        --write-out '\n%{http_code}' \
        "https://$REGISTRY/v2/$REPOSITORY/manifests/$REFERENCE")

    local ref=":$REFERENCE"
    local manifest="$REGISTRY/$REPOSITORY:${ref/#:sha256:/@sha256:}"
    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 200 ]]; then
        echo "Failed to pull manifest '$manifest', status=$status, details="
        echo "$result"
        return 1
    fi

    echo "Pulled $manifest"

    OUTPUTS="$result"
}

#
# Given a manifest, return its media type, e.g., application/vnd.oci.image.manifest.v1+json
#
# Globals
#   MANIFEST        Required. The contents of the manifest to parse
#
# Outputs
#   OUTPUTS         The manifest's media type
#
get_manifest_media_type() {
    OUTPUTS="$(echo "$MANIFEST" | jq -r '.mediaType')"
}

#
# Pushes the given image's manifest to the registry.
#
# Globals
#   MANIFEST        Required. The contents of the manifest to push
#   REFERENCE       Required. The tag or digest to which the manifest will be pushed
#   REGISTRY        Required. The registry to which the manifest will be pushed
#   REPOSITORY      Required. The repository to which the manifest will be pushed
#   SCOPES          Optional. If not given, a scope with be generated to perform this operation
#   TOKEN           Optional. The bearer token to use. If not given, a new token will be generated
#
# Outputs
#   TOKEN           Unchanged if set by caller, otherwise it will contain a valid bearer token
#
push_manifest() {
    __get_token_with_scope "repository:$REPOSITORY:push"

    get_manifest_media_type
    local content_type="$OUTPUTS"

    # make the push request with authorization
    local result=$(curl \
        --data "$MANIFEST" \
        --header "Authorization: Bearer $TOKEN" \
        --header "Content-Type: $content_type" \
        --include \
        --request PUT \
        --show-error \
        --silent \
        --write-out '\n%{http_code}' \
        "https://$REGISTRY/v2/$REPOSITORY/manifests/$REFERENCE")

    local ref=":$REFERENCE"
    local manifest="$REGISTRY/$REPOSITORY:${ref/#:sha256:/@sha256:}"
    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 201 ]]; then
        echo "Failed to push manifest '$manifest', status=$status, details="
        echo "$result"
        return 1
    fi

    echo "Pushed $manifest"
}

#
# Given a manifest list, parse the digests and platforms of all manifests.
#
# Globals
#   MANIFEST_LIST   Required. The contents of a manifest list
#
# Outputs
#   OUTPUTS         A variable containing a JSON document:
#                   [
#                       { "platform": "linux/arm/v7", "digest": "sha256:<value>" },
#                       { "platform": "unknown/unknown", "digest": "sha256:<value>" },
#                       ...
#                   ]
#
get_manifest_digests() {
    OUTPUTS="$(echo "$MANIFEST_LIST" | jq -c '[ .manifests[] | {
        platform: [ .platform | (.os, .architecture, .variant // empty) ] | join("/"),
        digest: .digest
    } ]')"
}

#
# Given a manifest, parse the layers and copy them to the destination repository.
#
# Globals
#   MANIFEST        Required. The contents of the manifest that describes the layers to copy
#   REGISTRY        Required. The registry within which layers will be copied
#   REPO_DST        Required. The repository to which the layers will be copied
#   REPO_SRC        Required. The repository from which the layers will be copied
#   SCOPES          Optional. If not given, a scope with be generated to perform this operation
#   TOKEN           Optional. The bearer token to use. If not given, a new token will be generated
#
# Outputs
#   TOKEN           Unchanged if set by caller, otherwise it will contain a valid bearer token
#
copy_layers() {
    # Ensure the manifest has a mediaType we understand
    local media_type=$(echo "$MANIFEST" | jq -r '.mediaType')
    if [[ "$media_type" != 'application/vnd.oci.image.manifest.v1+json' ]] &&
        [[ "$media_type" != 'application/vnd.docker.distribution.manifest.v2+json' ]]; then
        echo "Manifest has unexpected media type '$media_type'"
        return 1
    fi

    __get_token_with_scope "repository:$REPO_SRC:pull repository:$REPO_DST:pull,push"

    # Parse the layer digests from the manifest
    local digests=( $(echo "$MANIFEST" | jq -r '.. | objects | select(has("digest")) | .digest') )

    local digest
    for digest in ${digests[@]}; do
        # Check if the layer already exists at the destination
        local result=$(curl \
            --head \
            --header "Authorization: Bearer $TOKEN" \
            --show-error \
            --silent \
            --write-out '\n%{http_code}' \
            "https://$REGISTRY/v2/$REPO_DST/blobs/$digest")

        local status=$(echo "$result" | tail -n 1)
        result="$(echo "$result" | head -n -1)"
        if [[ "$status" == 200 ]]; then
            echo "Layer $REGISTRY/$REPO_DST@$digest already exists"
        elif [[ "$status" == 404 ]]; then
            # If the layer doesn't already exist, copy it
            result=$(curl \
                --header "Authorization: Bearer $TOKEN" \
                --include \
                --request POST \
                --show-error \
                --silent \
                --write-out '\n%{http_code}' \
                "https://$REGISTRY/v2/$REPO_DST/blobs/uploads/?mount=$digest&from=$REPO_SRC")

            status=$(echo "$result" | tail -n 1)
            result="$(echo "$result" | head -n -1)"
            if [[ "$status" != 201 ]]; then
                echo "Failed to copy layer to '$REGISTRY/$REPO_DST@$digest'," \
                    "status=$status, details="
                echo "$result"
                return 1
            fi

            echo "Pushed layer $REGISTRY/$REPO_DST@$digest"
        else [[ "$status" != 200 ]]
            echo "Failed to check existence of layer '$REGISTRY/$REPO_DST@$digest'," \
                "status=$status, details="
            echo "$result"
            return 1
        fi
    done
}

#
# Given a manifest list, make a copy of each manifest. If a manifest represents a platform-specific
# image that matches an entry in the given platform map, tag it in the destination. If a manifest
# already exists in the repository at the given tag or digest, it will be overwritten.
#
# Note: Using 'docker buildx imagetools create' won't work here because it always creates a manifest
# list. We want our platform-specific image tags to point directly to platform-specific images to be
# consistent with previous versions. We've also had tooling problems when publishing platform-
# specific images as manifest lists in the past. For these reasons, we use the Docker v2 Registry
# APIs directly.
#
# Globals
#   DST_REPO        Optional. If set, manifests will be coped to $DST_REPO instead of $REPOSITORY
#   PLATFORM_MAP    Optional. A JSON object that defines the mapping of platforms to tag suffixes.
#                   Default is $DEFAULT_PLATFORM_MAP (see definition above)
#   REGISTRY        Required. The registry in which the manifest(s) will be copied
#   REPOSITORY      Required. The repository in which the manifest(s) will be copied
#   TAG             Required. The source manifest list's tag
#
# Outputs
#   None
#
copy_manifests() {
    local dst_repo="${DST_REPO:-}"
    local platform_map="${PLATFORM_MAP:-$DEFAULT_PLATFORM_MAP}"

    # Pull multi-platform image's manifest list
    REFERENCE="$TAG" pull_manifest
    local manifest_list="$OUTPUTS"

    # Make sure the image the caller gave us is actually a manifest list
    MANIFEST="$manifest_list" get_manifest_media_type
    if [[ "$OUTPUTS" != 'application/vnd.docker.distribution.manifest.list.v2+json' ]] &&
        [[ "$OUTPUTS" != 'application/vnd.oci.image.index.v1+json' ]]; then
        echo "Unexpected manifest media type '$OUTPUTS'"
        return 1
    fi

    # Parse out the digests of all manifests referenced in the manifest list
    MANIFEST_LIST="$manifest_list" get_manifest_digests
    local digests="$OUTPUTS"

    # Make sure the manifest list returned a digest for every platform in the caller's map
    local expected="$(echo "$platform_map" | jq -c '[ .[] | .platform ]')"
    local actual="$(echo "$digests" | jq -c '[ .[] | .platform ]')"
    local missed_platforms="$(echo "$expected" | jq -c --argjson actual "$actual" '. - $actual')"
    if $(echo "$missed_platforms" | jq 'length != 0'); then
        echo "Manifest list '$REGISTRY/$REPOSITORY:$TAG' is missing entries for the following" \
            "platform-specific images:"
        echo "$missed_platforms" | jq -r '.[]'
        return 1
    fi

    local platform_digest
    for platform_digest in $(echo "$digests" | jq -r '.[] | "\(.platform),\(.digest)"')
    do
        local platform digest
        IFS=',' read platform digest <<< $(echo "$platform_digest")

        # If the manifest represents a platform-specific image we care about, tag it
        local tag="$(echo "$platform_map" |
            jq -r --arg platform "$platform" --arg tag_prefix "$TAG" '
                .[] | select(.platform == $platform) | "\($tag_prefix)-\(.tag_suffix)"
            ')"

        if [[ -n "$dst_repo" ]] && [[ "$dst_repo" != "$REPOSITORY" ]]; then
            # If we're copying to a different repository, always push the manifest. But first, we
            # might also need to copy the layers referenced in each manifest.
            REFERENCE="$digest" pull_manifest
            local manifest="$OUTPUTS"
            MANIFEST="$manifest" REPO_SRC="$REPOSITORY" REPO_DST="$dst_repo" copy_layers
            MANIFEST="$manifest" REPOSITORY="$dst_repo" REFERENCE="${tag:-$digest}" push_manifest
        elif [[ -n "$tag" ]]; then
            # If we're copying within the same repository, we only need to push tags, not digests
            REFERENCE="$digest" pull_manifest
            local manifest="$OUTPUTS"
            MANIFEST="$manifest" REPOSITORY="$REPOSITORY" REFERENCE="$tag" push_manifest
        fi
    done
}

#
# Given a manifest list, copy it to another repository in the same registry, or to another tag in
# the same repository. If necessary, all contained manifests and layers will be copied too.
#
# Note: We could use 'docker buildx imagetools create' but it isn't appropriate in many situations
# because it always creates a manifest list at the destination. Here, we'll use the Docker v2
# Registry APIs directly.
#
# Globals
#   REGISTRY        Required. The registry in which the manifest list will be copied
#   REPO_DST        Required. The destination repository to which the manifest list will be copied
#   REPO_SRC        Required. The source repository from which the manifest list will be copied
#   TAG             Required. The source tag from which the manifest list will be copied
#   TAGS_ADD        Optional. A JSON array of tags to which the manifest list will be copied, in
#                   addition to TAG.
#
# Outputs
#   None
#
copy_manifest_list() {
    local tags_add="${TAGS_ADD:-}"

    # first make sure TAGS_ADD is a JSON array
    if [[ -n "$tags_add" ]] && [[ $(echo "$tags_add" | jq -r '. | type') != 'array' ]]; then
        echo 'The value of TAGS_ADD must be a JSON array'
        return 1
    fi

    # Copy all child manifests and their layers to the destination repository
    DST_REPO="$REPO_DST" REPOSITORY="$REPO_SRC" copy_manifests

    # pull the source manifest list
    REPOSITORY="$REPO_SRC" REFERENCE="$TAG" pull_manifest
    local manifest="$OUTPUTS"

    # make a list of all tags to push
    local tags=( $(echo "$tags_add" | jq -r --arg tag "$TAG" '. + [ $tag ] | unique | join("\n")') )

    local tag
    for tag in ${tags[@]}
    do
        # push the destination manifest list
        MANIFEST="$manifest" REPOSITORY="$REPO_DST" REFERENCE="$tag" push_manifest
    done
}