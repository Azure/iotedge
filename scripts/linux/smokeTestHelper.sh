#! /bin/bash

get-latest-version-apt()
{
  # Use the 'apt' to get the latest version string from the linux repository
  # $1 - package name
  # $2 - the Nth latest package (ex. 1 is the latest, 2 is the second latest), Optional
  
  apt-cache madison $1 | awk -F '|' '{gsub(/ /,"",$2); print $2}' | sort --version-sort -r -u | sed -n ${2:-1}p
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

download-artifact-from-pmc-apt()
{
  # Download artifact for a package named ($1) using uri acquired by `apt` to a location ($2).
  # Then suffix the name of the artifact with "_pmc".
  #
  # $1 - downloading an artifact for a package name (latest by default)
  #    Note: To download a specific version, the ($1) can be specified as "aziot-edge=1.2.10-1"
  # $2 - target output directory

  pkgName=${1%=*}
  uri=$(apt-get install --reinstall --print-uris -qq $1 | cut -d"'" -f2 | grep "/$pkgName/")
  [[ -z "$uri" ]] && { echo "[FAIL] Package ($1) cannot be found in a known linux repository"; exit 1;}
  artifactName=${uri##*/}
  targetArtifactPath="$(realpath $2)/${artifactName%*.deb}_pmc.deb"
  
  cmd="wget -q $uri -O $targetArtifactPath"
  echo "Running: $cmd"
  $cmd
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

  sudo sed -i 's/bionic/focal/g' /etc/apt/sources.list && \
  sudo rm -f /etc/apt/sources.list.d/microsoft-prod.list && \
  wget https://packages.microsoft.com/config/ubuntu/20.04/prod.list  -O /etc/apt/sources.list.d/microsoft-prod.list && \
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