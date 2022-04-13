#!/bin/bash

set -euo pipefail

proxy="http://${1}:3128"

export http_proxy=$proxy
export https_proxy=$proxy

echo 'Installing PowerShell Core and .NET 6.0'

apt-get update
apt-get install -y curl git wget apt-transport-https
wget -q 'https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb'
dpkg -i packages-microsoft-prod.deb
apt-get update
add-apt-repository universe
apt-get install -y powershell dotnet-sdk-6.0

echo 'Installing Moby engine'

curl -x $proxy 'https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list' > microsoft-prod.list
mv microsoft-prod.list /etc/apt/sources.list.d/

curl -x $proxy 'https://packages.microsoft.com/keys/microsoft.asc' | gpg --dearmor > microsoft.gpg
mv microsoft.gpg /etc/apt/trusted.gpg.d/

apt-get update
apt-get install -y moby-engine

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
