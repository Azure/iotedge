This file documents how to set up a proxy environment in Azure for our E2E tests.

The environment includes:
- A proxy server VM - full network connectivity, runs an HTTP proxy server (squid).
- One or more proxy client VMs (aka "runners") - no internet-bound network connectivity except through the proxy server.
- A Key Vault that contains the private keys used to SSH into the runner VMs.

After installing the Azure CLI, enter the following commands to deploy and configure the VMs:

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

# The number of runner VMs to create
runner_count=2

# AAD Object ID for a user or group who will be given access to the secrets in the key vault
key_vault_access_objectid='<>'

# -------
# Execute

# Log in to Azure subscription
az login
az account set -s "$subscription_name"

# If the resource group doesn't already exist, create it
az group create -l "$location" -n "$resource_group_name"

# Deploy the VMs
az deployment group create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters "$(
    jq -n \
        --argjson runner_count $runner_count \
        --arg key_vault_access_objectid "$key_vault_access_objectid" \
        '{
            "runner_count": { "value": $runner_count },
            "key_vault_access_objectid": { "value": $key_vault_access_objectid },
            "create_runner_public_ip": { "value": true }
        }'
)"
```

Once the deployment has completed, you can SSH into the runner VMs to install/configure the Azure Pipelines agent. For more information, see [Self-hosted Linux Agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/v2-linux?view=azure-devops) and [Run a self-hosted agent behind a web proxy](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/proxy?view=azure-devops&tabs=unix).
