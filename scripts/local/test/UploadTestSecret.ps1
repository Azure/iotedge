Param([Parameter(Mandatory=$true)] [string]$SecretName,
	  [Parameter(Mandatory=$true)] [string]$SecretValue)

. .\Login.ps1
Login

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

$vaultName = 'edgebuildkv'

$secureSecretValue = ConvertTo-SecureString -String $SecretValue -AsPlainText -Force
$secret = Set-AzureKeyVaultSecret -VaultName $vaultName -Name $SecretName -SecretValue $secureSecretValue

Write-Host Secret $secret.Id was stored in Test KeyVault $vaultName -foregroundcolor "green"