#!/bin/bash

set -euo pipefail

[ -z "$1" ] && echo "Please specify the ipv6 subnet for the docker0 bridge network." && exit 1
[ -z "$2" ] && echo "Please specify the ipv6 subnet for the azure-iot-edge bridge network." && exit 1
[ -z "$3" ] && echo "Please specify the network interface" && exit 1

DOCKER0_BRIDGE_SUBNET=$1
IOT_EDGE_SUBNET=$2
NETWORK_INTERFACE=$3

echo "Installing jq and grep..."
apt-get update -y
apt-get install -y jq grep
echo "jq and grep installed."

DOCKER_CONFIG_PATH=$4
SYSCTL_CONF_PATH=$5

echo "Configuring the docker daemon.json configuration file with IPv6 subnet $DOCKER0_BRIDGE_SUBNET..."

if [[ ! -e "$DOCKER_CONFIG_PATH" ]]; then
    DEFAULT_DOCKER_CONFIG_PATH="/etc/docker/daemon.json"
    touch $DEFAULT_DOCKER_CONFIG_PATH
    DOCKER_CONFIG_PATH=$DEFAULT_DOCKER_CONFIG_PATH
fi

cat $DOCKER_CONFIG_PATH | jq -n --arg var1 "$DOCKER0_BRIDGE_SUBNET" '. + { "ipv6": true, "fixed-cidr-v6": "\($var1)" }' > $DOCKER_CONFIG_PATH
echo "Configured docker for IPv6."

echo "Configuring the sysctl config to allow router advertisements and enable ndp proxying on the network interface $NETWORK_INTERFACE..."
if [[ ! -e "$SYSCTL_CONF_PATH" ]]; then
    DEFAULT_SYSCTL_CONF_PATH="/etc/sysctl.conf"
    touch $DEFAULT_SYSCTL_CONF_PATH
    SYSCTL_CONF_PATH=$DEFAULT_SYSCTL_CONF_PATH
fi

SYSCTL_DIR_PATH=$(dirname $SYSCTL_CONF_PATH)
echo "Backup $SYSCTL_CONF_PATH to $SYSCTL_DIR_PATH/sysctl-backup.conf..."
cp $SYSCTL_CONF_PATH $SYSCTL_DIR_PATH/sysctl-backup.conf
echo "Backed up $SYSCTL_CONF_PATH."

echo "Updating $SYSCTL_CONF_PATH..."

SYSCTL_RA="net.ipv6.conf.$NETWORK_INTERFACE.accept_ra=2"
SYSCTL_PNDP="net.ipv6.conf.$NETWORK_INTERFACE.proxy_ndp=1"

if [[ -z $(grep "$SYSCTL_RA" "$SYSCTL_CONF_PATH") ]]; then SYSCTL_UPDATE=$SYSCTL_RA; fi
if [[ -z $(grep "$SYSCTL_PNDP" "$SYSCTL_CONF_PATH") ]]; then 
    SYSCTL_UPDATE="$SYSCTL_UPDATE
$SYSCTL_PNDP";
fi

cat <<EOT >> $SYSCTL_CONF_PATH
$SYSCTL_UPDATE
EOT
echo "Configured sysctl."

echo "Refresh sysctl configuration..."
sysctl -p
echo "Configuration refreshed."

echo "Restarting docker..."
systemctl restart docker
echo "docker restarted."

echo "Running ndppd configuration script."
chmod +x ./installNdppd.sh
sh ./installNdppd.sh $DOCKER0_BRIDGE_SUBNET $IOT_EDGE_SUBNET $NETWORK_INTERFACE
echo "ndppd configuration script executed."
