#!/bin/sh

docker_socket="/var/run/docker.sock"
timeout=30
interval=5

# Waiting before notifying makes sure that both
# docker-proxy & aziot-edged start only after docker is
# up and running. It's helpful on slower devices esp. after boot-up.
for i in $(seq 0 $interval $timeout); do
    if [ -e "$docker_socket" ]; then
        echo "Docker socket ($docker_socket) is available."
        break
    fi

    echo "Docker socket ($docker_socket) does not exist yet. Waiting... ($(($timeout - $i)) seconds left)"
    sleep $interval
done

# We report that docker-proxy is ready regardless of whether the
# socket exists or not otherwise the snap installation will fail.
# This is a better UX as the user can now fix the issue themselves.. say
# they installed iotedge before docker.
systemd-notify --ready

$SNAP/usr/bin/socat UNIX-LISTEN:$SNAP_COMMON/docker-proxy.sock,reuseaddr,fork,unlink-early,user=snap_aziotedge,group=snap_aziotedge UNIX-CONNECT:$docker_socket
