<#
.Synopsis
	Install a cert from KeyVault into the CurrentUser\my location
.Description
	This script downloads a certificate from KeyVault and then installs into the Current user
.Parameters
	VaultName - KeyVault to query for the certificate
	CertificateName - Name of the certificate in KeyVault
.Example
	Run
	.\InstallCertFromKeyVault.ps1 -VaultName iotdrsoneboxadmin -CertificateName iotdrsadmin
 #>

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

DownloadAndInstallCertificate $VaultName $CertificateName