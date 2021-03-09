#!/bin/sh

# get .net core 3.1
# sudo wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
# sudo dpkg -i packages-microsoft-prod.deb

# sudo add-apt-repository universe
# sudo apt-get update
# sudo apt-get install apt-transport-https -y
# sudo apt-get update
# sudo apt-get install dotnet-sdk-3.1 -y

apt-get install jq -y

cd /source/modules/AzureMonitorForIotEdgeModule

# dotnet clean
# dotnet restore
