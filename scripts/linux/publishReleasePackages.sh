#! /bin/bash

#Pre-Requisites For Running Locally
#Docker Connection to MSINT
#AZ CLI LOGIN

CONFIG_DIR="/root/.repoclient/configs"
PACKAGE_DIR="/root/.repoclient/packages"
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
    echo " -w,  --wdir                   working directory for secrets.Default is $(pwd)."
    echo " -s,  --server                 server name for package upload"
    exit 1
}

###############################################################################
# Functions
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

check_dir() {
    if [[ ! -d $DIR ]]; then
        echo "Directory $DIR does not exist"
        exit 1
    fi

    if [[ ! -d $WDIR ]]; then
        WDIR=$(pwd)
    fi
}

check_server() {
    if [[ -z $SERVER ]]; then
        echo "Server Not Provided"
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
        elif [ $save_next_arg -eq 3 ]; then
            WDIR="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 4 ]; then
            SERVER="$arg"
            save_next_arg=0
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-p" | "--packageos") save_next_arg=1 ;;
            "-d" | "--dir") save_next_arg=2 ;;
            "-w" | "--wdir") save_next_arg=3 ;;
            "-s" | "--server") save_next_arg=4 ;;
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
check_server
echo "OS is $OS_NAME"
echo "Version is $OS_VERSION"
echo "Work Dir is $WDIR"
echo "Package OS DIR is $DIR"

#Debug View of Package Dir Path
#find $DIR | sed -e "s/[^-][^\/]*\// |/g" -e "s/|\([^ ]\)/|-\1/"
ls -al $DIR 

#Cleanup
sudo rm -rf $WDIR/private-key.pem || true
sudo rm -rf $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json || true

#Download Secrets - Requires az login and proper subscription to be selected
az keyvault secret download --vault-name iotedge-packages -n private-key-pem -f $WDIR/private-key.pem
az keyvault secret download --vault-name iotedge-packages -n $OS_NAME-$OS_VERSION-multi-aad -f $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json

#Replace Server Name and Absolute Path of Private-key.pem and replace json
echo $(cat $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json | jq '.AADClientCertificate='\"$CONFIG_DIR/private-key.pem\"'' | jq '.server='\"$SERVER\"'') >$WDIR/$OS_NAME-$OS_VERSION-multi-aad.json

REPOTOOLCMD="docker run -v $WDIR:$CONFIG_DIR -v $DIR:$PACKAGE_DIR --rm msint.azurecr.io/linuxrepos/repoclient:latest repoclient"

#TODO: Enable After Testing
# #Upload packages
# output=$($REPOTOOLCMD -c $CONFIG_DIR/$OS_NAME-$OS_VERSION-multi-aad.json -v v3 package add $PACKAGE_DIR/)
# echo $output
# status=$(echo $output | jq '.status_code')
# submission_id=$(echo $output | jq '.message.submissionId')
# echo "StatusCode: $status"

# submission_id=$(echo $submission_id | tr -d '"')
# echo "Submission ID: $submission_id"

# if [[ $status != "202" ]]; then
#     echo "Received Incorrect Upload Status: $status"
#     exit 1
# fi

#Wait upto 10 Minutes to see if package uploaded
end_time=$((SECONDS + 600))
uploaded=false

#TODO: Remove this hardcoded value after test. Just to make sure YAML Build can reach service
submission_id=61d7d691ea3a771e261fd598
while [[ $SECONDS -lt $end_time ]]; do
    #Check for Successful Upload of Each of the Packages
    output=($($REPOTOOLCMD -c $CONFIG_DIR/$OS_NAME-$OS_VERSION-multi-aad.json -v v3 request check $submission_id | jq '.message.packages[].status'))
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
