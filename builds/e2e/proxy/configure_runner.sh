#!/bin/bash

set -euo pipefail

proxy="http://${1}:3128"

export http_proxy=$proxy
export https_proxy=$proxy

echo 'Installing .NET 6.0, PowerShell Core, and Moby engine'

apt-get update
apt-get install -y curl git wget apt-transport-https software-properties-common
wget -q 'https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb'
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt-get update
apt-get install -y powershell dotnet-sdk-6.0 moby-engine

> ~/proxy-env.override.conf cat <<-EOF
[Service]
Environment="http_proxy=$proxy"
Environment="https_proxy=$proxy"
EOF
mkdir -p /etc/systemd/system/docker.service.d/
cp ~/proxy-env.override.conf /etc/systemd/system/docker.service.d/

systemctl daemon-reload
systemctl restart docker

# add aziot-identityd's proxy settings (even though aziot-identityd isn't installed--the tests do that later)
mkdir -p /etc/systemd/system/aziot-identityd.service.d
cp ~/proxy-env.override.conf /etc/systemd/system/aziot-identityd.service.d/

echo 'Verifying VM behavior behind proxy server'

# Verify runner can't skirt the proxy (should time out after 5s)
unset http_proxy https_proxy
timeout 5s curl -L 'http://www.microsoft.com' && exit 1 || :
timeout 5s curl -L 'https://www.microsoft.com' && exit 1 || :
