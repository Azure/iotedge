function Create-Azure-VM-For-E2E-Test
{
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)]
        [string]
        $VmName,

        [Parameter(Mandatory)]
        [string]
        $VmRegion,

        [Parameter(Mandatory)]
        [string]
        $ResourceGroup
    )

        # Future Iteration:
        # A complete version to do the VM creation is to do the followign 
        #  1. Query VSTS to see what's the current index for the test agent there is
        #  2. Name the test agent after that prefix + however many to be created
        #  3. Download the nestedEdge RootCA cert
        #  4. Create the VM using the same RootCA cert
        #    4.1 If fail, move over to the next region from the region list
        #  5. Download the dependency script
        #  6. Run the dependency script
        #  7. Download the VSTS agent 
        #  8. Have user go in an install the VSTS agent 
        #  9. Can we automate (8)?

        # Current version:
        # For this miniture version, we can do 
        #  A. Take the VmName
        #  B. Download the cert somewhere
        #  C. Call AzCli to create the VM with cert
        #  D. Install dependencies for E2E
        #  E. Install software for VSTS test agent.
        
        # Pre-requ: Install the Azure CLI
        #Invoke-WebRequest -Uri https://aka.ms/installazurecliwindows -OutFile .\AzureCLI.msi; Start-Process msiexec.exe -Wait -ArgumentList '/I AzureCLI.msi /quiet'
        az login

        $AdminUsername="iotedgeuser"
        # Fetch default subscription
        $AzSubscriptionName=$(az account show --query 'name' -o tsv)
        echo "Azure Subscription: $AzSubscriptionName `n"

        $VmName=$($VmName -replace '[\W_]', '');

        # The public key was generated from private key using : ssh-keygen -f <Path/To/PrivateKey> -y 
        $VmPubKey=$(az keyvault secret show --vault-name nestededgeVMkeys --name nestededgePubkey --query value);
        # Get ride of the " at the begging and " at the end along with an extra \n
        $VmPubKey = $VmPubKey.substring(1, $VmPubKey.length-4);

        # Ref: https://docs.microsoft.com/en-us/cli/azure/vm?view=azure-cli-latest#az_vm_create
        #   For more --image : az vm image list --output table
        az vm create `
            --name "$VmName" `
            --resource-group "$ResourceGroup" `
            --subscription "$AzSubscriptionName" `
            --accelerated-networking false `
            --authentication-type ssh `
            --admin-username "$AdminUsername" `
            --ssh-key-values "$VmPubKey" `
            --image 'Canonical:UbuntuServer:18.04-LTS:latest' `
            --size 'Standard_D4s_v3' `
            --location "$VmRegion"

        # Install necessary E2E dependency
        az vm extension set `
            --resource-group "$ResourceGroup" `
            --vm-name "$VmName" `
            --name customScript `
            --publisher Microsoft.Azure.Extensions `
            --protected-settings '{"fileUris": ["https://iotedgeforiiot.blob.core.windows.net/edge-config-scripts/e2eOneTimeDependencySetup.sh"],"commandToExecute": "./e2eOneTimeDependencySetup.sh;"}' `
            --output none

        # Other setup command
        #  - Set AdminUsername to docker group
        #  - Download the VSTS test agent zip to be used.
        additionalSetupCommand=" `
            sudo usermod -aG docker $AdminUsername; `
            cd /home/${AdminUsername}; `
            mkdir myagent && cd myagent; `
            wget https://vstsagentpackage.azureedge.net/agent/2.174.2/vsts-agent-linux-x64-2.174.2.tar.gz; `
            tar zxvf ./vsts-agent-linux-x64-2.174.2.tar.gz; `
            sudo chown -R ${AdminUsername} . `
        "

        az vm run-command invoke `
            -g $ResourceGroup `
            -n $VmName `
            --command-id RunShellScript `
            --scripts "$additionalSetupCommand" >> /dev/null
}