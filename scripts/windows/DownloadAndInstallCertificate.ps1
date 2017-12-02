Param([Parameter(Mandatory=$true)] [string]$VaultName,
	  [Parameter(Mandatory=$true)] [string]$CertificateName)

function DownloadAndInstallCertificate($VaultName, $CertificateName)
{
	$secret = Get-AzureKeyVaultSecret -VaultName $VaultName -Name $CertificateName

	$jsonObjectBytes = [System.Convert]::FromBase64String($secret.SecretValueText)

	$certCollection = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
	$certCollection.Import($jsonObjectBytes, $null, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)

	$certStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::My, [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
	$certStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

	$certStore.AddRange($certCollection)
	$certStore.Close();
}

Write-Host "Downloading and installing certificate"

DownloadAndInstallCertificate $VaultName $CertificateName

Write-Host "Done"