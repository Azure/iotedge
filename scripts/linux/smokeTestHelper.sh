#! /bin/bash

get-latest-version-apt()
{
  # Use the 'apt' to get the latest version string from the linux repository
  # $1 - package name
  # $2 - the Nth latest package (ex. 1 is the latest, 2 is the second latest), Optional
  
  apt-cache madison $1 | awk -F '|' '{gsub(/ /,"",$2); print $2}' | sort --version-sort -r -u | sed -n ${2:-1}p
}

get-latest-image-publication-buildId()
{
  # Look through "Azure-IoT-Edge-Core Images Publish" pipeline for the latest successful image publication run given the github branch
  # $1 - branch
  # Note PipelineID = 223957 is  "Azure-IoT-Edge-Core Images Publish"
  [[ -z "$DEVOPS_PAT" ]] && { echo "\$DEVOPS_PAT variable is required to access Azure DevOps"; exit 1; }

  pipelineRuns=$(curl -s -u ":$DEVOPS_PAT" --request GET "https://dev.azure.com/msazure/One/_apis/pipelines/223957/runs?api-version=6.0")
  OLD_IFS=$IFS
  IFS=' ' buildIds=( $(echo $pipelineRuns | jq '."value"[] | select(.result == "succeeded").id' | tr '\n' ' ') )
  IFS=$OLD_IFS
  for buildId in "${buildIds[@]}"
  do
    result=$(curl -s -u ":$DEVOPS_PAT" --request GET "https://dev.azure.com/msazure/One/_apis/build/builds/$buildId?api-version=6.0" | jq "select(.sourceBranch == \"refs/heads/$1\")")
    [[ -z "$result" ]] || { echo $buildId; return 0; }
  done

  echo "Cannot find an associate build for branch ($1) from buildIds: ${buildIds[@]}"
  exit -1;
}

get-build-logs-from-task()
{
  # Get pipeline build logs for a given Task name for a given buildId
  # $1 - buildId
  # $2 - task display name
  [[ -z "$DEVOPS_PAT" ]] && { echo "\$DEVOPS_PAT variable is required to access Azure DevOps"; exit 1; }

  buildId=$1
  taskDisplayName=$2

  logId=$(curl -s -u ":$DEVOPS_PAT" --request GET "https://dev.azure.com/msazure/One/_apis/build/builds/$buildId/timeline?api-version=6.0" | jq ".records[] | select(.name == \"$taskDisplayName\").log.id")

  [[ -z "$logId" ]] && { echo "Failed to get log id for task ($taskDisplayName) with buildId ($buildId)"; exit 1; }
  curl -s -u ":$DEVOPS_PAT" --request GET "https://dev.azure.com/msazure/One/_apis/build/builds/$buildId/logs/$logId?api-version=6.0"
}

get-image-sha-from-devops-logs()
{
  # Parse DevOps build logs to get container and its sha
  # example of output: 
  # /public/azureiotedge-agent:1.1.15  sha256:e203b6f3f9a3edff8a98a19894a9b1ca295bfd80e5412fdff5a7bc037bd04dcf
  # /public/azureiotedge-agent:1.1.15-linux-amd64  sha256:3a8ef9d5f1ccf57dc6ebdc413e1312cec805ee340470b7c0808e715683eed5c9
  # /public/azureiotedge-agent:1.1.15-linux-arm64v8  sha256:c75323754fc45d74f7ec3458876bfbc046e4e73fb800cc301a7bc6698d1856e4
  # /public/azureiotedge-agent:1.1.15-linux-arm32v7  sha256:1fe533ae64e73141154afcfe1b1244d765a61cc2cdfd5c0a8fa9303fe60a5951
  # /public/azureiotedge-agent:1.1.15-windows-amd64  sha256:a192aa2b9e203493ff69bb8dd5b0c7807664ff30f129bde4feb1988cac178929

  # $1 - logs from DevOps image publication task
  logs=$1
  moduleName=$(echo "$logs" | grep " image: " | head -1 | awk '{print substr($3, 4, length($3)-4)}')
  moduleSha=$(echo "$logs" | grep "Digest: " | awk '{print $3}')
  echo "$moduleName  $moduleSha" 
  echo "$logs" | grep " is digest sha256:" | awk '{print substr($5, 6, length($5)-7)"  "substr($8, 1, length($8)-1)}'
}

check-matching-version()
{
  # Compare if ($2) is a substring of ($1)
  # $1 - version string 1 (string)
  # $2 - version string 2 (string)
  # $3 - pkgName
  
  if [[ ! "$1" =~ "$2" ]]; then
    echo "[FAIL] Mismatch Version: The latest $pkgName package version on linux repo is $1 (expecting: $2)"
    exit 1
  fi
  echo "[PASS] $pkgName linux repository artifact version check ($1)"
  echo ""
}

check-file-size-limit()
{
  # Compare if the incoming file size is within $sizePercentThrehold percent of
  # the golden package standard. That is 
  #  goldenPkgSize*(100-sizePercentThrehold)/100 < incomingPkgSize < goldenPkgSize*(100+sizePercentThrehold)/100
  #
  # $1 - incoming artifact
  # $2 - golden standard artifact
  # $3 - acceptable size difference percentage as a whole integer number

  incomingArtifact=$1
  goldenArtifact=$2

  # Check if the is between 0 <= sizePercentThrehold <= 100
  [[ "100" -lt "$3" ]] && { echo "[FAIL] The \$sizePercentThrehold ($3) needs to be lesser than 100"; exit 1; }
  [[ "0" -gt "$3" ]] && { echo "[FAIL] The \$sizePercentThrehold ($3) needs to be greater than 0"; exit 1; }

  # Do the actual size checking
  incomingSize=$(stat -c%s $incomingArtifact)
  goldenSize=$(stat -c%s $goldenArtifact)
  maxAcceptingArtifactSize=$(echo "scale=0; ($goldenSize*$sizePercentThrehold/100)+$goldenSize" | bc)
  [[ "$incomingSize" -lt "$maxAcceptingArtifactSize" ]] || { echo "[FAIL] The current artifact size ($incomingSize) is more than $edgelet.maxPercentAllowed% larger than previous version file ($goldenSize)"; exit 1; }
  minAcceptingArtifactSize=$(echo "scale=0; $goldenSize-($goldenSize*$sizePercentThrehold/100)" | bc)
  [[ "$incomingSize" -gt "$minAcceptingArtifactSize" ]] || { echo "[FAIL] The current artifact size ($incomingSize) is more than $edgelet.maxPercentAllowed% smaller than previous version file ($goldenSize)"; exit 1; }
  echo "[PASS] The current artifact size ($incomingSize) is withing the range of $minAcceptingArtifactSize Byte to $maxAcceptingArtifactSize Byte" 
}

check-version-content-difference()
{
  # Compare if the content of two files are difference. 
  #   Return true - if the two files are different
  #   Return false - if the two files are byte-exact identical.
  #
  # $1 - path to file1
  # $2 - path to file2

  # Compare the previous and current artifact content diff (by byte)
  if cmp --silent $1 $2; then
    echo "[FAIL] The current ($1) and previous ($2) artifacts are identical."
    exit 1;
  fi
  echo "[PASS] The current and previous artifact version have a different content"
}

check-github-pmc-artifacts-similarity()
{
  # Compare the artifact if github and pmc are the same artifact
  #  Return true - if the artifacts from two sources are identical
  #  Return false - if the artifacts from two sources are NOT identical
  #
  # $1 - path to PMC artifact
  # $2 - path to Github artifact directory
  # $3 - package name

  echo ""
  ghArtifact="$2/$3_$gitHubArtifactSuffix"
  echo "The artifact from GitHub: $ghArtifact"
  [[ -f "$ghArtifact" ]] || { echo "[FAIL] The artifact from GitHub doesn't exist ($ghArtifact)"; exit 1; }
  if cmp --silent $1 $ghArtifact; then
    echo "[PASS] The artifact content in PMC and the GitHub are identical."
  else
    echo "[FAIL] The artifact content in PMC and the GitHub are NOT identical."
    exit 1;
  fi
}

check-pmc-images-availability()
{
  # Check docker images availability in the PMC against all image tags.
  #
  # $1 - Github branch name

  sourceBranchName=$1
  taskDisplayNames=("Publish Edge Agent Manifest" "Publish Edge Hub Manifest" "Publish Temperature Sensor Manifest" "Publish Diagnostic Module Manifest")
  for taskDisplayName in "${taskDisplayNames[@]}"
  do
    # Amalgam of test cases to test docker images in mcr
    echo $'\n\n================================================\n\n'
    check-pmc-image-tags-availability "$sourceBranchName" "$taskDisplayName"
  done
}

check-pmc-image-tags-availability()
{
  # Check if the images pulled using docker image name from MCR resolves the to the same image SHA 
  # which "Azure-IoT-Edge-Core Images Publish" pipeline claimed to publish to the MCR. 
  #
  # $1 - branch name
  # $2 - task display name (use to reference the image module)
  
  branchName=$1
  pipelineDisplayName=$2
  echo "Checking images published for $branchName ($pipelineDisplayName)"

  buildId=$(get-latest-image-publication-buildId "$branchName")
  echo "BuildId: $buildId"

  logs=$(get-build-logs-from-task "$buildId" "$pipelineDisplayName")

  imageHashMap=$(get-image-sha-from-devops-logs "$logs")
  echo "Checking the following images publication:"
  echo "$imageHashMap"
  echo ""

  OLD_IFS=$IFS; 
  IFS=' '
  nameList=($(echo "$imageHashMap" | awk '{sub(/.*azureiotedge/, "azureiotedge", $1); print $1}' | tr '\n' ' '))
  shaList=($(echo "$imageHashMap" | awk '{print $2}' | tr '\n' ' '))
  pmcImages=($(prepare-docker-image-names "$imageHashMap" | tr '\n' ' '))
  
  IFS=''
  pullResults=()
  for image in "${pmcImages[@]}"
  do
    pullResults+=($(docker pull $image))
  done

  IFS=$OLD_IFS
  isFailed=false
  for i in $(seq 0 $((${#nameList[@]} - 1 )) );
  do
    echo "${pullResults[$i]}" | grep -q "${nameList[$i]}" \
      && { echo "[PASS] ${nameList[$i]} is available."; } \
      || { echo "[FAIL] The image name:tag ( ${nameList[$i]} ) is missing."; isFailed=true; }
    echo "${pullResults[$i]}" | grep -q "${shaList[$i]}" \
      && { echo "[PASS] ${shaList[$i]} is available."; } \
      || { echo "[FAIL] The sha256 ( ${shaList[$i]} ) is missing."; isFailed=true; }
  done

  if $isFailed
  then 
    echo "##vso[task.logissue type=error]The sha256 is missing."
    exit 1;
  fi
}

download-artifact-from-pmc-apt()
{
  # Download artifact for a package named ($1) using uri acquired by `apt` to a location ($2).
  # Then suffix the name of the artifact with "_pmc".
  #
  # $1 - downloading an artifact for a package name (latest by default)
  #    Note: To download a specific version, the ($1) can be specified as "aziot-edge=1.2.10-1"
  # $2 - target output directory

  pkgName=${1%=*}
  sudo apt --fix-broken install 
  uri=$(apt-get install --reinstall --print-uris -qq $1 | cut -d"'" -f2 | grep "/$pkgName/")
  [[ -z "$uri" ]] && { echo "[FAIL] Package ($1) cannot be found in a known linux repository"; exit 1;}
  artifactName=${uri##*/}
  targetArtifactPath="$(realpath $2)/${artifactName%*.deb}_pmc.deb"
  
  cmd="wget -q $uri -O $targetArtifactPath"
  echo "Running: $cmd"
  $cmd
}

prepare-docker-image-names()
{
  # The function does the following: 
  # 1. Parse the result from get-image-sha-from-devops-logs()'s image hashmap to get list of image names
  # 2. Set the docker images namespace to be "mcr.microsoft.com"
  # 3. Return list of docker to be pulled from MCR
  #
  # $1 - String result from get-image-sha-from-devops-logs()
  imageHashMap=$1

  OLD_IFS=$IFS
  IFS=' ' images=( $(echo "$imageHashMap" | awk '{print $1}' | tr '\n' ' ') )
  IFS=$OLD_IFS

  pmcImages=()
  for image in "${images[@]}"
  do
    echo "mcr.microsoft.com/${image##*/}"
  done
}

wait-for-dpkg-lock()
{
  # Wait for task 'apt.systemd.daily' to be completed. This is helpful if you later
  # need to use `apt`. 
  #
  # $1 - wait time in seconds

  waitAttempts=$1
  sleepDuration=10
  for (( i=0; $i<$waitAttempts; i++ ))
  do
    if [[ $(ps aux | grep -c apt.systemd.daily) == 1 ]]; then
      break
    fi
    sleep $sleepDuration
  done
}

setup-focal-source-apt()
{
  # 1ES image for Ubuntu20.04 (Focal) actually has a misconfigured apt repo source
  # This function correct the repo source by directly configure the apt to point to 
  # the Microsoft Linux Repository for Focal.

  for file in $(ls /etc/apt/sources.list.d/*.list)
  do
    sudo sed -i 's/bionic/focal/g' $file
  done

  sudo sed -i 's/bionic/focal/g' /etc/apt/sources.list
  sudo rm -f /etc/apt/sources.list.d/microsoft-prod.list
  sudo wget -c https://packages.microsoft.com/config/ubuntu/20.04/prod.list -O /etc/apt/sources.list.d/microsoft-prod.list
  sudo apt --fix-broken install && \
  sudo apt update -y && \
  sudo DEBIAN_FRONTEND=noninteractive apt-get -y -o Dpkg::Options::="--force-confdef" -o Dpkg::Options::="--force-confold" dist-upgrade && \
  sudo apt update -y && \
  sudo apt upgrade -y
}

setup-config-apt()
{
  # Download the `apt` config file from the provided uri ($1), install it, and update the apt
  #
  # $1 - uri for the config deb package
  # Ref: https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric?view=iotedge-2020-11&tabs=azure-portal%2Cubuntu

  # For ARM machine, we need to wait for the apt-get daily update to be done first before we can proceed.
  wait-for-dpkg-lock 120

  echo "Setup source artifact repository:"
  echo "$1"
  wget "$1" -O packages-microsoft-prod.deb &&
  sudo dpkg --force-confnew -i packages-microsoft-prod.deb &&
  rm packages-microsoft-prod.deb &&
  sudo apt-get update -y
}

test-released-metadata()
{
  # Amalgam of test cases to test PMC or GitHub release page.
  #
  # $1 - package name (string)
  # $2 - version string (string)

  pkgName=$1
  version=$2

  echo "Get the latest version string from linux repository"
  # Note: $latestVersionApt includes the trailing revision: "1.2.10-1"
  latestVersionApt=$(get-latest-version-apt "$pkgName" 1)
  check-matching-version "$latestVersionApt" "$version" "$pkgName"
}

test-released-artifact()
{
  # Amalgam of test cases to test PMC or GitHub release page.
  #
  # $1 - package name (string)
  # $2 - artifact version string (string)

  # Trying my best to make sure the script can easily be updated to run locally
  pkgName=$1
  artifactVersion=$2
  artifactPath=$3
  gitHubArtifactSuffix=$4
  sizePercentThrehold=$5
  isCheckPreviousPkg=$6

  echo "Download PMC artifacts ($pkgName)"
  wgetRespCurrent=$(download-artifact-from-pmc-apt "$pkgName" "$artifactPath")
  echo $wgetRespCurrent
  currentArtifact=${wgetRespCurrent##* }
  echo "[PASS] The artifacts can be downloaded from the Microsoft Linux Package Repository"
  echo "The releasing artifact: $currentArtifact"
  echo ""

  if $isCheckPreviousPkg; then
    # Download the previous release artifact for comparison
    wgetRespPrev=$(download-artifact-from-pmc-apt "$pkgName=$(get-latest-version-apt "$pkgName" 2)" "$artifactPath")
    echo $wgetRespPrev
    previousArtifact=${wgetRespPrev##* }
    echo "The previous version artifact: $previousArtifact"

    check-file-size-limit "$currentArtifact" "$previousArtifact" "sizePercentThrehold"
    check-version-content-difference "$currentArtifact" "$previousArtifact"
  fi

  check-github-pmc-artifacts-similarity "$currentArtifact" "$artifactPath" "$pkgName"
}

test-released-images()
{
  # Amalgam of test cases to test docker images availablity on MCR.
  # $1 - Source Branch Name i.e. "release/1.3"
  sourceBranchName=$1

  check-pmc-images-availability "$sourceBranchName"
}