#!/bin/bash

set -euo pipefail

agent_url='https://vstsagentpackage.azureedge.net/agent/2.175.2/vsts-agent-linux-x64-2.175.2.tar.gz'
agent_file="${agent_url##*/}"

proxy="http://${1}:3128"

export http_proxy=$proxy
export https_proxy=$proxy

echo 'Installing PowerShell Core and .NET Core 3.1'

apt-get update
apt-get install -y curl git wget apt-transport-https
wget -q 'https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb'
dpkg -i packages-microsoft-prod.deb
apt-get update
add-apt-repository universe
apt-get install -y powershell dotnet-sdk-3.1

echo 'Installing Azure Pipelines agent'

wget -q $agent_url
mkdir myagent
cd myagent
tar zxvf ../$agent_file

# TODO: script the agent config process?
# proxy_fqdn="http://$1.$(grep -Po '^search \K.*' /etc/resolv.conf):3128"
# ./config.sh --proxyurl $proxy_fqdn
# ./svc.sh install
# ./svc.sh start
