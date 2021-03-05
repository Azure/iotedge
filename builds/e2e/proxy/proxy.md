This file documents how to set up a proxy environment in Azure for our E2E tests.

The environment includes:
- A proxy server VM - full network connectivity, runs an HTTP proxy server (squid).
- One or more proxy client VMs (aka "runners") - no internet-bound network connectivity except through the proxy server.
- A Key Vault that contains the private keys used to SSH into the Linux VMs, and passwords to RDP into the Windows VMs.

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

# Prefix used when creating Azure resources. If not given, defaults to 'e2e-<13 char hash>-'.
# **NOTE** Windows VM names have a 15-character limit and this script creates the VM name by
#   appending 'w<n>' to the resource_prefix, where <n> is a number between 1 and
#   windows_runner_count (see below). If you're adding a Windows runner, you must define a
#   resource_prefix that takes these constraints into account (e.g. no more than 13 characters
#   for 1-9 Windows runners).
resource_prefix='<>'

# The number of Linux and Windows runner VMs to create
linux_runner_count=1
windows_runner_count=1

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
        --arg resource_prefix $resource_prefix \
        --argjson linux_runner_count $linux_runner_count \
        --argjson windows_runner_count $windows_runner_count \
        --arg key_vault_access_objectid "$key_vault_access_objectid" \
        '{
            "resource_prefix": { "value": $resource_prefix },
            "linux_runner_count": { "value": $linux_runner_count },
            "windows_runner_count": { "value": $windows_runner_count },
            "key_vault_access_objectid": { "value": $key_vault_access_objectid },
            "create_runner_public_ip": { "value": true }
        }'
)"
```

## PowerShell deployment notes

Variable assignments are the same, except that the variable names should be prefixed with '$', e.g.:

```PowerShell
# Name of Azure subscription
$subscription_name='<>'
# ...
```

The command to deploy the VMs is little different because it doesn't use jq, and bash syntax is replaced with PowerShell:

```PowerShell
az group create -l "$location" -n "$resource_group_name"
az deployment group create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters `
    resource_prefix="$resource_prefix" `
    linux_runner_count="$linux_runner_count" `
    windows_runner_count="$windows_runner_count" `
    key_vault_access_objectid="$key_vault_access_objectid" `
    create_runner_public_ip='true'
```

## Post-deployment steps

Once the deployment has completed, SSH/RDP into each runner VM to install and configure the Azure Pipelines agent. To access the runner VMs, first download their private keys/passwords from Key Vault. Find the name of the key vault from your deployment, then list the secret URLs for the private keys/passwords:

```sh
az keyvault secret list --vault-name '<>' -o tsv --query "[].id|[?contains(@, 'runner')]"
```

With a secret URL and an IP address, you can SSH into a runner VM like this:

```sh
az keyvault secret show --id '<>' -o tsv --query value > ~/.ssh/id_rsa.runner
chmod 600 ~/.ssh/id_rsa.runner
ssh -i ~/.ssh/id_rsa.runner azureuser@<ip addr>
```
Or print the Windows password to your terminal like this:

```
az keyvault secret show --id '<>' -o tsv --query value
```

...and copy it into the login dialog of your Remote Desktop client.

To install and configure Azure Pipelines agent, see [Self-hosted Linux Agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/v2-linux?view=azure-devops) or [Self-hosted Windows Agents](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/v2-windows?view=azure-devops). Also, see [Run a self-hosted agent behind a web proxy](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/proxy?view=azure-devops).

> Note that the proxy URL required for most operations on the runner VMs is simply the hostname of the proxy server VM, e.g. `http://e2e-piaj2z37enpb4-proxy-vm:3128`. However, operations inside Docker containers on the runner VMs need either:
> - The _fully-qualified_ name of the proxy VM, e.g. `http://e2e-piaj2z37enpb4-proxy-vm.e0gkjhpfr5quzatbjwfoss05vh.xx.internal.cloudapp.net:3128`, or
> - The private IP address of the proxy VM, e.g. `http://10.0.0.4:3128`
>
> The end-to-end tests get the proxy URL from the agent (via the predefined variable `$(Agent.ProxyUrl)`). Therefore, when you configure the agent you must give it one of the two proxy URLs described above (using either the fully-qualified name or the IP address). For example, To pass the fully-qualifed name during agent installation on a runner VM:

> ```sh
> proxy_hostname='<>'
> proxy_fqdn="http://$proxy_hostname.$(grep -Po '^search \K.*' /etc/resolv.conf):3128"
> ./config.sh --proxyurl $proxy_fqdn
> ```