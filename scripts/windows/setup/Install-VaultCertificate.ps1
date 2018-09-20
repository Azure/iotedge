<#
 # Downloads and installs a certificate from an Azure Key Vault.
 #>

param (
	[Parameter(Mandatory=$true)]
	[String]$VaultName,
	
	[Parameter(Mandatory=$true)]
	[String]$CertificateName,

	[ValidateNotNullOrEmpty()]
	[String]$StoreName = 'My'
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

Write-Host "Downloading and installing certificate"

$Secret = Get-AzureKeyVaultSecret -VaultName $VaultName -Name $CertificateName
$CertificateBytes = [System.Convert]::FromBase64String($Secret.SecretValueText)

$CertificateCollection = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2Collection
$CertificateCollection.Import(
	$CertificateBytes,
	$null,
	[System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)

$storeNameEnumVal = [System.Security.Cryptography.X509Certificates.StoreName] $StoreName
$CertificateStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
	$storeNameEnumVal,
	[System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
$CertificateStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

$CertificateStore.AddRange($CertificateCollection)
$CertificateStore.Close()

Write-Host "Done!"
