#!/bin/bash

# This script is intended to be sourced from other scripts. It expects that 'set -euo pipefail' was
# invoked by the caller.

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
# Given a WWW-Authenticate header containing a "Bearer" challenge as input, parsed the realm,
# service, and scope parameters.
#
# Globals
#   RESPONSE_401    Required. The "401 Unauthorized" response headers which include the
#                   WWW-Authenticate header to be parsed
#
# Outputs
#   OUTPUTS         REALM, SERVICE, and SCOPE in NAME=value format, suitable for sourcing into the
#                   current environment
#
parse_authenticate_header() {
    local auth_header=$(echo "$RESPONSE_401" | grep -i 'WWW-Authenticate: Bearer ' | sed -e 's/[[:space:]]*$//')
    local challenge_vars=()

    for name in realm service scope
    do
        challenge_vars+=( "local $(echo "$auth_header" | grep -Eo "$name=\"[^\"]+\"")" )
    done

    source <(printf '%s\n' "${challenge_vars[@]}")

    OUTPUTS="REALM='$realm'; SERVICE='$service'; SCOPE='$scope'"
}

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
    local cred=

    if [[ "$(echo "$docker_config" | jq --arg reg "$REGISTRY" '.auths | .[$reg] | has("auth")')" == true ]]; then
        # Get credentials directly from config.json
        cred=$(
            echo "$docker_config" | jq --arg reg "$REGISTRY" -r '.auths | .[$reg].auth' | base64 --decode
        )
    elif [[ "$(echo "$docker_config" | jq 'has("credsStore")')" == true ]]; then
        # Get credentials from store
        local store=$(echo "$docker_config" | jq -r '.credsStore')
        cred=$(echo "$REGISTRY" | docker-credential-$store get | jq -r '"\(.Username):\(.Secret)"')
    fi

    OUTPUTS="$cred"    
}

#
# Queries the registry's authorization service for a bearer token that can be used to perform
# operations at the given scope for the given image. The inputs to this function come from the
# WWW-Authenticate header of a 401 Unathorized response.
#
# This function assumes that the needed credentials to acquire the bearer token are available in the
# Docker configuration file at $DOCKER_CONFIG/config.json.
#
# Globals
#   REALM           Required. The token server from which to request the bearer token
#   SERVICE         Required. The registry that hosts the image
#   SCOPE           Required. The resource and requested scope
#   DOCKER_CONFIG   Optional. The path to Docker's config.json. Default value is $HOME/.docker
#
# Outputs
#   OUTPUTS         The bearer token
#
get_bearer_token() {
    REGISTRY="$SERVICE" get_docker_credentials
    local cred="$OUTPUTS"

    local result=$(
        curl --show-error --silent --user "$cred" --write-out '\n%{http_code}' "$REALM?service=$SERVICE&scope=$SCOPE"
    )

    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 200 ]]; then
        echo "Request for bearer token failed, status=$status, details="
        echo "$result"
        return 1
    fi

    OUTPUTS="$(echo "$result" | jq -r '.access_token')"
}

#
# Makes a push request for the given image without authorization, for the purpose of acquiring
# a bearer token. This function does not actually push the manifest, but rather returns the bearer
# token that can be used to push the manifest.
#
# Globals
#   REGISTRY        Required. The registry from which to request a token
#   REPOSITORY      Required. The image repository for which a token will be requested
#   TAG             Required. The tag for which a token will be requested
#
# Outputs
#   OUTPUTS         The bearer token
#
authorize_push_manifest() {
    # Make unauthorized push request
    local result=$(
        curl --include --request PUT --show-error --silent --write-out '\n%{http_code}' \
            "https://$REGISTRY/v2/$REPOSITORY/manifests/$TAG"
    )

    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 401 ]]; then
        echo 'Unauthorized request returned an unexpected result.' \
            "Expected status=401, actual status=$status, details="
        echo "$result"
        return 1
    fi

    # Get bearer token
    RESPONSE_401="$result" parse_authenticate_header
    source <(echo "$OUTPUTS")

    get_bearer_token
}

#
# Pulls the given manifest from the registry.
#
# Globals
#   REFERENCE       Required. The tag or digest for which a manifest will be retreived
#   REGISTRY        Required. The registry from which a manifest will be retreived
#   REPOSITORY      Required. The image repository for which a manifest will be retreived
#   TOKEN           Optional. The bearer token to use. A new token will be generated by default
#
# Outputs
#   OUTPUTS         The retreived manifest
#   TOKEN           Unchanged if set by caller, otherwise it will contain a valid bearer token
#
pull_manifest() {
    TOKEN=${TOKEN:-''}
    if [[ -z "$TOKEN" ]]; then
        # Even though this function only pulls, we always get pull+push authorization because this
        # function is generally used in the context of pulling one manifest to copy (push) it to
        # another tag in the same repository.
        TAG="$REFERENCE" authorize_push_manifest
        TOKEN="$OUTPUTS"
    fi

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

    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 200 ]]; then
        echo 'Request to pull manifest failed, status=$status, details=' \
        echo "$result"
        return 1
    fi

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
#   REGISTRY        Required. The registry to which the manifest will be pushed
#   REPOSITORY      Required. The repository to which the manifest will be pushed
#   TAG             Required. The tag to which the manifest will be pushed
#   TOKEN           Optional. The bearer token to use. A new token will be generated by default
#
# Outputs
#   TOKEN           Unchanged if set by caller, otherwise it will contain a valid bearer token
#
push_manifest() {
    if [[ -z "$TOKEN" ]]; then
        # Even though this function only pulls, we always get pull+push authorization because this
        # function is generally used in the context of pulling one manifest to copy (push) it to
        # another tag in the same repository.
        authorize_push_manifest
        TOKEN="$OUTPUTS"
    fi

    get_manifest_media_type
    local content_type="$OUTPUTS"

    # make the push request with authorization
    result=$(curl \
        --data "$MANIFEST" \
        --header "Authorization: Bearer $TOKEN" \
        --header "Content-Type: $content_type" \
        --include \
        --request PUT \
        --show-error \
        --silent \
        --write-out '\n%{http_code}' \
        "https://$REGISTRY/v2/$REPOSITORY/manifests/$TAG")

    local status=$(echo "$result" | tail -n 1)
    result="$(echo "$result" | head -n -1)"
    if [[ "$status" != 201 ]]; then
        echo 'Request to push manifest failed, status=$status, details=' \
        echo "$result"
        return 1
    fi

    echo "Pushed $REGISTRY/$REPOSITORY:$TAG"
}

#
# Given a manifest list, parse the digests from any platform-specific manifest entries (i.e.,
# manifest entries with platform.os and platform.architecture != "unknown").
#
# Globals
#   MANIFEST_LIST   Required. The contents of a manifest list
#   PLATFORM_FILTER Optional. A JSON array of the platforms for which to return digests.
#
# Outputs
#   OUTPUTS         A variable containing a JSON document:
#                   [
#                       {
#                           "platform": "linux/arm/v7",
#                           "digest": "sha256:<value>"
#                       }, ...
#                   ]
#
get_platform_specific_digests() {
    local filter=${PLATFORM_FILTER:-'null'}

    OUTPUTS="$(echo "$MANIFEST_LIST" | jq -c --argjson filter "$filter" '[
        .manifests[] |
        select(.platform | .architecture != "unknown" and .os != "unknown") |
        { platform: [ .platform | (.os, .architecture, .variant // empty) ] | join("/"), digest: .digest } |
        select(.platform as $candidate | $filter // [ $candidate ] | any(. == $candidate))
    ]')"
}

#
# Given a manifest list, make a copy of each platform-specific manifest according to the given
# mapping of platforms to tags. If a manifest already exists in the repository at the given tag, it
# will be overwritten.
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
copy_platform_specific_manifests() {
    local platform_map=${PLATFORM_MAP:-$DEFAULT_PLATFORM_MAP}

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

    # Parse out the digests of all platform-specific images referenced in the manifest list
    local map_platforms="$(echo "$platform_map" | jq -c '[ .[] | .platform ]')"
    MANIFEST_LIST="$manifest_list" PLATFORM_FILTER="$map_platforms" get_platform_specific_digests
    local platform_specific_digests="$OUTPUTS"

    # Make sure the manifest list returned a digest for every platform in the caller's map
    local list_platforms="$(echo "$platform_specific_digests" | jq -c '[ .[] | .platform ]')"
    local match=$(jq -n \
        --argjson map_platforms "$map_platforms" \
        --argjson list_platforms "$list_platforms" \
        '($map_platforms | sort) == ($list_platforms | sort)')
    if [[ "$match" != 'true' ]]; then
        echo "Manifest list '$REGISTRY/$REPOSITORY:$TAG' does not have the expected entries"
        echo "Expected: $map_platforms"
        echo "Actual: $list_platforms"
        return 1
    fi

    for platform in $(echo "$list_platforms" | jq -r '.[]')
    do
        # Pull platform-specific manifest by digest
        local digest="$(echo "$platform_specific_digests" |
            jq -r --arg platform "$platform" '.[] | select(.platform == $platform) | .digest')"
        REFERENCE="$digest" pull_manifest
        local manifest="$OUTPUTS"

        # Push platform-specific manifest by tag
        local tag="${TAG}-$(echo "$platform_map" |
            jq -r --arg platform "$platform" '.[] | select(.platform == $platform) | .tag_suffix')"
        MANIFEST="$manifest" REPOSITORY="${DST_REPO:-$REPOSITORY}" TAG="$tag" push_manifest
    done
}

#
# Given a manifest, copy it to another repository in the same registry, or to another tag in the
# same repository.
#
# Note: We could use 'docker buildx imagetools create' but it isn't appropriate in many situations
# because it always creates a manifest list at the destination. Here, we'll use the Docker v2
# Registry APIs directly.
#
# Globals
#   REGISTRY        Required. The registry in which the manifest will be copied
#   REF_SRC         Required. The source tag or digest from which the manifest will be copied
#   REPO_DST        Required. The destination repository to which the manifest will be copied
#   REPO_SRC        Required. The source repository from which the manifest will be copied
#   TAG_DST         Required. The destination tag to which the manifest will be copied
#
# Outputs
#   None
#
copy_manifest() {
    # Pull source manifest
    REFERENCE="$REF_SRC" REPOSITORY="$REPO_SRC" pull_manifest
    local manifest="$OUTPUTS"

    # Push destination manifest
    MANIFEST="$manifest" REPOSITORY="$REPO_DST" TAG="$TAG_DST" push_manifest
}