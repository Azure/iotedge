<#
    Installs a into the CurrentUser\my location
#>

Param([Parameter(Mandatory=$true)] [string]$CertificateValue, [ValidateNotNullOrEmpty()][string]$StoreName='My')

function InstallCertificate($CertificateValue)
{
	$jsonObjectBytes = [System.Convert]::FromBase64String($CertificateValue)
	$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @(,$jsonObjectBytes)
    $storeNameEnumVal = [System.Security.Cryptography.X509Certificates.StoreName] $StoreName
	$certStore = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeNameEnumVal, [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
	$certStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

	$certStore.Add($cert)
}

InstallCertificate $CertificateValue