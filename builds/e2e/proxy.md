This file documents how to set up a proxy environment in Azure for our E2E tests.

The overall setup includes three VMs:

- The "agent VM" has full network connectivity and runs the SSH tasks defined in the E2E proxy VSTS job. It also runs an HTTP proxy (squid).

- A Linux "runner VM" runs the proxy tests themselves, on Linux. It has no network connectivity except to talk to the agent VM, thus all its interactions with Azure IoT Hub need to occur through the squid proxy on the agent VM.

- A Windows "runner VM" serves the same purpose as the Linux runner, but on Windows.

Follow the steps below to deploy the three VMs and set them up. The steps are in bash, but there are notes at the bottom about doing the same thing in PowerShell. In both cases, the Azure CLI `az` is required. If the deployment completes successfully, that means the environment is set up, the agent can reach the runners via SSH, and the runners can't reach the internet without the proxy.

```sh
cd ./builds/e2e/

# ----------
# Parameters


# Name of Azure subscription
subscription_name='<>'

# Location of the resource group
location='<>'

# Name of the resource group
resource_group_name='<>'

# Name of the key vault to store secrets for this deployment
key_vault_name='<>'

# AAD Object ID for a user or group who will be given access to the secrets in this key vault
key_vault_access_objectid='<>'

# Name of the Azure virtual network to which all VMs will attach.
vms_vnet_name='<>'

# The address prefix (in CIDR notation) of the virtual network/subnet
vms_vnet_address_prefix='<>'

# Name of the user for the VMs
vms_username='vsts'

# Name of the subnet within the virtual network
vms_vnet_subnet_name='default'

# Names of the agent and runner VMs. Used to resolve them via DNS for the tests.
vsts_agent_vm_name='e2eproxyvstsagent'
vsts_runner1_vm_name='e2eproxyvstsrunner1'
# This will be a windows machine name so, e.g., must be <= 15 chars
vsts_runner2_vm_name='e2eproxyrunner2'

# Name of the Windows VM admin password secret in key vault
key_vault_secret_name='windows-vm-admin-password'

# Windows VM admin password
windows_vm_password="$(openssl rand -base64 32)"

# -------
# Execute


# Create SSH key for the VMs
keyfile="$(realpath ./id_rsa)"
ssh-keygen -t rsa -b 4096 -N '' -f "$keyfile"


# Create an SSH service connection in VSTS using $vsts_agent_vm_name and $keyfile


# Log in to Azure subscription
az login
az account set -s "$subscription_name"


# If the resource group doesn't already exist, create it
az group create -l "$location" -n "$resource_group_name"


# Deploy the VMs
az group deployment create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters "$(
    jq -n \
        --arg key_vault_access_objectid "$key_vault_access_objectid" \
        --arg key_vault_name "$key_vault_name" \
        --arg key_vault_secret_name "$key_vault_secret_name" \
        --arg vms_ssh_key_encoded "$(base64 -w 0 $keyfile)" \
        --arg vms_ssh_public_key "$(cat $keyfile.pub)" \
        --arg vms_username "$vms_username" \
        --arg vms_vnet_address_prefix "$vms_vnet_address_prefix" \
        --arg vms_vnet_name "$vms_vnet_name" \
        --arg vms_vnet_subnet_name "$vms_vnet_subnet_name" \
        --arg vsts_agent_vm_name "$vsts_agent_vm_name" \
        --arg vsts_runner1_vm_name "$vsts_runner1_vm_name" \
        --arg vsts_runner2_vm_name "$vsts_runner2_vm_name" \
        --arg windows_vm_password "$windows_vm_password" \
        '{
            "key_vault_access_objectid": { "value": $key_vault_access_objectid },
            "key_vault_name": { "value" : $key_vault_name },
            "key_vault_secret_name": { "value": $key_vault_secret_name },
            "vms_ssh_key_encoded": { "value": $vms_ssh_key_encoded },
            "vms_ssh_public_key": { "value": $vms_ssh_public_key },
            "vms_username": { "value": $vms_username },
            "vms_vnet_address_prefix": { "value": $vms_vnet_address_prefix },
            "vms_vnet_name": { "value": $vms_vnet_name },
            "vms_vnet_subnet_name": { "value": $vms_vnet_subnet_name },
            "vsts_agent_vm_name": { "value": $vsts_agent_vm_name },
            "vsts_runner1_vm_name": { "value": $vsts_runner1_vm_name },
            "vsts_runner2_vm_name": { "value": $vsts_runner2_vm_name },
            "windows_vm_password": { "value": $windows_vm_password }
        }'
)"


## PowerShell notes:

Variable assignments are the same, except that the variable names should be prefixed with '$', e.g.:

```PowerShell
# Name of Azure subscription
$subscription_name='<>'
# ...
```

To create the Windows VM administrator password without openssl, use the following call:

```PowerShell
Add-Type -AssemblyName System.Web
$windows_vm_password=$([Convert]::ToBase64String([System.Web.Security.Membership]::GeneratePassword(32, 3).ToCharArray(), 0))
```

The commands to create an SSH key for the VMs are a little different. On Windows 1809 or later, install the ssh-agent feature first; more information [here](https://docs.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse).

```PowerShell
$keyfile=$(Join-Path (pwd).Path id_rsa)
ssh-keygen -t rsa -b 4096 -f "$keyfile" --% -N ""
```

The command to deploy the VMs is different. It doesn't use jq, and the base64-encoding command is PowerShell-specific:

```PowerShell
az group deployment create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters `
    key_vault_access_objectid="$key_vault_access_objectid" `
    key_vault_name="$key_vault_name" `
    key_vault_secret_name="$key_vault_secret_name" `
    vms_ssh_key_encoded="$([System.Convert]::ToBase64String([System.Text.Encoding]::Utf8.GetBytes($(Get-Content "$keyfile" -Raw))))" `
    vms_ssh_public_key="$(cat "$keyfile.pub")" `
    vms_username="$vms_username" `
    vms_vnet_address_prefix="$vms_vnet_address_prefix" `
    vms_vnet_name="$vms_vnet_name" `
    vms_vnet_subnet_name="$vms_vnet_subnet_name" `
    vsts_agent_vm_name="$vsts_agent_vm_name" `
    vsts_runner1_vm_name="$vsts_runner1_vm_name" `
    vsts_runner2_vm_name="$vsts_runner2_vm_name" `
    windows_vm_password=$windows_vm_password
```
