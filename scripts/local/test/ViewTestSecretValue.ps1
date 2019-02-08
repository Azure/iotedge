## Copyright (c) Microsoft. All rights reserved.

Param([Parameter(Mandatory=$true)] [string]$SecretName,
      [string]$OutputFilePath=$NULL)

. .\Login.ps1
Login

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

$vaultName = 'edgebuildkv'
$secret = Get-AzureKeyVaultSecret -VaultName $vaultName -Name $SecretName

if ($secret)
{
    if ($NULL -ne $OutputFilePath)
    {
        Set-Content -Path $OutputFilePath -Value $secret.SecretValueText
        Write-Host Secret $SecretName output to file $OutputFilePath -foregroundcolor "green"
    }
    else
    {
        Write-Host Secret $SecretName value $secret.SecretValueText -foregroundcolor "green"
    }
}
else
{
    Write-Host Secret $SecretName does not exist. -foregroundcolor "red"
}