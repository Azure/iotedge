Param([Parameter(Mandatory=$true)] [string]$SecretName)

Try {
  Get-AzureRmContext
} Catch {
  if ($_ -like "*Login-AzureRmAccount to login*") {
    Login-AzureRmAccount
  }
}

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV_1

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