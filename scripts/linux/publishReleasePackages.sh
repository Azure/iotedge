#! /bin/bash

#Pre-Requisites For Running Locally
#Docker Connection to MSINT
#AZ CLI LOGIN
SCRIPT_NAME=$(basename $0)
CONFIG_DIR="/root/.repoclient/configs"
PACKAGE_DIR="/root/.repoclient/packages"
SKIP_UPLOAD="false"
###############################################################################
# Print usage information pertaining to this script and exit
###############################################################################
function usage() {
    echo "$SCRIPT_NAME [options]"
    echo ""
    echo "options"
    echo " -h,  --help                   Print this help and exit."
    echo " -p,  --packageos              Package OS"
    echo " -d,  --dir                    package directory to publish"
    echo " -w,  --wdir                   working directory for secrets.Default is $(pwd)."
    echo " -s,  --server                 server name for package upload"
    echo " -g,  --ghubpat                value of github pat. Required only if uploading to github"
    echo " -v,  --version                version of the release."
    echo " -u,  --skip-upload            Skips Upload and Only Creates Release for Github. Defaults to false"
    echo " -b,  --branch-name            Git Branch Name"
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
    elif [[ "$PACKAGE_OS" == "ubuntu22.04" ]]; then
        OS_NAME="ubuntu"
        OS_VERSION="jammy"
    elif [[ "$PACKAGE_OS" == "debian10" ]]; then
        OS_NAME="debian"
        OS_VERSION="buster"
    elif [[ "$PACKAGE_OS" == "debian11" ]]; then
        OS_NAME="debian"
        OS_VERSION="bullseye"
    elif [[ "$PACKAGE_OS" == "centos7" ]]; then
        OS_NAME="centos"
        OS_VERSION="7"
    elif [[ "$PACKAGE_OS" == "redhat8" ]]; then
        OS_NAME="redhat"
        OS_VERSION="8"
    elif [[ "$PACKAGE_OS" == "redhat9" ]]; then
        OS_NAME="redhat"
        OS_VERSION="9"
    else
        echo "Unsupported OS: $PACKAGE_OS"
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
        elif [ $save_next_arg -eq 5 ]; then
            GITHUB_PAT="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 6 ]; then
            VERSION="$arg"
            save_next_arg=0   
        elif [ $save_next_arg -eq 7 ]; then
            SKIP_UPLOAD="$arg"
            save_next_arg=0
        elif [ $save_next_arg -eq 8 ]; then
            BRANCH_NAME="$arg"
            save_next_arg=0   
        else
            case "$arg" in
            "-h" | "--help") usage ;;
            "-p" | "--packageos") save_next_arg=1 ;;
            "-d" | "--dir") save_next_arg=2 ;;
            "-w" | "--wdir") save_next_arg=3 ;;
            "-s" | "--server") save_next_arg=4 ;;
            "-g" | "--ghubpat") save_next_arg=5 ;;
            "-v" | "--version") save_next_arg=6 ;;
            "-u" | "--skip-upload") save_next_arg=7 ;;
            "-b" | "--branch-name") save_next_arg=8 ;;
            *) usage ;;
            esac
        fi
    done
}


#######################################
# NAME: 
#    publish_to_microsoft_repo
#
# DESCRIPTION:
#    The function upload artifacts to Microsoft Linux Package Repository which is multiarch 
#    repository at packages.microsoft.com (PMC). The upload of the artifacts is done via RepoClient
#    app (which now avaiable as a docker image)
#
#    The script simply does the follow:
#    1. Pull clean docker image for RepoClient app
#    2. To upload the artifacts, the function runs the RepoClient image against 
#       the config file an the uploading artifacts.
#    3. Validate if the artifacts are readily available on PMC.
# GLOBALS:
#    BRANCH_NAME ________________ Source Branch name            (string)
#    CONFIG_DIR _________________ Path to RepoClient config file(string)
#    OS_NAME ____________________ Operating System name         (string)
#    OS_VERSION _________________ Operating System version      (string)
#    PACKAGE_DIR ________________ Path to artifact directory    (string)
#    SERVER _____________________ Server name for package upload(string)
#    WDIR _______________________ Working directory for secrets (string)
#
# OUTPUTS:
#    Uploaded linux artifacts in packages.microsoft.com
#######################################
publish_to_microsoft_repo()
{
#Cleanup
sudo rm -rf $WDIR/private-key.pem || true
sudo rm -rf $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json || true

#Download Secrets - Requires az login and proper subscription to be selected
az keyvault secret download --vault-name iotedge-packages -n private-key-pem -f $WDIR/private-key.pem
az keyvault secret download --vault-name iotedge-packages -n $OS_NAME-$OS_VERSION-multi-aad -f $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json

#Replace Server Name and Absolute Path of Private-key.pem and replace json
echo $(cat $WDIR/$OS_NAME-$OS_VERSION-multi-aad.json | jq '.AADClientCertificate='\"$CONFIG_DIR/private-key.pem\"'' | jq '.server='\"$SERVER\"'') >$WDIR/$OS_NAME-$OS_VERSION-multi-aad.json

REPOTOOLCMD="docker run -v $WDIR:$CONFIG_DIR -v $DIR:$PACKAGE_DIR --rm msint.azurecr.io/linuxrepos/repoclient:latest repoclient"

#Upload packages
output=$($REPOTOOLCMD -c $CONFIG_DIR/$OS_NAME-$OS_VERSION-multi-aad.json -v v3 package add $PACKAGE_DIR/)
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

#Wait upto 30 Minutes to see if package uploaded
end_time=$((SECONDS + 1800))
uploaded=false

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
        sleep 30
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

}


#######################################
# NAME: 
#    publish_to_github
#
# DESCRIPTION:
#    The function has two operating mode depending the value of $SKIP_UPLOAD
#
#    If SKIP_UPLOAD=false, the script creates a github release page on /azure-iotedge 
#    repository with a VERSION tag to the latest commit. The release page comprises of
#      - Change log as a description which is parsed from CHANGELOG.MD
#      - Renamed production artifacts from the build pipeline in the format of
#        <component>_<version>_<os>_<architecture>.<fileExtension> 
#        i.e. iotedge_1.1.13-1_debian11_arm64.deb
#
#    If SKIP_UPLOAD=true, the script creates a DRAFT github release page on /azure-iotedge
#    without a github tag AND no production artifacts are uploaded to draft.
#    
# GLOBALS:
#    BRANCH_NAME ________________ Source Branch name            (string)
#    GITHUB_PAT _________________ Github Personal Access Token  (string)
#    SKIP_UPLOAD ________________ Skip Github artifact upload   (bool)
#      if false, upload artifacts to github release page
#      if true, create the release page in draft mode without artifacts uploaded.
#    VERSION ____________________ Current release version       (string)
#    WDIR _______________________ Current work directory        (string)
#
# OUTPUTS:
#    Github release page on /azure-iotedge repository
#######################################
publish_to_github()
{
    # Investigate if this can be derived from a commit, Hardcode for now.
    if [[ -z $BRANCH_NAME ]]; then
        echo "No Branch Name Provided"
        exit 1
    fi

    branch_name=${BRANCH_NAME/"refs/heads/"/""}
    echo "Branch Name is $branch_name"

    # Using relative path from this script to source the helper script
    source "$(dirname "$(realpath "$0")")/github/updateLatestVersion.sh"
    latest_release=$(get_latest_release_per_branch_name)
    echo "Latest Release is $latest_release"

    if [[ -z $latest_release || $latest_release == null ]];then
        echo "Invalid Response when Querying for Last Release"
        exit
    fi
    
    url="https://api.github.com/repos/Azure/azure-iotedge/releases"
    header_content="Accept:application/vnd.github.v3+json"
    header_auth="Authorization:token $GITHUB_PAT"
    content=$(curl -X GET -H "$header_content" -H "$header_auth" "$url")

    # Check if Release Page has already been created
    release_created=$(echo $content | jq --arg version $VERSION '.[] | select(.name==$version)')
    
    if [[ -z $release_created ]];then
        
        echo "Fetch Changelog"
        url="https://api.github.com/repos/Azure/iotedge/contents?path=iotedge/&ref=$branch_name"
        content=$(curl -X GET  -H "$header_content" -H "$header_auth" "$url")
        download_uri=$(echo $content | jq '.[] | select(.name=="CHANGELOG.md")' | jq '.download_url')
        download_uri=$(echo $download_uri | tr -d '"')
        echo "download_url is $download_uri"
                
        echo "$(curl -X GET  -H "$header_content" -H "$header_auth" "$download_uri")" > $WDIR/content.txt
        
        #Find Content of New Release between (# NEW_VERSION) and (# PREVIOUS_VERSION)
        
        echo "$(sed -n "/# $VERSION/,/# $latest_release/p" $WDIR/content.txt)" > $WDIR/content.txt
        
        #Remove Last Line
        sed -i "$ d" $WDIR/content.txt

        #Create Release Page
        url="https://api.github.com/repos/Azure/azure-iotedge/releases"
        reqBody='{tag_name: $version, name: $version, target_commitish:"main", draft: true, body: $body}'
        if [[ $SKIP_UPLOAD == "false" ]]; then
            reqBody='{tag_name: $version, name: $version, target_commitish:"main", body: $body}'
        fi
        body=$(jq -n --arg version "$VERSION" --arg body "$(cat $WDIR/content.txt)" "$reqBody")
        sudo rm -rf $WDIR/content.txt
        
        echo "Body for Release is $body"
        content=$(curl -X POST -H "$header_content" -H "$header_auth" "$url" -d "$body")
        release_id=$(echo $content | jq '.id')
    else
        release_id=$(echo $release_created | jq '.id')
    fi

    echo "Release ID is $release_id"

    if [[ $SKIP_UPLOAD == "false" ]]; then
        #Upload Artifact
        for f in $(sudo ls $DIR);
        do  
            echo "File Name is $f, File Extension is ${f##*.}"
            echo $upload_url
            name=$f
            case ${f##*.} in 
                'deb')
                    mimetype="application/vnd.debian.binary-package"
                    # Modify Name to be of form {name}_{os}_{arch}.{extension}
                    name="${f%_*}_$PACKAGE_OS"
                    name+="_${f##*_}"
                    ;;
                'rpm')
                    mimetype="application/x-rpm"
                    ;;
                *)
                    mimetype="application/octet-stream"
                    ;;
            esac

            upload_url="https://uploads.github.com/repos/Azure/azure-iotedge/releases/$release_id/assets?name=$name"
            echo "Upload URL is $upload_url"
            echo "Mime Type is $mimetype"

            response=$(curl -X POST -H "Content-Type:$mimetype" -H "$header_content" -H "$header_auth" "$upload_url" --data-binary @$DIR/$f)
            
            state=$(echo "$response" | jq '.state')
            if [[  $state != "\"uploaded\"" ]]; then
                echo "failed to Upload Package. Response is"
                echo $response
                exit 1
            fi
        done;
    fi

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
ls -al $DIR 


if [[ $SERVER == *"github"* ]]; then
    if [[ -z $GITHUB_PAT ]]; then
        echo "Github PAT Token Not Provider"
        exit 1
    fi
    if [[ -z $VERSION ]]; then
        echo "Version Not Provided"
        exit 1
    fi
    publish_to_github
else
    publish_to_microsoft_repo
fi

