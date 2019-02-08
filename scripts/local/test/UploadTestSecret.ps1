## Copyright (c) Microsoft. All rights reserved.
function Set-SecretFile([Parameter(Mandatory=$true)] [string]$SecretName,
						[Parameter(Mandatory=$true)] [string]$SecretFilePath)
{
    if (-not (Test-Path $SecretFilePath -PathType Leaf))
    {
        Write-Host ("Input secret file not found: $SecretFilePath")
        throw ("Secret file '$SecretFilePath' not found")
    }
	$SecretValue = Get-Content $SecretFilePath -Raw
	Set-Secret $SecretName $SecretValue
}

function Set-Secret([Parameter(Mandatory=$true)] [string]$SecretName,
   				    [Parameter(Mandatory=$true)] [string]$SecretValue)
{
	. .\Login.ps1
	Login

	Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

	$vaultName = 'edgebuildkv'

	$secureSecretValue = ConvertTo-SecureString -String $SecretValue -AsPlainText -Force
	$secret = Set-AzureKeyVaultSecret -VaultName $vaultName -Name $SecretName -SecretValue $secureSecretValue

	if ($NULL -ne $secret)
	{
		Write-Host Secret $secret.Id was stored in KeyVault $vaultName -foregroundcolor "green"
	}
	else
	{
		Write-Host There was an error writing to KeyVault $vaultName -foregroundcolor "red"
        throw ("Null response from Set-AzureKeyVaultSecret")
	}
}
