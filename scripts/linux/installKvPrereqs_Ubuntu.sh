#!/bin/bash

# Check if Python is installed
if ! command -v python > /dev/null 2>&1; then
        echo "Python not found! Please make sure it is installed before running the script"
        exit /b 1
fi

# Update Apt-get

sudo apt-get update

# Install AZ Cli - https://docs.microsoft.com/en-us/cli/azure/install-azure-cli

# Install AZ Cli Pre-Reqs
echo Installing Azure Cli Pre-Reqs

if [[ `lsb_release -rs` == "12.04" || `lsb_release -rs` == "14.04" ]]
then
        sudo apt-get install -y libssl-dev libffi-dev python-dev
else
        sudo apt-get install -y libssl-dev libffi-dev python-dev build-essential
fi

echo Done installing Azure Cli Pre-Reqs

# Install AZ Cli itself
echo Installing Azure Cli

echo "deb [arch=amd64] https://packages.microsoft.com/repos/azure-cli/ wheezy main" | tee /etc/apt/sources.list.d/azure-cli.list
sudo apt-key adv --keyserver packages.microsoft.com --recv-keys 417A0893
sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install azure-cli

echo Done installing Azure Cli

# Install Powershell - https://github.com/PowerShell/PowerShell/blob/master/docs/installation/linux.md
echo Installing Powershell
yes | bash <(curl -fsSL https://raw.githubusercontent.com/PowerShell/PowerShell/v6.0.0-alpha.18/tools/download.sh)
echo Done installing Powershell

# install jq - https://stedolan.github.io/jq/
echo Installing jq
sudo apt-get install -y jq
echo Done installing jq