#!/bin/sh

set -euo pipefail

[ -z "$1" ] && echo "Please specify the docker0 bridge network subnet IPv6 address to proxy. This is the subnet value specified in docker's daemon.json file." && exit 1
[ -z "$2" ] && echo "Please specify the azure-iot-edge network subnet IPv6 address to proxy" && exit 1
[ -z "$3" ] && echo "Please specify the network interface" && exit 1

DOCKER0_BRIDGE_SUBNET=$1
IOT_EDGE_SUBNET=$2
NETWORK_INTERFACE=$3

echo "Installing ndppd..."
apt-get update -y
apt-get install -y ndppd
echo "Ndppd installed."

echo "Configured the ndppd configuration file with network interface $NETWORK_INTERFACE and IPv6 subnets $DOCKER0_BRIDGE_SUBNET and $IOT_EDGE_SUBNET..."
cat <<EOF > /etc/ndppd.conf
route-ttl 5000
proxy $NETWORK_INTERFACE {
  router yes
  timeout 500
  ttl 30000
  rule $DOCKER0_BRIDGE_SUBNET {
    auto
  }
  rule $IOT_EDGE_SUBNET {
    auto
  }
}
EOF
echo "Configured ndppd."

echo "Restarting ndppd..."
systemctl restart ndppd
echo "ndppd restarted."
