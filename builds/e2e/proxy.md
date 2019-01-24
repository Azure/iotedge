This file documents how to set up a proxy environment in Azure for our E2E tests.

The overall setup includes three VMs:

- The "agent VM" has full network connectivity and runs the SSH tasks defined in the E2E proxy VSTS job. It also runs an HTTP proxy (squid).

- A Linux "runner VM" runs the proxy tests themselves, on Linux. It has no network connectivity except to talk to the agent VM, thus all its interactions with Azure IoT Hub need to occur through the squid proxy on the agent VM.

- A Windows "runner VM" serves the same purpose as the Linux runner, but on Windows.

Follow the steps below to deploy the three VMs and set them up. The steps are in bash and require the Azure CLI (`az`), but there are notes at the bottom about doing the same thing with the Azure PowerShell module (`Az`).


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

# Name of the user for the VMs
vms_username='vsts'

# Name of the subnet within the virtual network
vms_vnet_subnet_name='default'

# The address prefix (in CIDR notation) of the virtual network/subnet
vms_vnet_address_prefix='10.0.0.0/24'

# Name of the dynamically-provisioned public IP for the agent VM
vsts_agent_vm_public_ip_name='e2eproxy'

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
        --arg key_vault_name "$key_vault_name" \
        --arg key_vault_access_objectid "$key_vault_access_objectid" \
        --arg key_vault_secret_name "$key_vault_secret_name" \
        --arg vms_ssh_key_encoded "$(base64 -w 0 $keyfile)" \
        --arg vms_ssh_public_key "$(cat $keyfile.pub)" \
        --arg windows_vm_password "$windows_vm_password" \
        --arg vms_username "$vms_username" \
        --arg vms_vnet_name "$vms_vnet_name" \
        --arg vms_vnet_subnet_name "$vms_vnet_subnet_name" \
        --arg vms_vnet_address_prefix "$vms_vnet_address_prefix" \
        --arg vsts_agent_vm_public_ip_name "$vsts_agent_vm_public_ip_name" \
        --arg vsts_agent_vm_name "$vsts_agent_vm_name" \
        --arg vsts_runner1_vm_name "$vsts_runner1_vm_name" \
        --arg vsts_runner2_vm_name "$vsts_runner2_vm_name" \
        '{
            "key_vault_name": { "value" : $key_vault_name },
            "key_vault_access_objectid": { "value": $key_vault_access_objectid },
            "key_vault_secret_name": { "value": $key_vault_secret_name },
            "vms_ssh_key_encoded": { "value": $vms_ssh_key_encoded },
            "vms_ssh_public_key": { "value": $vms_ssh_public_key },
            "windows_vm_password": { "value": $windows_vm_password },
            "vms_username": { "value": $vms_username },
            "vms_vnet_name": { "value": $vms_vnet_name },
            "vms_vnet_subnet_name": { "value": $vms_vnet_subnet_name },
            "vms_vnet_address_prefix": { "value": $vms_vnet_address_prefix },
            "vsts_agent_vm_public_ip_name": { "value": $vsts_agent_vm_public_ip_name },
            "vsts_agent_vm_name": { "value": $vsts_agent_vm_name },
            "vsts_runner1_vm_name": { "value": $vsts_runner1_vm_name },
            "vsts_runner2_vm_name": { "value": $vsts_runner2_vm_name }
        }'
)"

# Get the public IP of the agent VM
vsts_agent_public_ip="$(az network public-ip show --resource-group "$resource_group_name" --name "$vsts_agent_vm_public_ip_name" --query 'ipAddress' --output tsv)"


# Verify Linux proxy works (should succeed)
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner1_vm_name" curl -x "http://${vsts_agent_vm_name}:3128" -L 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner1_vm_name" curl -x "http://${vsts_agent_vm_name}:3128" -L 'https://www.microsoft.com'


# Verify Linux proxy is required (should time out after 5s)
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner1_vm_name" timeout 5 curl -L 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner1_vm_name" timeout 5 curl -L 'https://www.microsoft.com'


# Verify Windows proxy works (should succeed)
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -ti "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner2_vm_name" iwr -useb -proxy "http://${vsts_agent_vm_name}:3128" 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -ti "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner2_vm_name" iwr -useb -proxy "http://${vsts_agent_vm_name}:3128" 'https://www.microsoft.com'


# Verify Windows proxy is required (should time out after 5s)
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner2_vm_name" iwr -useb -noproxy -timeoutsec 5 'http://www.microsoft.com'
ssh -ti "$keyfile" "$vms_username@$vsts_agent_public_ip" ssh -i "/home/$vms_username/.ssh/id_rsa" "$vms_username@$vsts_runner2_vm_name" iwr -useb -noproxy -timeoutsec 5 'https://www.microsoft.com'
```

## PowerShell notes:

With earlier iterations of this deployment, the steps in PowerShell vs. Bash were nearly identical using the `az` CLI. However, with the introduction of a Key Vault secret for the Windows VM Administrator password, we hit a snag: you can't (currently) reference a Key Vault secret in the `--parameters` argument to `az group deployment create`. You have to use a parameters file, or the PowerShell `Az` module (`Install-Module -Name Az -AllowClobber`). So, using the latter option, here are the steps:

```PowerShell
cd .\builds\e2e\

$subscription_name='<>'
$location='<>'
$resource_group_name='<>'
$key_vault_name='<>'
$key_vault_access_objectid='<>'
$vms_vnet_name='<>'

$vms_username='vsts'
$vms_vnet_subnet_name='default'
$vms_vnet_address_prefix='10.0.0.0/24'
$vsts_agent_vm_public_ip_name='e2eproxy'
$vsts_agent_vm_name='e2eproxyvstsagent'
$vsts_runner1_vm_name='e2eproxyvstsrunner1'
$vsts_runner2_vm_name='e2eproxyrunner2'
$key_vault_secret_name='windows-vm-admin-password'

# Note: If openssl isn't installed, You can replace "$(openssl rand -base64 32)" with:
#   "$([Convert]::ToBase64String([System.Web.Security.Membership]::GeneratePassword(32, 3).ToCharArray(), 0))"
$windows_vm_password=$(ConvertTo-SecureString "$(openssl rand -base64 32)" -AsPlainText -Force)

# On Windows 1809 or later, install the ssh-agent feature first; see https://docs.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse
$keyfile=$(Join-Path (pwd).Path id_rsa)
ssh-keygen -t rsa -b 4096 -f "$keyfile" --% -N ""

Connect-AzAccount
Set-AzContext -Subscription "$subscription_name"

New-AzResourceGroup -Name "$resource_group_name" -Location "$location"

New-AzResourceGroupDeployment `
  -Name 'e2eproxy' `
  -ResourceGroupName "$resource_group_name" `
  -TemplateFile '.\proxy-deployment-template.json' `
  -key_vault_name "$key_vault_name" `
  -key_vault_access_objectid "$key_vault_access_objectid" `
  -key_vault_secret_name "$key_vault_secret_name" `
  -vms_ssh_key_encoded "$([System.Convert]::ToBase64String([System.Text.Encoding]::Utf8.GetBytes($(Get-Content "$keyfile" -Raw))))" `
  -vms_ssh_public_key "$(cat "$keyfile.pub")" `
  -windows_vm_password $windows_vm_password `
  -vms_username "$vms_username" `
  -vms_vnet_name "$vms_vnet_name" `
  -vms_vnet_subnet_name "$vms_vnet_subnet_name" `
  -vms_vnet_address_prefix "$vms_vnet_address_prefix" `
  -vsts_agent_vm_public_ip_name "$vsts_agent_vm_public_ip_name" `
  -vsts_agent_vm_name "$vsts_agent_vm_name" `
  -vsts_runner1_vm_name "$vsts_runner1_vm_name" `
  -vsts_runner2_vm_name "$vsts_runner2_vm_name"

$vsts_agent_public_ip=(Get-AzPublicIpAddress -Name "$vsts_agent_vm_public_ip_name" -ResourceGroupName "$resource_group_name").IpAddress

# ssh commands to verify that the proxy works on both runner VMs...
```
