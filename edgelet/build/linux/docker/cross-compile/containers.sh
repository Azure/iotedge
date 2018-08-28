#!/bin/bash 
CONTAINER_REGISTRY=edgebuilds.azurecr.io

build_container () {
    local dockerfile=$1
    local tag=$2

    docker build -f $dockerfile --tag $tag .
}
