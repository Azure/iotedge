#!/bin/bash

set -euo pipefail

agent_url='https://vstsagentpackage.azureedge.net/agent/2.175.2/vsts-agent-linux-x64-2.175.2.tar.gz'
agent_file="${agent_url##*/}"

proxy="http://${1}:3128"

export http_proxy=$proxy
export https_proxy=$proxy
