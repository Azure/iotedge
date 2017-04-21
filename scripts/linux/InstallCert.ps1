<#
    Installs a into the CurrentUser\my location
#>

Param([Parameter(Mandatory=$true)] [string]$CertificateValue)

function InstallCertificate($CertificateValue)
{
	$jsonObjectBytes = [System.Convert]::FromBase64String($CertificateValue)
	$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @(,$jsonObjectBytes)

	$certStore = New-Object System.Security.Cryptography.X509Certificates.X509Store([System.Security.Cryptography.X509Certificates.StoreName]::My, [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
	$certStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)

	$certStore.Add($cert)
}

InstallCertificate $CertificateValue