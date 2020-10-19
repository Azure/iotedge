This file documents how to set up a proxy environment in Azure for our E2E tests.

The environment includes two VMs, both running Ubuntu 18.04:

- The "proxy server" VM has full network connectivity and runs an HTTP proxy server (squid).

- The "proxy client" VM has no internet-bound network connectivity except through the proxy server, and runs the latest version of moby engine.

Enter the commands below in your shell to deploy and configure the VMs. The commands use the Azure CLI (`az`). As part of the deployment, the proxy client will run a few commands to verify that it has no direct HTTP connectivity to the internet.

```sh
cd ./builds/e2e/proxy/

# ----------
# Parameters

# Name of Azure subscription
subscription_name='<>'

# Location of the resource group
location='<>'

# Name of the resource group
resource_group_name='<>'

# Name of the Azure virtual network to which the VMs will attach.
vnet_name='<>'

# The address prefix (in CIDR notation) of the virtual network/subnet
vnet_address_prefix='<>'

# Name of the subnet within the virtual network
subnet_name='default'

# Name of the user for the VMs
username='azureuser'

# Names of the proxy server and client (test runner) VMs. Used to resolve them via DNS for the tests.
proxy_vm_name='e2eproxy-server'
runner_vm_name='e2eproxy-runner'

# -------
# Execute

# Create SSH key for the VMs
keyfile="$(realpath ./id_rsa)"
ssh-keygen -t rsa -b 4096 -N '' -f "$keyfile"

# Log in to Azure subscription
az login
az account set -s "$subscription_name"

# If the resource group doesn't already exist, create it
az group create -l "$location" -n "$resource_group_name"

# Deploy the VMs
az deployment group create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters "$(
    jq -n \
        --arg ssh_public_key "$(cat $keyfile.pub)" \
        --arg username "$username" \
        --arg vnet_address_prefix "$vnet_address_prefix" \
        --arg vnet_name "$vnet_name" \
        --arg subnet_name "$subnet_name" \
        --arg proxy_vm_name "$proxy_vm_name" \
        --arg runner_vm_name "$runner_vm_name" \
        '{
            "ssh_public_key": { "value": $ssh_public_key },
            "username": { "value": $username },
            "vnet_address_prefix": { "value": $vnet_address_prefix },
            "vnet_name": { "value": $vnet_name },
            "subnet_name": { "value": $subnet_name },
            "proxy_vm_name": { "value": $proxy_vm_name },
            "runner_vm_name": { "value": $runner_vm_name }
        }'
)"
```

Once deployment has completed, you can SSH into the proxy client VM and configure the Azure Pipelines agent the following commands. For more information about installing/configuring the agent, see [Self-hosted Linux Agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/v2-linux?view=azure-devops) and [Run a self-hosted agent behind a web proxy](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/proxy?view=azure-devops&tabs=unix).

```sh
proxy_fqdn="http://$proxy_vm_name`.$(grep -Po '^search \K.*' /etc/resolv.conf):3128"
./config.sh --proxyurl $proxy_fqdn
./svc.sh install
./svc.sh start
```
