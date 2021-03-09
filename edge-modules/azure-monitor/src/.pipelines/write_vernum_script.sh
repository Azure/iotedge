#!/bin/sh

cd /source/modules/AzureMonitorForIotEdgeModule
# dotnet publish -c Release -o out_amd64

VERNUM=$(jq .image.tag.version module.json | tr -d '\"')

echo \#\!/bin/sh > get_vernum.sh
echo echo ${VERNUM} >> get_vernum.sh
chmod +x get_vernum.sh

echo Wrote get_vernum.sh file
cat get_vernum.sh
