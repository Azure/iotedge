#!/bin/bash

set -euo pipefail

agent_url='https://vstsagentpackage.azureedge.net/agent/2.175.2/vsts-agent-linux-x64-2.175.2.tar.gz'
agent_file="${agent_url##*/}"

proxy="http://$1:3128"

# install PowerShell Core and .NET Core 3.1
http_proxy=$proxy https_proxy=$proxy apt-get update
http_proxy=$proxy https_proxy=$proxy apt-get install -y git wget apt-transport-https
http_proxy=$proxy https_proxy=$proxy wget -q 'https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb'
dpkg -i packages-microsoft-prod.deb
http_proxy=$proxy https_proxy=$proxy apt-get update
http_proxy=$proxy https_proxy=$proxy add-apt-repository universe
http_proxy=$proxy https_proxy=$proxy apt-get install -y powershell dotnet-sdk-3.1

# install Azure Pipelines agent
curl -x $proxy -L -o ~/vsts-agent-linux-x64-2.175.2.tar.gz $agent_url
mkdir myagent && cd myagent
tar zxvf ~/$agent_file

# TODO: script the agent config process?
# proxy_fqdn="http://$1.$(grep -Po '^search \K.*' /etc/resolv.conf):3128"
# ./config.sh --proxyurl $proxy_fqdn
# ./svc.sh install
# ./svc.sh start

# install moby
curl -x $proxy 'https://packages.microsoft.com/config/ubuntu/18.04/multiarch/prod.list' > microsoft-prod.list
mv microsoft-prod.list /etc/apt/sources.list.d/

curl -x $proxy 'https://packages.microsoft.com/keys/microsoft.asc' | gpg --dearmor > microsoft.gpg
mv microsoft.gpg /etc/apt/trusted.gpg.d/

http_proxy=$proxy https_proxy=$proxy apt-get update
http_proxy=$proxy https_proxy=$proxy apt-get install -y moby-engine

> ~/proxy-env.override.conf cat <<-EOF
[Service]
Environment="http_proxy=$proxy"
Environment="https_proxy=$proxy"
EOF
mkdir -p /etc/systemd/system/docker.service.d/
cp ~/proxy-env.override.conf /etc/systemd/system/docker.service.d/

systemctl daemon-reload
systemctl restart docker

# add iotedged's proxy settings (even though iotedged isn't installed--the tests do that later)
mkdir -p /etc/systemd/system/iotedge.service.d
cp ~/proxy-env.override.conf /etc/systemd/system/iotedge.service.d/

# Verify runner can use the proxy
curl -x $proxy -L 'http://www.microsoft.com'
curl -x $proxy -L 'https://www.microsoft.com'

# Verify runner can't skirt the proxy (should time out after 5s)
curl --connect-timeout 5 -L 'http://www.microsoft.com' && exit 1 || :
curl --connect-timeout 5 -L 'https://www.microsoft.com' && exit 1 || :
