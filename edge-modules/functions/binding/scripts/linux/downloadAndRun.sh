#!/bin/sh

apt-get update
apt-get upgrade
apt-get install unzip -y

curl --remote-name $FUNCTION_ROOT_FILEADDRESS

unzip $FUNCTION_FILE_NAME -d /app/

rm $FUNCTION_FILE_NAME.zip

exec dotnet WebJobs.Script.Host/WebJobs.Script.Host.dll ./$FUNCTION_FILE_NAME
