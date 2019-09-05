#!/bin/sh

[ -z "$1" ] && echo "Please specify the docker0 bridge network subnet IPv6 address to proxy. This is the subnet value specified in docker's daemon.json file." && exit 1
[ -z "$2" ] && echo "Please specify the azure-iot-edge network subnet IPv6 address to proxy" && exit 1
[ -z "$3" ] && echo "Please specify the network interface" && exit 1

echo "Installing ndppd..."
apt-get update -y
apt-get install -y ndppd
echo "Ndppd installed."

echo "Configured the ndppd configuration file with network interface $3 and IPv6 subnets $1 and $2..."
cat <<EOF > /etc/ndppd.conf
route-ttl 5000
proxy $3 {
  router yes
  timeout 500
  ttl 30000
  rule $1 {
    auto
  }
  rule $2 {
    auto
  }
}
EOF
echo "Configured ndppd."

echo "Restarting ndppd..."
systemctl restart ndppd
echo "ndppd restarted."