#!/bin/bash

# Installs the pre-reqs (currently only .Net Core) on the Windwos machine.

if [ -z "${AGENT_WORKFOLDER}" ]; then
	echo Error: Environment variable AGENT_WORKFOLDER is not defined.
	exit 1
fi

DOTNET_SDK_PACKAGE_NAME='dotnet.tar.gz'
DOTNET_ROOT_PATH=$AGENT_WORKFOLDER/dotnet

if [ ! -d "${AGENT_WORKFOLDER}" ]; then
	echo Path $AGENT_WORKFOLDER does not exist” 1>&2
	exit 1
fi

usage() 
{ 
	echo "Missing arguments. Usage: $0 -u <dotnet_cli_url>" 1>&2
	exit 1
}

while getopts ":u:" o; do
    case "${o}" in
        u)
            DOTNET_SDK_PACKAGE=${OPTARG}
            ;; 
    esac
done
shift $((OPTIND-1))

if [ -z "${DOTNET_SDK_PACKAGE}" ]; then
	usage
fi

rm -rf $DOTNET_ROOT_PATH
mkdir $DOTNET_ROOT_PATH

echo Downloading package $DOTNET_SDK_PACKAGE
if wget -q $DOTNET_SDK_PACKAGE -O /tmp/$DOTNET_SDK_PACKAGE_NAME ; then
	echo Downloaded .Net Core package
else 
	echo Error downloading .Net Core package
	exit 1
fi

echo Unzip and binplace dotnet
tar -xzf /tmp/$DOTNET_SDK_PACKAGE_NAME -C $DOTNET_ROOT_PATH
chmod +x $DOTNET_ROOT_PATH/dotnet

exit 0
