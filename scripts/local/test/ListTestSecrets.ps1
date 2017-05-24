Try {
  Get-AzureRmContext
} Catch {
  if ($_ -like "*Login-AzureRmAccount to login*") {
    Login-AzureRmAccount
  }
}

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

Get-AzureKeyVaultSecret -VaultName edgebuildkv