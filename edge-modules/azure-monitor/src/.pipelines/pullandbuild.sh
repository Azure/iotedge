#!/bin/bash

# this script is for publishing the module to a container registry

export VERSION=$(jq .image.tag.version modules/AzureMonitorForIotEdgeModule/module.json | tr -d '\"')


docker build  --rm -f "$(pwd)/modules/AzureMonitorForIotEdgeModule/Dockerfile.amd64.debug" -t azuremonitoriotprivatepreview.azurecr.io/azuremonitoriotmodule:${VERSION}-amd64 
"$(pwd)/modules/AzureMonitorForIotEdgeModule"
# docker push azuremonitoriotprivatepreview.azurecr.io/azuremonitoriotmodule:${VERSION}-amd64
