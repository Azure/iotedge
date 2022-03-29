#!/bin/bash
# This uses to setup the VM with prerequisite softwares to run E2E tests.
mkdir ~/setup
cd ~/setup

# Install >NET 6 runtime ( https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu )
wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

sudo apt-get update; \
  sudo apt-get install -y moby-engine moby-cli

sudo apt-get update; \
  sudo apt-get install -y apt-transport-https && \
  sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-6.0

# Install Powershell ( https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-linux?view=powershell-7 )
sudo apt-get update; \
  sudo add-apt-repository universe && \
  sudo apt-get install -y powershell

# Install Azure CLI ( https://docs.microsoft.com/en-us/cli/azure/install-azure-cli-apt?view=azure-cli-latest )
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az config set extension.use_dynamic_install=yes_without_prompt
az extension add --name azure-iot