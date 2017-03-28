#!/bin/bash

DOTNET_SDK_PACKAGE_NAME='dotnet-dev-ubuntu.16.04-x64.latest.tar.gz'
DOTNET_SDK_PACKAGE='https://dotnetcli.blob.core.windows.net/dotnet/Sdk/master/dotnet-dev-ubuntu.16.04-x64.latest.tar.gz'
DOTNET_ROOT_PATH=~/dotnet

while getopts ":u:" o; do
    case "${o}" in
        u)
            DOTNET_SDK_PACKAGE=${OPTARG}
            ;; 
    esac
done
shift $((OPTIND-1))

rm -rf $DOTNET_ROOT_PATH
mkdir $DOTNET_ROOT_PATH

echo Downloading package $DOTNET_SDK_PACKAGE
if wget -q $DOTNET_SDK_PACKAGE -O /tmp/$DOTNET_SDK_PACKAGE_NAME ; then
	echo Downloaded .Net Core package
else 
	echo Error downloading .Net Core package
	exit 1
fi

tar -xzf /tmp/$DOTNET_SDK_PACKAGE_NAME -C $DOTNET_ROOT_PATH
chmod +x $DOTNET_ROOT_PATH/dotnet

exit 0
