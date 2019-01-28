#!/bin/bash

set -euo pipefail

proxy_hostname="$1"
user="$2"

# Output the host key so it can be added to the agent's known_hosts file
echo '#DATA#'
cat /etc/ssh/ssh_host_rsa_key.pub
echo '#DATA#'

curl -x "http://$proxy_hostname:3128" 'https://packages.microsoft.com/config/ubuntu/18.04/prod.list' > ./microsoft-prod.list
mv ./microsoft-prod.list /etc/apt/sources.list.d/

curl -x "http://$proxy_hostname:3128" 'https://packages.microsoft.com/keys/microsoft.asc' | gpg --dearmor > microsoft.gpg
mv ./microsoft.gpg /etc/apt/trusted.gpg.d/

http_proxy="http://$proxy_hostname:3128" https_proxy="http://$proxy_hostname:3128" apt-get update > /dev/null
http_proxy="http://$proxy_hostname:3128" https_proxy="http://$proxy_hostname:3128" apt-get install -y moby-cli moby-engine > /dev/null

> ~/proxy-env.override.conf cat <<-EOF
[Service]
Environment="http_proxy=http://$proxy_hostname:3128"
Environment="https_proxy=http://$proxy_hostname:3128"
EOF
mkdir -p /etc/systemd/system/docker.service.d/
cp ~/proxy-env.override.conf /etc/systemd/system/docker.service.d/

# Make proxy-env.override.conf available in $user's home directory so tests can
# apply the same proxy settings to the iotedge service
home="$(eval echo ~$user)"
cp ~/proxy-env.override.conf "$home/"
chown -R "$user:$user" "$home/proxy-env.override.conf"

systemctl daemon-reload
systemctl restart docker
