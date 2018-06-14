Param([Parameter(Mandatory=$true)] [string]$SecretName)

. .\Login.ps1
Login

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

$vaultName = 'edgebuildkv'
$secret = Get-AzureKeyVaultSecret -VaultName $vaultName -Name $SecretName

if ($secret)
{
    Write-Host Secret $SecretName value $secret.SecretValueText -foregroundcolor "green"
}
else 
{
    Write-Host Secret $SecretName does not exist. -foregroundcolor "red"
}