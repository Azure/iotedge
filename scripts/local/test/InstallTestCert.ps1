Try {
  Get-AzureRmContext
} Catch {
  if ($_ -like "*Login-AzureRmAccount to login*") {
    Login-AzureRmAccount
  }
}

& "..\..\windows\DownloadAndInstallCertificate.ps1" edgebuildkv IoTEdgeTestCert