This file documents how to set up a proxy environment in Azure for our E2E tests.

The overall setup includes three VMs:

- The "agent VM" has full network connectivity and runs the SSH tasks defined in the E2E proxy VSTS job. It also runs an HTTP proxy (squid).

- A Linux "runner VM" runs the proxy tests themselves, on Linux. It has no network connectivity except to talk to the agent VM, thus all its interactions with Azure IoT Hub need to occur through the squid proxy on the agent VM.

- A Windows "runner VM" serves the same purpose as the Linux runner, but on Windows.

Follow the steps below to deploy the three VMs and set them up. The steps are in bash, but there are notes at the bottom about doing the same thing in PowerShell. In both cases, the Azure CLI `az` is required.


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

# Name of the user for the VMs
vms_username='vsts'

# Name of the Azure virtual network and subnet that the agent VM is in.
# The new VMs have to be in the same VNet as the agent VM.
vms_vnet_name='<>'
vms_vnet_subnet_name='default'

# Names of the agent and runner VMs. Used to resolve them via DNS for the tests.
vsts_agent_vm_name='e2eproxyvstsagent'
vsts_runner1_vm_name='e2eproxyvstsrunner1'
# This will be a windows machine name so, e.g., must be <= 15 chars
vsts_runner2_vm_name='e2eproxyrunner2'

# Name of the dynamically-provisioned public IP for the agent VM
vsts_agent_vm_public_ip_name='e2eproxy'

# Name of the key vault to store secrets for this deployment
key_vault_name='<>'

# Name of the Windows VM admin password secret in key vault
secret_name='windows-vm-admin-password'


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


# If the key vault doesn't already exist, create it
# NOTE the key vault must be enabled for template deployment
az keyvault create -n "$key_vault_name" -g "$resource_group_name" -l "$location" --enabled-for-template-deployment true


# Save the key vault's ID in order to reference the Windows VM password in the deployment below
key_vault_id=$(az keyvault show -n "$key_vault_name" -g "$resource_group_name" --query 'id' --output tsv)


# If it doesn't already exist, insert Windows VM password into key vault
az keyvault secret set --vault-name "$key_vault_name" --name "$secret_name" --value "$(openssl rand -base64 32)"


# If the virtual network + subnet doesn't already exist within the resource group, create it
az network vnet create -n "$vms_vnet_name" -g "$resource_group_name" --subnet-name "$vms_vnet_subnet_name"


# Deploy the VMs
az group deployment create --resource-group "$resource_group_name" --name 'e2e-proxy' --template-file ./proxy-deployment-template.json --parameters "$(
    jq -n \
        --arg vms_ssh_key_encoded "$(base64 -w 0 $keyfile)" \
        --arg vms_ssh_public_key "$(cat $keyfile.pub)" \
        --arg vms_username "$vms_username" \
        --arg key_vault_id "$key_vault_id" \
        --arg secret_name "$secret_name" \
        --arg vms_vnet_name "$vms_vnet_name" \
        --arg vms_vnet_subnet_name "$vms_vnet_subnet_name" \
        --arg vsts_agent_vm_public_ip_name "$vsts_agent_vm_public_ip_name" \
        --arg vsts_agent_vm_name "$vsts_agent_vm_name" \
        --arg vsts_runner1_vm_name "$vsts_runner1_vm_name" \
        --arg vsts_runner2_vm_name "$vsts_runner2_vm_name" \
        '{
            "vms_ssh_key_encoded": { "value": $vms_ssh_key_encoded },
            "vms_ssh_public_key": { "value": $vms_ssh_public_key },
            "vms_username": { "value": $vms_username },
            "windows_vm_password": {
                "reference": {
                    "keyVault": { "id": $key_vault_id },
                    "secretName": "$secret_name"
                }
            },
            "vms_vnet_name": { "value": $vms_vnet_name },
            "vms_vnet_subnet_name": { "value": $vms_vnet_subnet_name },
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
$vms_username='vsts'
$vms_vnet_name='<>'
$vms_vnet_subnet_name='default'
$vsts_agent_vm_name='e2eproxyvstsagent'
$vsts_runner1_vm_name='e2eproxyvstsrunner1'
$vsts_runner2_vm_name='e2eproxyrunner2'
$vsts_agent_vm_public_ip_name='e2eproxy'
$key_vault_name='<>'
$secret_name='windows-vm-admin-password'

# On Windows 1809 or later, install the ssh-agent feature first; see https://docs.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse
$keyfile=$(Join-Path (pwd).Path id_rsa)
ssh-keygen -t rsa -b 4096 -f "$keyfile" --% -N ""

Connect-AzAccount

Set-AzContext -Subscription "$subscription_name"

New-AzResourceGroup -Name "$resource_group_name" -Location "$location"

New-AzKeyVault `
  -Name "$key_vault_name" `
  -ResourceGroupName "$resource_group_name" `
  -Location "$location" `
  -EnabledForTemplateDeployment

$key_vault_id=(Get-AzKeyVault `
  -VaultName "$key_vault_name" `
  -ResourceGroupName "$resource_group_name").ResourceId

Set-AzKeyVaultSecret `
  -VaultName "$key_vault_name" `
  -Name "$secret_name" `
  -SecretValue (ConvertTo-SecureString "$(openssl rand -base64 32)" -AsPlainText -Force)

$address_prefix='10.0.0.0/24'
New-AzVirtualNetwork `
  -Name "$vms_vnet_name" `
  -ResourceGroupName "$resource_group_name" `
  -Location "$location" `
  -AddressPrefix "$address_prefix" `
  -Subnet (New-AzVirtualNetworkSubnetConfig -Name $vms_vnet_subnet_name -AddressPrefix "$address_prefix")

New-AzResourceGroupDeployment `
  -Name 'e2eproxy' `
  -ResourceGroupName "$resource_group_name" `
  -TemplateFile '.\proxy-deployment-template.json' `
  -vms_ssh_key_encoded "$([System.Convert]::ToBase64String([System.Text.Encoding]::Utf8.GetBytes($(Get-Content "$keyfile" -Raw))))" `
  -vms_ssh_public_key "$(cat "$keyfile.pub")" `
  -vms_username "$vms_username" `
  -vms_vnet_name "$vms_vnet_name" `
  -vms_vnet_subnet_name "$vms_vnet_subnet_name" `
  -vsts_agent_vm_public_ip_name "$vsts_agent_vm_public_ip_name" `
  -vsts_agent_vm_name "$vsts_agent_vm_name" `
  -vsts_runner1_vm_name "$vsts_runner1_vm_name" `
  -vsts_runner2_vm_name "$vsts_runner2_vm_name" `
  -TemplateParameterObject @{ "windows_vm_password" = @{ "reference" = @{ "keyVault" = @{ "id" = "$key_vault_id" }; "secretName" = $secret_name } } }
```
