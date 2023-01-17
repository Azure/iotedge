#!/bin/sh

set -e

echo "Making /var/run/iotedge if it does not exist"
mkdir -p /var/run/iotedge
echo "Successfully made /var/run/iotedge if it did not exist"
chown snap_aziotedge:snap_aziotedge /var/run/iotedge

exec "$@"
