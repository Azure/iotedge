#!/bin/bash

#
# Configures user.email and user.name in git config.
#
# Globals
#   GIT_EMAIL    Optional. If not given, this function is a no-op
#   GIT_NAME     Optional. Defaults to 'IoT Edge Bot'
#
# Output
#   None
#
configure_git_user() {
  local name=${GIT_NAME:-'IoT Edge Bot'}

  if [ -n "$GIT_EMAIL" ]; then
    git config user.email "$GIT_EMAIL"
    git config user.name "$name"
  fi
}

#
# Echoes a remote URL suitable for use in 'git push' commands.
#
# Globals
#   GITHUB_TOKEN Optional. If not given, will assume you have push rights to the repo
#   REMOTE       Optional. Defaults to 'origin'
#
# Output
#   None
#
get_push_url() {
  local remote=${REMOTE:-origin}
  local remote_url

  if [ -n "$GITHUB_TOKEN" ]; then
    remote_url="$(git config --get remote.$remote.url)"
    remote_url="${remote_url/#https:\/\//https:\/\/$GITHUB_TOKEN@}" # add token to URL
  else
    remote_url="$remote"
  fi

  echo "$remote_url"
}

#
# Echoes the first tag of format ${TAG_PREFIX}n.n* reachable from $COMMIT. If the $TAG_PREFIX
# variable is not set, it will default to no prefix (''). If the $COMMIT variable is not set, it
# will default to 'HEAD'. Returns exit status 1 if no tag can be found.
#
get_nearest_version() {
  local commit=${COMMIT:-HEAD}
  local tag_prefix=${TAG_PREFIX:-}
  local version=$(git describe --tags --abbrev=0 --match "${tag_prefix}[0-9].[0-9]*" $commit)
  if [ -z "$version" ]; then
    echo "Error: No release tag matching '${tag_prefix}n.n*' reachable from $commit"
    return 1
  fi
  
  echo "${version/$tag_prefix/}"
}

# don't indent the body of this function
make_core_changelog() {
local prod_version="$1"
local diag_version="$2"
local filepath="$3"

cat <<- EOF > "$filepath"
# $prod_version ($(date --iso-8601=date))

The following Docker images were updated because their base images changed:
* azureiotedge-agent
* azureiotedge-hub
* azureiotedge-simulated-temperature-sensor
* azureiotedge-diagnostics (remains at version $diag_version to match the daemon)

EOF
}

# WARNING: DON'T INDENT THE BODY OF THIS FUNCTION!
# Parameters
#   $1    Metrics collector version
#   $2    Path to changelog
make_metrics_collector_changelog() {
cat <<- EOF > "$2"
# $1 ($(date --iso-8601=date))

The following Docker images were updated because their base images changed:
* azureiotedge-metrics-collector

EOF
}

#
# Creates a release commit appropriate for refreshing core images (agent, hub, temp sensor and
# diagnostics) when their base images have been updated. It ensures there is *no change* to core
# image code by creating the new release commit directly off the previously tagged release commit,
# even if there are newer commits in the branch.
#
#         (E)------+
#         /         \
#    A---B---C---D--(F)
#
# If the last release was tagged at commit B, and two unreleased commits have been added since (C
# and D), this function creates commit E from B, then merges it to create F. As a result, E becomes
# the latest tagged release commit and the release does not include the changes in C or D. The only
# difference between the Docker images produced from B and the Docker images produced from E are the
# base images they're built upon. Also, because this is intended for an images-only release, this
# function does not update the edgelet version file.
#
# Globals
#   GIT_EMAIL    Optional. If not given, will assume git is already configured
#   GITHUB_TOKEN Optional. If not given, will assume you have push rights to the repo
#   REMOTE       Optional. If not given, defaults to 'origin'
#   BRANCH       Optional. If not given, defaults to current branch (e.g., 'release/1.4')
#
# Output
#   None
#
make_project_release_commit_for_core_image_refresh() {
  local remote=${REMOTE:-origin}
  local branch=${BRANCH:-$(git branch --show-current)}

  # checkout code at current release version
  local prev=$(get_nearest_version)
  git checkout "$prev"

  # determine new version
  IFS='.' read -a parts <<< "$prev"
  local next="${parts[0]}.${parts[1]}.$((${parts[2]} + 1))"
  local tags="[\"${parts[0]}.${parts[1]}\"]"

  # determine version of diagnostics image, which must match the edgelet version
  local diag_version=$(
    cat edgelet/version.txt | grep -Eo '^[[:digit:]]+\.[[:digit:]]+\.[[:digit:]]+'
  )

  # update changelog
  make_core_changelog "$next" "$diag_version" 'CHANGELOG.new.md'
  cat CHANGELOG.md >> CHANGELOG.new.md
  mv CHANGELOG.new.md CHANGELOG.md

  # update versionInfo.json
  echo "$(cat versionInfo.json | jq --arg next "$next" '.version = $next')" > versionInfo.json
  git add CHANGELOG.md versionInfo.json

  configure_git_user

  local remote_url=$(get_push_url)

  # commit changes, tag, and push
  git commit -m "Prepare for release $next"
  git tag "$next"
  git push "$remote_url" "$next"

  # merge release commit and push
  git fetch --prune "$remote"
  # check out the very latest to minimize possibility of a rejected push
  git checkout "refs/remotes/$remote/$branch"
  git merge "$next" -m "Merge tag '$next' into $branch"
  git push "$remote_url" "HEAD:$branch"
}

#
# Creates a release commit appropriate for refreshing the metrics collector images when their base
# images have been updated. It ensures there is *no change* to metrics collector image code by
# creating the new release commit directly off the previously tagged release commit, even if there
# are newer commits in the branch.
#
#         (E)------+
#         /         \
#    A---B---C---D--(F)
#
# If the last release was tagged at commit B, and two unreleased commits have been added since (C
# and D), this function creates commit E from B, then merges it to create F. As a result, E becomes
# the latest tagged release commit and the release does not include the changes in C or D. The only
# difference between the Docker images produced from B and the Docker images produced from E are the
# base images they're built upon. Also, because this is intended for an images-only release, this
# function does not update the edgelet version file.
#
# Globals
#   GIT_EMAIL    Optional. If not given, will assume git is already configured
#   GITHUB_TOKEN Optional. If not given, will assume you have push rights to the repo
#   REMOTE       Optional. If not given, defaults to 'origin'
#   BRANCH       Optional. If not given, defaults to current branch (e.g., 'release/1.4')
#
# Output
#   None
#
make_project_release_commit_for_metrics_collector_image_refresh() {
  local remote=${REMOTE:-origin}
  local branch=${BRANCH:-$(git branch --show-current)}
  local git_tag_prefix='metrics-collector-'
  local changelog='edge-modules/metrics-collector/CHANGELOG.md'
  local version_info='edge-modules/metrics-collector/src/config/versionInfo.json'

  # checkout code at current release version
  local prev=$(TAG_PREFIX="$git_tag_prefix" get_nearest_version)
  git checkout "${git_tag_prefix}${prev}"

  # determine new version
  IFS='.' read -a parts <<< "$prev"
  local next="${parts[0]}.${parts[1]}.$((${parts[2]} + 1))"
  local tags="[\"${parts[0]}.${parts[1]}\"]"

  # update changelog
  make_metrics_collector_changelog "$next" 'CHANGELOG.new.md'
  cat $changelog >> CHANGELOG.new.md
  mv CHANGELOG.new.md $changelog

  # update versionInfo.json
  echo "$(cat $version_info | jq --arg next "$next" '.version = $next')" > $version_info
  git add $changelog $version_info

  configure_git_user

  local remote_url=$(get_push_url)
  local git_tag="${git_tag_prefix}${next}"

  # commit changes, tag, and push
  git commit -m "Prepare for Metrics Collector release $next"
  git tag "$git_tag"
  git push "$remote_url" "$git_tag"

  # merge release commit and push
  git fetch --prune "$remote"
  # check out the very latest to minimize possibility of a rejected push
  git checkout "refs/remotes/$remote/$branch"
  git merge "$git_tag" -m "Merge tag '$git_tag' into $branch"
  git push "$remote_url" "HEAD:$branch"
}

#
# Assuming a project release commit for core image refresh has been created and tagged, this
# function gathers information about the release.
#
# Output
#   OUTPUTS      A variable containing a JSON document:
#                {
#                  "changelog": "The text appended to CHANGELOG.md for the latest version, with
#                hex-escaped newlines\\x0A. You can easily deserialize it in bash with
#                'printf -v var $changelog'.\\x0A.",
#                  "version": "1.4.3",
#                  "previous_version": "1.4.2",
#                  "diagnostics_version": "1.4.2",
#                  "tags": ["1.4"]
#                }
#
get_project_release_info() {
  # get the new version and the previous version
  local next=$(get_nearest_version)
  local prev=$(COMMIT="$next~" get_nearest_version)

  # determine tags
  IFS='.' read -a parts <<< "$next"
  local tags="[\"${parts[0]}.${parts[1]}\"]"

  # diagnostics image version must match the edgelet version
  local diag_version=$(
    cat edgelet/version.txt | grep -Eo '^[[:digit:]]+\.[[:digit:]]+\.[[:digit:]]+'
  )

  # get the changelog for the new release
  local tmpfile=$(mktemp)
  echo "$(sed -n "/# $next/,/# $prev/p" CHANGELOG.md)" > "$tmpfile"
  sed -i "$ d" "$tmpfile" # remove last line

  # Azure Pipelines doesn't seem to handle multi-line task variables, so encode to one line
  # See https://developercommunity.visualstudio.com/t/multiple-lines-variable-in-build-and-release/365667
  readarray -t lines < <(cat "$tmpfile")
  local changelog=$(printf '\\x0A%s' "${lines[@]//$'\r'}")
  local changelog=${changelog:4} # Remove leading newline

  rm "$tmpfile"

  OUTPUTS=$(jq -nc \
    --arg changelog "$changelog" \
    --arg next "$next" \
    --arg prev "$prev" \
    --arg diag_version "$diag_version" \
    --argjson tags "$tags" '
    {
      changelog: $changelog,
      version: $next,
      previous_version: $prev,
      diagnostics_version: $diag_version,
      tags: $tags
    }')
}

#
# Assuming a release commit for metrics collector image refresh has been created and tagged, this
# function gathers information about the release.
#
# Output
#   OUTPUTS      A variable containing a JSON document:
#                {
#                  "changelog": "The text appended to edge-modules/metrics-collector/CHANGELOG.md
#                for the latest version, with hex-escaped newlines\\x0A. You can easily deserialize
#                it in bash with 'printf -v var $changelog'.\\x0A.",
#                  "version": "1.1.1",
#                  "previous_version": "1.1.0",
#                  "tags": ["1.1"]
#                }
#
get_metrics_collector_release_info() {
  local git_tag_prefix='metrics-collector-'

  # get the new version and the previous version
  local next=$(TAG_PREFIX="$git_tag_prefix" get_nearest_version)
  local prev=$(TAG_PREFIX="$git_tag_prefix" COMMIT="${git_tag_prefix}${next}~" get_nearest_version)

  # determine docker tags
  IFS='.' read -a parts <<< "$next"
  local docker_tags="[\"${parts[0]}.${parts[1]}\"]"

  # get the changelog for the new release
  local tmpfile=$(mktemp)
  echo "$(sed -n "/# $next/,/# $prev/p" edge-modules/metrics-collector/CHANGELOG.md)" > "$tmpfile"
  sed -i "$ d" "$tmpfile" # remove last line

  # Azure Pipelines doesn't seem to handle multi-line task variables, so encode to one line
  # See https://developercommunity.visualstudio.com/t/multiple-lines-variable-in-build-and-release/365667
  readarray -t lines < <(cat "$tmpfile")
  local changelog=$(printf '\\x0A%s' "${lines[@]//$'\r'}")
  local changelog=${changelog:4} # Remove leading newline

  rm "$tmpfile"

  OUTPUTS=$(jq -nc \
    --arg changelog "$changelog" \
    --arg next "$next" \
    --arg prev "$prev" \
    --argjson tags "$docker_tags" '
    {
      changelog: $changelog,
      version: $next,
      previous_version: $prev,
      tags: $tags
    }')
}

#
# Uses the GitHub API to create a GitHub Release page in the *product* repo for a release that only
# refreshes our core Docker images (agent, hub, temp sensor and diagnostics) when their base images
# have been updated (i.e., no code changes in our project repo). Returns an error status code if
# the required environment variables are empty, or if GitHub returns an error.
#
# Globals
#   BRANCH       Optional. If not given, defaults to 'main'
#   CHANGELOG    Required. Changelog text to add to the Release page.
#   CORE_VERSION Required. Version of modules (except diagnostics) that are part of this release
#   DIAG_VERSION Required. Version of diagnostics module that is part of this release
#   GITHUB_TOKEN Required. The Authorization token passed to GitHub
#   IS_LTS       Optional. If not given, defaults to 'false'
#   REPO_NAME    Required. The GitHub product repository, as 'org/repo'
#
# Output
#   RELEASE_URL  The URL of the created Release page
#
create_github_release_page_in_product_repo() {
  if [[
    -z "$CHANGELOG" ||
    -z "$CORE_VERSION" ||
    -z "$DIAG_VERSION" ||
    -z "$GITHUB_TOKEN" ||
    -z "$REPO_NAME" ]]
  then
    echo 'Error: One or more required arguments are empty'
    return 1
  fi

  local branch=${BRANCH:-main}
  local is_lts=${IS_LTS:-false}
  local name="$CORE_VERSION"
  if [ "$is_lts" != "false" ]; then
    name+=" LTS"
  fi

  local body='Only Docker images are updated in this release.'
  body+=$(echo -e " The daemon remains at version $DIAG_VERSION.\n\n")
  body+="$CHANGELOG"

  local data=$(jq -nc \
    --arg version "$CORE_VERSION" \
    --arg name "$name" \
    --arg branch "$branch" \
    --arg body "$body" '
    {
      tag_name: $version,
      name: $name,
      target_commitish: $branch,
      body: $body
    }')
  
  local response=$(curl \
    -sS \
    -X POST \
    -w "%{http_code}" \
    -H 'Accept:application/vnd.github.v3+json' \
    -H "Authorization:token $GITHUB_TOKEN" \
    -d "$data" \
    "https://api.github.com/repos/$REPO_NAME/releases")

  local code=$(echo "$response" | tail -n 1)
  response=$(echo "$response" | head -n -1)
  echo 'Response from GitHub:'
  echo "$response"

  if [ $code -ge 300 ]; then
    echo "Error: GitHub responded with status code: $code"
    exit 1
  fi

  RELEASE_URL=$(echo "$response" | jq -r '.html_url')
}

#
# Uses the GitHub API to create a GitHub Release page in the *project* repo for a release that only
# refreshes our core Docker images (agent, hub, temp sensor and diagnostics) when their base images
# have been updated (i.e., no code changes in our project repo). Returns an error status code if
# the required environment variables are empty, or if GitHub returns an error.
#
# Globals
#   BRANCH       Optional. If not given, defaults to current branch (e.g., 'release/1.4')
#   CORE_VERSION Required. Version of modules (except diagnostics) that are part of this release
#   GITHUB_TOKEN Required. The Authorization token passed to GitHub
#   IS_LTS       Optional. If not given, defaults to 'false'
#   RELEASE_URL  Required. The URL of the already-created Release page in the product repo
#   REPO_NAME    Required. The GitHub project repository, as 'org/repo'
#
# Output
#   None
#
create_github_release_page_for_core_images_in_project_repo() {
  if [[ -z "$CORE_VERSION" || -z "$GITHUB_TOKEN" || -z "$RELEASE_URL" || -z "$REPO_NAME" ]]; then
    echo 'Error: One or more required arguments are empty'
    return 1
  fi

  local branch=${BRANCH:-$(git branch --show-current)}
  local is_lts=${IS_LTS:-false}
  local name="$CORE_VERSION"
  if [ "$is_lts" != "false" ]; then
    name+=" LTS"
  fi

  local body='The project source code is linked below.'
  body+=" Head to the [product release page]($RELEASE_URL) for the changelog."

  local data=$(jq -nc \
    --arg version "$CORE_VERSION" \
    --arg name "$name" \
    --arg branch "$branch" \
    --arg body "$body" '
    {
      tag_name: $version,
      name: $name,
      target_commitish: $branch,
      body: $body
    }')

  local response=$(curl \
    -sS \
    -X POST \
    -w "%{http_code}" \
    -H 'Accept:application/vnd.github.v3+json' \
    -H "Authorization:token $GITHUB_TOKEN" \
    -d "$data" \
    "https://api.github.com/repos/$REPO_NAME/releases")

  local code=$(echo "$response" | tail -n 1)
  echo "Response from GitHub:"
  echo "$response" | head -n -1

  if [ $code -ge 300 ]; then
    echo "Error: GitHub responded with status code: $code"
    exit 1
  fi
}

#
# Uses the GitHub API to create a GitHub Release page in the *project* repo for a release that only
# refreshes our Metrics Collector Docker images when their base images have been updated (i.e., no
# code changes in our project repo). Returns an error status code if the required environment
# variables are empty, or if GitHub returns an error.
#
# Globals
#   COMMITISH    Optional. If not given, defaults to current branch (e.g., 'release/1.4')
#   CHANGELOG    Required. Changelog text to add to the Release page.
#   VERSION      Required. Version of Metrics Collector module for this release
#   GITHUB_TOKEN Required. The Authorization token passed to GitHub
#   REPO_NAME    Required. The GitHub project repository, as 'org/repo'
#
# Output
#   None
#
create_github_release_page_for_metrics_collector_in_project_repo() {
  if [[ -z "$CHANGELOG" || -z "$VERSION" || -z "$GITHUB_TOKEN" || -z "$REPO_NAME" ]]; then
    echo 'Error: One or more required arguments are empty'
    return 1
  fi

  local commitish=${COMMITISH:-$(git branch --show-current)}
  local body="$CHANGELOG"

  local data=$(jq -nc --arg version "$VERSION" --arg commitish "$commitish" --arg body "$body" '
    {
      tag_name: "metrics-collector-\($version)",
      name: "Metrics Collector \($version)",
      target_commitish: $commitish,
      body: $body,
      make_latest: "false"
    }
  ')

  local response=$(curl \
    -sS \
    -X POST \
    -w "%{http_code}" \
    -H 'Accept:application/vnd.github.v3+json' \
    -H "Authorization:token $GITHUB_TOKEN" \
    -d "$data" \
    "https://api.github.com/repos/$REPO_NAME/releases")

  local code=$(echo "$response" | tail -n 1)
  echo "Response from GitHub:"
  echo "$response" | head -n -1

  if [ $code -ge 300 ]; then
    echo "Error: GitHub responded with status code: $code"
    exit 1
  fi
}