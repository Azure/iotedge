#!/bin/sh

$SNAP/usr/bin/socat UNIX-LISTEN:$SNAP_COMMON/docker-proxy.sock,reuseaddr,fork,user=snap_aziotedge,group=snap_aziotedge UNIX-CONNECT:/var/run/docker.sock
