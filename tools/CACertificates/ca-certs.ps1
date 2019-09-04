## Copyright (c) Microsoft. All rights reserved.
## Licensed under the MIT license. See LICENSE file in the project root for full license information.

###############################################################################
# This script demonstrates creating X.509 certificates for an Azure IoT Hub
# CA Cert deployment.
#
# These certs MUST NOT be used in production.  It is expected that production
# certificates will be created using a company's proper secure signing process.
# These certs are intended only to help demonstrate and prototype CA certs.
###############################################################################

# This will make PowerShell complain more on unsafe practices
Set-StrictMode -Version 2.0

#
#  Globals
#

# Errors in system routines will stop script execution
$errorActionPreference       = "stop"

$_basePath                   = $PSScriptRoot
$env:CERTIFICATE_OUTPUT_DIR  = $_basePath
$_rootCertCommonName         = "Azure IoT CA TestOnly Root CA"
$_rootCertSubject            = "`"/CN=$_rootCertCommonName`""
$_rootCAPrefix               = "azure-iot-test-only.root.ca"
$_intermediateCommonName     = "Azure IoT CA TestOnly Intermediate CA"
$_intermediatePrefix         = "azure-iot-test-only.intermediate"
$_privateKeyPassword         = "1234"
$_keyBitsLength              = "4096"
if (-not (Test-Path env:DEFAULT_VALIDITY_DAYS)) { $env:DEFAULT_VALIDITY_DAYS = 30 }
$_days_until_expiration      = $env:DEFAULT_VALIDITY_DAYS
$_opensslRootConfigFile      = Join-Path $_basePath "openssl_root_ca.cnf"
$_keySuffix                  = "key.pem"
$_certSuffix                 = "cert.pem"
$_certPfxSuffix              = "cert.pfx"
$_csrSuffix                  = "csr.pem"
# Whether to use ECC or RSA is stored in a file.  If it doesn't exist, we default to ECC.
$algorithmUsedFile           = Join-Path $_basePath "algorithmUsed.txt"
# avoid pesky conf file not found warnings when running certain openssl
# commands that do not accept a config file argument
$env:OPENSSL_CONF            = $_opensslRootConfigFile
# despite being specified in openssl conf file, on windows hosts this is required
$env:RANDFILE                = Join-Path $_basePath ".rnd"
$FORCE_NO_PROD_WARNING       = if (-not (Test-Path env:FORCE_NO_PROD_WARNING)) { $False } else { $True }

<#
    .SYNOPSIS
        Print a warning message conditionally
    .DESCRIPTION
        The Invoke-External cmdlet enables the synchronous execution of external commands. The external
        error stream is automatically redirected into the external output stream. Execution information is
        written to the verbose stream. If a nonzero exit code is returned from the external command, the
        output string is thrown as an exception.
    .PARAMETER Message
        The message string to print.
#>
function Write-Warning-Msg([Parameter(Mandatory = $true)][String] $Message)
{
    if (-not $FORCE_NO_PROD_WARNING)
    {
        Write-Warning $Message
    }
}

<#
    .SYNOPSIS
        Invoke an external OS command using PowerShell semantics.
    .DESCRIPTION
        The Invoke-External cmdlet enables the synchronous execution of external commands. The external
        error stream is automatically redirected into the external output stream. Execution information is
        written to the verbose stream. If a nonzero exit code is returned from the external command, the
        output string is thrown as an exception.
    .PARAMETER Command
        The command to execute.
    .PARAMETER Passthru
        Return the output string for each executed command.
#>
function Invoke-External()
{
    #Requires -Version 5
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [String] $Command,
        [Switch] $Passthru
    )

    process {
        $output = $null
        Write-Host "Executing: $Command"
        # this store - restore of errorActionPreference is needed so that
        # test automation failures are not observed since an Invoke-Expression
        # might cause output on stderr but still have a zero exit code.
        # An example of this is openssl commands
        $oldErrorActionPreference = $errorActionPreference
        try {
            $errorActionPreference = 'Continue'
            Invoke-Expression $Command | Tee-Object -Variable "output" | Write-Verbose
        }
        finally {
            $errorActionPreference = $oldErrorActionPreference
        }
        Write-Host "Exit code: $LASTEXITCODE"

        if ($LASTEXITCODE) {
            throw $output
        } elseif ($Passthru) {
            $output
        }
    }
}

<#
    .SYNOPSIS
        Helper function to obtain a path to a certificate given its prefix.
    .DESCRIPTION
        Helper function to obtain a path to a certificate given its prefix.
        A certificate file is created using the following scheme ./certs/$prefix.cert.pem.
#>
function Get-CertPathForPrefix([string]$prefix)
{
    $certsDir = Join-Path $_basePath "certs"
    return Join-Path $certsDir "$prefix.$_certSuffix"
}

<#
    .SYNOPSIS
        Helper function to obtain a path to a PFX certificate given its prefix.
    .DESCRIPTION
        Helper function to obtain a path to a certificate given its prefix.
        A certificate file is created using the following scheme ./certs/$prefix.cert.pfx.
#>
function Get-PfxCertPathForPrefix([string]$prefix)
{
    $certsDir = Join-Path $_basePath "certs"
    return Join-Path $certsDir "$prefix.$_certPfxSuffix"
}

<#
    .SYNOPSIS
        Helper function to obtain a path to a key file given its prefix.
    .DESCRIPTION
        Helper function to obtain a path to the certificate given its prefix.
        A certificate file is created using the following scheme ./private/$prefix.key.pem.
#>
function Get-KeyPathForPrefix([string]$prefix)
{
    $privDir = Join-Path $_basePath "private"
    return Join-Path $privDir "$prefix.$_keySuffix"
}

<#
    .SYNOPSIS
        Helper function to obtain a path to a CSR file given its prefix.
    .DESCRIPTION
        Helper function to obtain a path to the certificate signing request (CSR)
        given its prefix. A certificate file is created using the following
        scheme ./csr/$prefix.csr.pem.
#>
function Get-CSRPathForPrefix([string]$prefix)
{
    $csrDir = Join-Path $_basePath "csr"
    return Join-Path $csrDir "$prefix.$_csrSuffix"
}

<#
    .SYNOPSIS
    Function to check if there is already a cert of the root CA present in the certificate store.
    .DESCRIPTION
    This function detects if a the root CA certifcate is already installed in the cert store
    and warns the user to clean it up since they might be installing newer ones after
    executing this script.
    .PARAMETER printMsg
    An optional bool that controls if additional diagnotics messages are to be printed or not.
#>
function Test-CACertNotInstalledAlready([bool]$printMsg=$true)
{
    if ($TRUE -eq $printMsg)
    {
        Write-Host ("Testing if any test root certificates have already been installed...")
    }
    $certInstalled = $null
    try
    {
        $certInstalled = Get-CACertsCertBySubjectName $_rootCertSubject
    }
    catch
    {

    }

    if ($NULL -ne $certInstalled)
    {
        $nl = [Environment]::NewLine
        $cleanup_msg  = "$nl$nl"
        $cleanup_msg += "This utility detected test certificates already installed in the certificate store.$nl"
        $cleanup_msg += "Since newer root CA certificates will be generated, it is recommended to clean any stale root certificates.$nl"
        $cleanup_msg += "Steps to cleanup, from the 'Start' menu, type 'open manage computer certificates':$nl"
        $cleanup_msg += " - Navigate to Certificates -> Trusted Root Certification Authority -> Certificates. Remove certificates issued by 'Azure IoT CA TestOnly*'.$nl"
        $cleanup_msg += " - Navigate to Certificates -> Intermediate Certificate Authorities -> Certificates. Remove certificates issued by 'Azure IoT CA TestOnly*'.$nl"
        $cleanup_msg += " - Navigate to Certificates -> Local Computer -> Personal. Remove certificates issued by 'Azure IoT CA TestOnly*'.$nl"
        $cleanup_msg += "$nl$nl"
        Write-Warning-Msg("Certificate {0} already installed in the certificate store. {1}" -f $_rootCertSubject,  $cleanup_msg)
        throw ("Certificate {0} already installed." -f $_rootCertSubject)
    }
    if ($TRUE -eq $printMsg)
    {
        Write-Host ("  Ok.")
    }
}

<#
    .SYNOPSIS
    Verify that the prerequisites for this script are met.
    .DESCRIPTION
    Helper function verify that the prerequisites dependencies for this script are met
    .PARAMETER printMsg
        An optional bool that controls if additional diagnotics messages are to be printed or not.
#>
function Test-CACertsPrerequisites([bool]$printMsg=$true)
{
    Test-CACertNotInstalledAlready($printMsg)
    $openssl_ext = ""
    if ($env:OS -eq "Windows_NT")
    {
        $openssl_ext = ".exe"
    }
    $openssl_exe_name = "openssl" + $openssl_ext
    if ($TRUE -eq $printMsg)
    {
        Write-Host ("Testing if $openssl_exe_name executable is set in PATH...")
    }
    if ($NULL -eq (Get-Command $openssl_exe_name -ErrorAction SilentlyContinue))
    {
        throw ("$openssl_exe_name is unavailable. Please install $openssl_exe_name and set it in the PATH before proceeding.")
    }
    if ($TRUE -eq $printMsg)
    {
        Write-Host ("  Ok.")
        Write-Host ("Testing if openssl_root_ca.cnf is available in script dir: $_basePath")
    }
    if (-not (Test-Path $_opensslRootConfigFile -PathType Leaf))
    {
        throw ("$_opensslRootConfigFile is unavailable. Please ensure it is in the same directory as this script file.")
    }
    if ($TRUE -eq $printMsg)
    {
        Write-Host ("  Ok.")
        Write-Host ("Success")
    }
}

<#
    .SYNOPSIS
    Prepare the nessary files and directories to hold the resulting certificates.
    .DESCRIPTION
    Prepare the nessary files and directories to hold the resulting certificates. This will be
    called if a new Root CA is being created thus wiping out any prior generated certificates.
#>
function PrepareFilesystem()
{
    $csrPath = Join-Path $_basePath "csr"
    $privatePath = Join-Path $_basePath "private"
    $certsPath = Join-Path $_basePath "certs"
    $intermediateCertsPath = Join-Path $_basePath "intermediateCerts"
    $newcertsPath = Join-Path $_basePath "newcerts"
    $indexPath = Join-Path $_basePath "index.txt"
    $serialPath = Join-Path $_basePath "serial"

    Remove-Item -Path $csrPath -Recurse -Force -ErrorAction Ignore
    Remove-Item -Path $privatePath -Recurse -Force -ErrorAction Ignore
    Remove-Item -Path $certsPath -Recurse -Force -ErrorAction Ignore
    Remove-Item -Path $intermediateCertsPath -Force -ErrorAction Ignore
    Remove-Item -Path $newcertsPath -Recurse -Force -ErrorAction Ignore

    $item = New-Item -ItemType directory -Path $csrPath
    $item = New-Item -ItemType directory -Path $privatePath
    $item = New-Item -ItemType directory -Path $certsPath
    $item = New-Item -ItemType directory -Path $intermediateCertsPath
    $item = New-Item -ItemType directory -Path $newcertsPath

    Remove-Item -Path $indexPath -ErrorAction Ignore
    $item = New-Item $indexPath -ItemType file

    Remove-Item -Path $serialPath -ErrorAction Ignore
    $item = New-Item $serialPath -ItemType file
    "1000`n" | Out-File -NoNewline -Encoding ASCII $serialPath
}

<#
    .SYNOPSIS
    Creates an asymmetric private key to be used for certificate generation.
    .DESCRIPTION
    Generate an ECC or RSA key with an optional passphrase. The choice of what type of key
    to create is determined by function Get-CACertsCertUseRSA(). The private key file
    will be saved to the private dir and named using the provided prefix.
    .PARAMETER prefix
        Prefix is used to name the file containing the private key
    .PARAMETER keyPass
        Optional passphrase used to encrypt the private key
#>
function New-PrivateKey([string]$prefix, [string]$keyPass=$NULL)
{
    Write-Host ("Creating the $prefix private Key")

    $passwordCreateCmd = ""
    if (-not [string]::IsNullOrWhiteSpace($keyPass))
    {
        $passwordCreateCmd = "-aes256 -passout pass:$keyPass"
    }

    $algorithm = ""
    $cmdEpilog = ""
    if ($TRUE -eq (Get-CACertsCertUseRSA))
    {
        $algorithm = "genrsa"
        $cmdEpilog = "$_keyBitsLength"
    }
    else
    {
        $algorithm = "ecparam -genkey -name secp256k1"
        $passwordCreateCmd = " | openssl ec $passwordCreateCmd"
        $cmdEpilog = ""
    }

    $keyFile = Get-KeyPathForPrefix($prefix)
    $cmd = "openssl $algorithm $passwordCreateCmd -out '$keyFile' $cmdEpilog 2>&1"
    Invoke-External -verbose $cmd
    return $keyFile
}

<#
    .SYNOPSIS
    Generate a certificate using an issuer certificate and key. Certificates could be
    either CA, server and client certificates.
    .DESCRIPTION
    Generate a client certificate using the supplied certificate and key parameters and
    have it signed by an issuer certificate and key.
    Issuer is specified by its prefix and its corresponding key and certificate
    will be retrieved from the filesystem.
    .PARAMETER x509Ext
        A string to identify which configuration to use to generate a certificate.
    .PARAMETER expirationDays
        Number of days from now to set the certificate expiration timestamp.
    .PARAMETER subject
        Value of the certificate subject to use to generate a certificate.
    .PARAMETER prefix
        Prefix is used to name the file containing the private key
    .PARAMETER issuerPrefix
        Issuer prefix to look up the certifcate and key
    .PARAMETER keyPass
        Optional passphrase used to encrypt the private key
    .PARAMETER issuerKeyPass
        Optional passphrase used to decrypt the issuer private key when signing the certificate.
#>
function New-IntermediateCertificate
(
    [ValidateSet("usr_cert","v3_intermediate_ca","server_cert")][string]$x509Ext,
    [string]$expirationDays,
    [string]$subject,
    [string]$prefix,
    [string]$issuerPrefix,
    [string]$keyPass=$NULL,
    [string]$issuerKeyPass=$NULL
)
{
    $issuerKeyFile = Get-KeyPathForPrefix($issuerPrefix)
    if (-not (Test-Path $issuerKeyFile -PathType Leaf))
    {
        Write-Host ("Private key file not found: $issuerKeyFile")
        throw ("Issuer '$issuerPrefix' private key not found")
    }

    $issuerCertFile = Get-CertPathForPrefix($issuerPrefix)
    if (-not (Test-Path $issuerCertFile -PathType Leaf))
    {
        Write-Host ("Certificate file not found: $issuerCertFile")
        throw ("Issuer '$issuerPrefix' certificate not found")
    }

    Write-Host ("Creating the Intermediate CSR for $prefix")
    Write-Host "-----------------------------------"
    $keyFile = New-PrivateKey $prefix $keyPass

    $keyPassUseCmd = ""
    if (-not [string]::IsNullOrWhiteSpace($keyPass))
    {
        $keyPassUseCmd = "-passin pass:$keyPass"
    }
    $csrFile = Get-CSRPathForPrefix($prefix)

    $cmd =  "openssl req -new -sha256 $keyPassUseCmd "
    $cmd += "-config '$_opensslRootConfigFile' "
    $cmd += "-subj $subject "
    $cmd += "-key '$keyFile' "
    $cmd += "-out '$csrFile' 2>&1"
    Invoke-External -verbose $cmd

    Write-Host ("Signing the certificate for $prefix with issuer certificate $issuerPrefix")
    Write-Host "-----------------------------------"
    $keyPassUseCmd = ""
    if (-not [string]::IsNullOrWhiteSpace($issuerKeyPass))
    {
        $keyPassUseCmd = "-key $issuerKeyPass"
    }
    $certFile = Get-CertPathForPrefix($prefix)

    $cmd =  "openssl ca -batch "
    $cmd += "-config '$_opensslRootConfigFile' "
    $cmd += "-extensions $x509Ext "
    $cmd += "-days $expirationDays -notext -md sha256 "
    $cmd += "-cert '$issuerCertFile' "
    $cmd += "$keyPassUseCmd -keyfile '$issuerKeyFile' -keyform PEM "
    $cmd += "-in '$csrFile' -out '$certFile' 2>&1"
    Invoke-External -verbose $cmd

    Write-Host ("Verifying the certificate for $prefix with issuer certificate $issuerPrefix")
    Write-Host ("---------------------------------")
    $rootCertFile = Get-CertPathForPrefix($_rootCAPrefix)
    $cmd =  "openssl verify -CAfile '$rootCertFile' -untrusted '$issuerCertFile' '$certFile' 2>&1"
    Invoke-External -verbose $cmd

    Write-Host ("Certificate for prefix $prefix generated at:")
    Write-Host ("---------------------------------")
    Write-Host ("    $certFile`r`n")
    $cmd = "openssl x509 -noout -text -in '$certFile' 2>&1"
    Invoke-External $cmd

    New-CertFullChain $certFile $prefix $issuerPrefix $subject

    Write-Host ("Create the full chain $prefix PFX Certificate")
    Write-Host ("----------------------------------------")
    $keyPassUseCmd = ""
    if (-not [string]::IsNullOrWhiteSpace($keyPass))
    {
        $keyPassUseCmd = "-passin pass:$keyPass -passout pass:$keyPass"
    }
    else
    {
        $keyPassUseCmd = "-passout pass:"
    }
    $certFilePfx = Get-PfxCertPathForPrefix($prefix)

    $issuerChain = ""
    if ($issuerPrefix -eq $_rootCAPrefix)
    {
        $issuerChain = Get-CertPathForPrefix($_rootCAPrefix)
    }
    else
    {
        $issuerChain = Get-CertPathForPrefix("$issuerPrefix-full-chain")
    }
    $cmd =  "openssl pkcs12 -export "
    $cmd += "-in '$certFile' -certfile '$issuerChain' "
    $cmd += "-inkey '$keyFile' $keyPassUseCmd "
    $cmd += "-name $prefix "
    $cmd += "-out '$certFilePfx' 2>&1"
    Invoke-External -verbose $cmd
    Write-Host ("$prefix PFX Certificate Generated At:")
    Write-Host ("----------------------------------------")
    Write-Host ("    '$certFilePfx'`r`n")
}

function New-CertFullChain([string]$certFile, [string]$prefix, [string]$issuerPrefix, [string]$subject)
{
    $fullCertChain = Get-CertPathForPrefix("$prefix-full-chain")
    if ($issuerPrefix -eq $_rootCAPrefix)
    {
        $issuerFullChainCertFileName  = Get-CertPathForPrefix($_rootCAPrefix)
    }
    else
    {
        $issuerFullChainCertFileName  = Get-CertPathForPrefix("$issuerPrefix-full-chain")
    }
    Get-Content $certFile, $issuerFullChainCertFileName | Set-Content $fullCertChain
    Write-Host ("Certificate with subject {0} has been output to {1} and with full chain to {2}" -f $subject, $certFile, $fullCertChain)
}

<#
    .SYNOPSIS
    Generate a client certificate using an issuer certificate and key
    .DESCRIPTION
    Generate a client certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER prefix
        Prefix is used to name the file containing the private key
    .PARAMETER issuerPrefix
        Issuer prefix to look up the certifcate and key
    .PARAMETER commonName
        Value of the CN field to set when generating the certifcate
#>
function New-ClientCertificate([string]$prefix, [string]$issuerPrefix, [string]$commonName)
{
    $subject = "`"/CN=$commonName`""
    Write-Warning-Msg ("Generating client certificate CN={0} which is for prototyping, NOT PRODUCTION.  It has a hard-coded password and will expire in {1} days." -f $commonName, $_days_until_expiration)
    New-IntermediateCertificate "usr_cert" $_days_until_expiration $subject $prefix  $issuerPrefix $NULL $_privateKeyPassword
}

<#
    .SYNOPSIS
    Generate a server certificate using an issuer certificate and key
    .DESCRIPTION
    Generate a server certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER prefix
        Prefix is used to name the file containing the private key
    .PARAMETER issuerPrefix
        Issuer prefix to look up the certifcate and key
    .PARAMETER commonName
        Value of the CN field to set when generating the certifcate
#>
function New-ServerCertificate([string]$prefix, [string]$issuerPrefix, [string]$commonName)
{
    $subject = "`"/CN=$commonName`""
    Write-Warning-Msg ("Generating server certificate CN={0} which is for prototyping, NOT PRODUCTION.  It has a hard-coded password and will expire in {1} days." -f $commonName, $_days_until_expiration)
    New-IntermediateCertificate "server_cert" $_days_until_expiration $subject $prefix $issuerPrefix $NULL $_privateKeyPassword
}

<#
    .SYNOPSIS
    Generate an intermediate CA certificate using an issuer certificate and key
    .DESCRIPTION
    Generate an intermediate CA certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER prefix
        Prefix is used to name the file containing the private key
    .PARAMETER issuerPrefix
        Issuer prefix to look up the certifcate and key
    .PARAMETER commonName
        Value of the CN field to set when generating the certifcate
#>
function New-IntermediateCACertificate([string]$prefix, [string]$issuerPrefix, [string]$commonName, [string]$keyPass=$NULL, [string]$issuerKeyPass=$NULL)
{
    $subject = "`"/CN=$commonName`""
    Write-Warning-Msg ("Generating certificate CN={0} which is for prototyping, NOT PRODUCTION.  It has a hard-coded password and will expire in {1} days." -f $commonName, $_days_until_expiration)
    New-IntermediateCertificate "v3_intermediate_ca" $_days_until_expiration $subject $prefix $issuerPrefix $keyPass $issuerKeyPass
}

<#
    .SYNOPSIS
    Generate a root CA certificate
    .DESCRIPTION
    Generate a root CA certificate using the parameters specified in the script.
#>
function New-RootCACertificate()
{
    Write-Host ("Creating the Root CA private key")
    $keyFile = New-PrivateKey $_rootCAPrefix $_privateKeyPassword
    $certFile = Get-CertPathForPrefix($_rootCAPrefix)

    Write-Host ("Creating the Root CA certificate")
    $passwordUseCmd = "-passin pass:$_privateKeyPassword"
    $cmd =  "openssl req -new -x509 -config $_opensslRootConfigFile $passwordUseCmd "
    $cmd += "-key $keyFile -subj $_rootCertSubject -days $_days_until_expiration "
    $cmd += "-sha256 -extensions v3_ca -out $certFile 2>&1"
    Invoke-External -verbose $cmd

    Write-Host ("CA Root Certificate Generated At:")
    Write-Host ("---------------------------------")
    Write-Host ("    $certFile`r`n")
    $cmd = "openssl x509 -noout -text -in $certFile 2>&1"
    Invoke-External $cmd

    # Now use splatting to process this
    Write-Warning-Msg ("Generating certificate {0} which is for prototyping, NOT PRODUCTION.  It has a hard-coded password and will expire in {1} days." -f $_rootCertSubject, $_days_until_expiration)

    return $certFile
}

<#
    .SYNOPSIS
    Generate a new certificate chain.
    .DESCRIPTION
    Generate a root CA certificate using the parameters specified in the script.
    .PARAMETER algorithm
        ECC or RSA keys to be used to create the chain and all resulting certificates.
#>
function New-CACertsCertChain([Parameter(Mandatory=$TRUE)][ValidateSet("rsa","ecc")][string]$algorithm)
{
    Write-Host "Beginning to create certificates in your filesystem here $_basePath"
    Test-CACertsPrerequisites($FALSE)
    PrepareFilesystem

    # Store the algorithm we're using in a file so later stages always use the same one (without forcing user to keep passing it around)
    Set-Content $algorithmUsedFile $algorithm

    New-RootCACertificate
    New-IntermediateCACertificate $_intermediatePrefix $_rootCAPrefix $_intermediateCommonName $_privateKeyPassword $_privateKeyPassword
    Write-Host "Success"
}

<#
    .SYNOPSIS
        Install a root CA certificate and use this to generate the intermediate
        certificate and the rest of the chain
    .DESCRIPTION
        Install a root CA certificate using the supplied certificate and key files
        in the PEM format.
    .PARAMETER rootCAFile

    .PARAMETER rootCAKeyFile

    .PARAMETER algorithm
        ECC or RSA algorith used in the creation of the root certificate.
        This will be used for all resulting certificates.
#>
function Install-RootCACertificate(
    [Parameter(Position=0,Mandatory=$TRUE)][string]$rootCAFile,
    [Parameter(Position=1,Mandatory=$TRUE)][string]$rootCAKeyFile,
    [Parameter(Position=2,Mandatory=$TRUE)][ValidateSet("rsa","ecc")][string]$algorithm,
    [Parameter(Position=3)][string]$rootPrivateKeyPassword=$_privateKeyPassword)
{
    Write-Host "Beginning to install the root certificates in your filesystem here $_basePath"
    Test-CACertsPrerequisites($FALSE)
    PrepareFilesystem

    # Store the algorithm we're using in a file so later stages always use the same one (without forcing user to keep passing it around)
    Set-Content $algorithmUsedFile $algorithm

    $rootKeyFile = Get-KeyPathForPrefix($_rootCAPrefix)
    Write-Host ("Copying the Root CA private key to $rootKeyFile")
    Copy-Item $rootCAKeyFile $rootKeyFile

    $rootCertFile = Get-CertPathForPrefix($_rootCAPrefix)
    Write-Host ("Copying the Root CA certificate to $rootCertFile")
    Copy-Item $rootCAFile $rootCertFile

    New-IntermediateCACertificate $_intermediatePrefix $_rootCAPrefix $_intermediateCommonName $_privateKeyPassword $rootPrivateKeyPassword
    Write-Host "Success"
}

# Get-CACertsCertUseEdge retrieves the algorithm (RSA vs ECC) that was specified during New-CACertsCertChain
function Get-CACertsCertUseRsa()
{
    Write-Output ((Get-Content $algorithmUsedFile -ErrorAction SilentlyContinue) -eq "rsa")
}

<#
    .SYNOPSIS
    Queries the Windows certificate store to check if a certificate is already installed using the
    provided subjectName.
    .DESCRIPTION
    Queries the Windows certificate store to check if a certificate is already installed using the
    provided subjectName.
    .PARAMETER subjectName
        Subject name to be used when querying the certificate store.
#>
function Get-CACertsCertBySubjectName([string]$subjectName)
{
    $certificates = Get-ChildItem -Recurse Cert:\LocalMachine\ |? { $_.gettype().name -eq "X509Certificate2" }
    $cert = $certificates |? { $_.subject -eq $subjectName -and $_.PSParentPath -eq "Microsoft.PowerShell.Security\Certificate::LocalMachine\My" }
    if ($NULL -eq $cert)
    {
        throw ("Unable to find certificate with subjectName {0}" -f $subjectName)
    }

    Write-Output $cert
}

<#
    .SYNOPSIS
    Generate a client certificate to be used for private key possession test for IoT Hub
    .DESCRIPTION
    The verification certificate is client certificate issued by the root CA. The IoT hub
    generated a common name which is to be supplied as parameter requestedCommonName.
    .PARAMETER requestedCommonName
        Common name to be used when generating the IoT Hub certificate.
#>
function New-CACertsVerificationCert([Parameter(Mandatory=$TRUE)][string]$requestedCommonName)
{
    if ([string]::IsNullOrWhiteSpace($requestedCommonName))
    {
        throw "Verification string parameter is required and cannot be null or empty"
    }
    $verificationPrefix = "iot-device-verification-code"
    $requestedCommonName = $requestedCommonName.Trim()
    $verifCertPath = Get-CertPathForPrefix($verificationPrefix)
    $verifCertFullChainPath = Get-CertPathForPrefix("$verificationPrefix-full-chain")
    $verifKeyPath = Get-KeyPathForPrefix($verificationPrefix)
    Remove-Item -Path $verifCertPath -ErrorAction SilentlyContinue
    Remove-Item -Path $verifCertFullChainPath -ErrorAction SilentlyContinue
    Remove-Item -Path $verifKeyPath -ErrorAction SilentlyContinue
    New-ClientCertificate $verificationPrefix $_rootCAPrefix $requestedCommonName
    if (-not (Test-Path $verifCertPath))
    {
        throw ("Error: CERT file {0} doesn't exist" -f $verifCertPath)
    }
}

<#
    .SYNOPSIS
    Generate an IoT Edge/Hub device certificate using an issuer's certificate and key
    .DESCRIPTION
    Generate a certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    If parameter isEdgeDevice is true the resulting certificate is a CA certificate else a
    client certificate will be created.
    .PARAMETER deviceName
        Device name will be used as a prefix and as the value of the CN field
    .PARAMETER issuerPrefix
        Optional issuer prefix to look up the certifcate and key. If null the root CA is used.
    .PARAMETER filePrefix
        The file name prefix to be used when creating the certificate and keys
#>
function New-CACertsDevice(
    [Parameter(Mandatory=$TRUE)]
    [string]$deviceName,
    [string]$issuerPrefix=$_intermediatePrefix,
    [ValidateSet("iot-device","iot-edge-device", "iot-edge-device-ca", "iot-edge-device-identity")]
    [string]$filePrefix="iot-device")
{
    if ([string]::IsNullOrWhiteSpace($deviceName))
    {
        throw "Device name string parameter is required and cannot be null or empty"
    }
    $deviceName = $deviceName.Trim()

    $devicePrefix = "$filePrefix-$deviceName"
    # Certificates for edge devices need to be able to sign other certs.
    $edgePrefixes = @('iot-edge-device', 'iot-edge-device-ca')
    if ($edgePrefixes.Contains($filePrefix))
    {
        # Note: Appending a '.ca' to the common name is useful in situations
        # where a user names their hostname as the edge device name.
        # By doing so we avoid TLS validation errors where we have a server or
        # client certificate where the hostname is used as the common name
        # which essentially results in "loop" for validation purposes.
        $deviceName += ".ca"
        New-IntermediateCACertificate $devicePrefix $issuerPrefix $deviceName $NULL $_privateKeyPassword
    }
    else
    {
        New-ClientCertificate $devicePrefix $issuerPrefix $deviceName
    }
}

<#
    .SYNOPSIS
    Generate an IoT Edge device CA certificate, private key and full certificate chain
    using an issuer's certificate and key
    .DESCRIPTION
    Generate a certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER deviceName
        Device name will be used as a prefix and as the value of the CN field
    .PARAMETER issuerPrefix
        Optional issuer prefix to look up the certifcate and key. If null the default intermediate
        CA certificate is used.
#>
function New-CACertsEdgeDevice([Parameter(Mandatory=$TRUE)][string]$deviceName, [string]$issuerPrefix=$_intermediatePrefix)
{
    if ([string]::IsNullOrWhiteSpace($deviceName))
    {
        throw "Edge device CA name string parameter is required and cannot be null or empty"
    }
    New-CACertsDevice $deviceName $issuerPrefix "iot-edge-device"
}

<#
    .SYNOPSIS
    Generate an IoT Edge device CA certificate, private key and full certificate chain
    using an issuer's certificate and key
    .DESCRIPTION
    Generate a certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER deviceName
        Device name will be used as a prefix and as the value of the CN field
    .PARAMETER issuerPrefix
        Optional issuer prefix to look up the certifcate and key. If null the default intermediate
        CA certificate is used.
#>
function New-CACertsEdgeDeviceCA([Parameter(Mandatory=$TRUE)][string]$deviceName, [string]$issuerPrefix=$_intermediatePrefix)
{
    if ([string]::IsNullOrWhiteSpace($deviceName))
    {
        throw "Edge device CA name string parameter is required and cannot be null or empty"
    }
    New-CACertsDevice $deviceName $issuerPrefix "iot-edge-device-ca"
}

<#
    .SYNOPSIS
    Generate an IoT Edge device identity certificate, private key and full certificate chain
    using an issuer's certificate and key
    .DESCRIPTION
    Generate a certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER deviceName
        Device name will be used as a prefix and as the value of the CN field
    .PARAMETER issuerPrefix
        Optional issuer prefix to look up the certifcate and key. If null the default intermediate
        CA certificate is used.
#>
function New-CACertsEdgeDeviceIdentity([Parameter(Mandatory=$TRUE)][string]$deviceName, [string]$issuerPrefix=$_intermediatePrefix)
{
    if ([string]::IsNullOrWhiteSpace($deviceName))
    {
        throw "Edge device name string parameter is required and cannot be null or empty"
    }
    New-CACertsDevice $deviceName $issuerPrefix "iot-edge-device-identity"
}

<#
    .SYNOPSIS
    Generate an IoT Edge device server certificate, private key and full certificate chain
    using an issuer's certificate and key
    .DESCRIPTION
    Generate a certificate using the supplied common name and have it signed by
    an issuer certificate and key. Issuer is specified by its prefix and its corresponding
    key and certificate will be retrieved from the filesystem.
    .PARAMETER hostName
        Hostname will be used as a prefix and as the value of the CN field
    .PARAMETER issuerPrefix
        Optional issuer prefix to look up the certifcate and key. If null the default intermediate
        CA certificate is used.
#>
function New-CACertsEdgeServer([Parameter(Mandatory=$TRUE)][string]$hostName, [string]$issuerPrefix=$_intermediatePrefix)
{
    if ([string]::IsNullOrWhiteSpace($hostName))
    {
        throw "Edge device name string parameter is required and cannot be null or empty"
    }
    $serverPrefix = "iot-edge-server-$hostName"
    New-ServerCertificate $serverPrefix $issuerPrefix $hostName
}

Write-Warning-Msg "This script is provided for prototyping only."
Write-Warning-Msg "DO NOT USE CERTIFICATES FROM THIS SCRIPT FOR PRODUCTION!"
