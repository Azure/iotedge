
# Bring in openssl install function
$openssl = Join-Path -Path $PSScriptRoot -ChildPath "opensslarm.ps1"
. $openssl

# test

Get-OpenSSL
