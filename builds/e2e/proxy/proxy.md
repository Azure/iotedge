This file documents how to set up a proxy environment in Azure for our E2E tests.

The environment includes two VMs, both running Ubuntu 18.04:

- The "proxy server" VM has full network connectivity and runs an HTTP proxy server (squid).

- The "proxy client" VM has no internet-bound network connectivity except through the proxy server, and runs the latest version of moby engine.

Enter the commands below in your shell to deploy and configure the VMs. The commands use the Azure CLI (`az`). As part of the deployment, the proxy client will run a few commands to verify that it has no direct HTTP connectivity to the internet.

```sh
cd builds/e2e/proxy/

# ----------
# Parameters

# Name of Azure subscription
subscription_name='<>'

# Location of the resource group
location='<>'

# Name of the resource group
resource_group_name='<>'

# Names of the proxy server and client (test runner) VMs. Used to resolve them via DNS for the tests.
proxy_vm_name='e2eproxy-server'
runner_vm_name='e2eproxy-runner'

# Name of the user for the VMs
username='azureuser'

# The address prefix (in CIDR notation) of the virtual network/subnet
vnet_address_prefix='<>'

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
        --arg proxy_vm_name "$proxy_vm_name" \
        --arg runner_vm_name "$runner_vm_name" \
        --arg username "$username" \
        --arg ssh_public_key "$(cat $keyfile.pub)" \
        --arg vnet_address_prefix "$vnet_address_prefix" \
        '{
            "proxy_vm_name": { "value": $proxy_vm_name },
            "runner_vm_name": { "value": $runner_vm_name },
            "username": { "value": $username },
            "ssh_public_key": { "value": $ssh_public_key },
            "vnet_address_prefix": { "value": $vnet_address_prefix }
        }'
)"
```

Once deployment has completed, you can SSH into the proxy client VM to install/configure the Azure Pipelines agent. For more information, see [Self-hosted Linux Agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/v2-linux?view=azure-devops) and [Run a self-hosted agent behind a web proxy](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/proxy?view=azure-devops&tabs=unix).
