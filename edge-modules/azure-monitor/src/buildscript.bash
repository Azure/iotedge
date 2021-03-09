#!/bin/bash

# this script is for publishing the module to a container registry

# TODO: I believe this script is completely unused. Get rid of it?

rm -rf modulecode
git clone git@github.com:microsoft/AzureMonitor-IOTMetricsModule.git modulecode
cd modulecode
git checkout release

export VERSION=$(jq .image.tag.version modules/AzureMonitorForIotEdgeModule/module.json | tr -d '\"')


docker build  --rm -f "$(pwd)/modules/AzureMonitorForIotEdgeModule/Dockerfile.amd64.debug" -t azuremonitoriotprivatepreview.azurecr.io/azuremonitoriotmodule:${VERSION}-amd64 
"$(pwd)/modules/AzureMonitorForIotEdgeModule"
docker push azuremonitoriotprivatepreview.azurecr.io/azuremonitoriotmodule:${VERSION}-amd64
