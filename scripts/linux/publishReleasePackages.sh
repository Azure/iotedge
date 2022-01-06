#! /bin/bash

###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -p,  --packageos              packageos: ubuntu18.04|ubuntu20.04|debian9"
    echo " -d,  --dir                    package directory to publish"
    exit 1
}

###############################################################################
# Function to obtain the underlying OS and check if supported
###############################################################################
check_os() {
    if [[ "$PACKAGE_OS" == "ubuntu18.04" ]]; then
        OS_NAME="ubuntu"
        OS_VERSION="bionic"
    elif [[ "$PACKAGE_OS" == "ubuntu20.04" ]]; then
        OS_NAME="ubuntu"
        OS_VERSION="focal"
    elif [[ "$PACKAGE_OS" == "debian9" ]]; then
        OS_NAME="debian"
        OS_VERSION="stretch"
    else
        echo "Unsupported OS $PACKAGE_OS"
        exit 1
    fi
}

###############################################################################
# Function to check if the directory for packages exist
###############################################################################
check_dir() {
    if [[ ! -d $DIR ]]; then
        echo "Directory $DIR does not exist"
        exit 1
    fi
}

###############################################################################
# Obtain and validate the options supported by this script
###############################################################################
process_args() {
    save_next_arg=0
    for arg in "$@"; do
        if [ $save_next_arg -eq 1 ]; then
            PACKAGE_OS="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 2 ]; then
            DIR="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-p" | "--packageos") save_next_arg=1 ;;
            "-d" | "--dir") save_next_arg=2 ;;
            *) usage ;;
            esac
        fi
    done
}

###############################################################################
# Process Start
###############################################################################

process_args "$@"
check_os
check_dir

#Cleanup
sudo rm -rf private-key.pem || true
sudo rm -rf $OS_NAME-$OS_VERSION-multi-aad.json || true

#Install Repo-Client Tool - Requires Corpnet Connected Machine
# sudo curl http://tux-devrepo.corp.microsoft.com/keys/tux-devrepo.asc >tux-devrepo.asc
# sudo apt-key add tux-devrepo.asc
# echo "deb [arch=amd64] http://tux-devrepo.corp.microsoft.com/repos/tux-dev/ xenial main" | sudo tee /etc/apt/sources.list.d/tuxdev.list
# sudo apt-get install -y --no-install-recommends azure-repoapi-client
$value=$(az keyvault secret show -n registry-key --vault-name msint-community --query value)
echo $value

#Download Secrets - Requires az login and proper subscription to be selected
az keyvault secret download --vault-name iotedge-packages -n private-key-pem -f private-key.pem
az keyvault secret download --vault-name iotedge-packages -n $OS_NAME-$OS_VERSION-multi-aad -f $OS_NAME-$OS_VERSION-multi-aad.json
echo $(cat $OS_NAME-$OS_VERSION-multi-aad.json | jq '.AADClientCertificate="private-key.pem"') >$OS_NAME-$OS_VERSION-multi-aad.json

#Upload packages
output=$(repoclient -c $OS_NAME-$OS_VERSION-multi-aad.json -v v3 package add $DIR/)
echo $output
status=$(echo $output | jq '.status_code')
submission_id=$(echo $output | jq '.message.submissionId')
echo "StatusCode: $status"

submission_id=$(echo $submission_id | tr -d '"')
echo "Submission ID: $submission_id"

if [[ $status != "202" ]]; then
    echo "Received Incorrect Upload Status: $status"
    exit 1
fi

#Wait upto 10 Minutes to see if package uploaded
end_time=$((SECONDS + 600))
uploaded=false
while [[ $SECONDS -lt $end_time ]]; do
    #Check for Successful Upload of Each of the Packages
    output=($(repoclient -c $OS_NAME-$OS_VERSION-multi-aad.json -v v3 request check $submission_id | jq '.message.packages[].status'))
    for item in "${output[@]}"; do
        if [[ $item != "\"Success\"" ]]; then
            echo "Package Not Uploaded Yet, Status : $item"
            uploaded=false
            break
        else
            echo "Package Uploaded"
            uploaded=true

        fi
    done
    if [[ $uploaded == false ]]; then
        echo "Retrying.."
        sleep 10
    else
        break
    fi

done

if [[ $uploaded == false ]]; then
    echo "Package(s) Upload Failed"
    exit 1
else
    echo "Packages Uploaded"
fi
