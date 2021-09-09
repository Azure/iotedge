REM RSA scripts
REM ###############################################################################
REM This script generates test certs and keys for Manifest Trust feature 
REM
REM !!!MUST not be used in production!!!
REM ###############################################################################
@echo off
REM SHA algorithm choices : SHA256, SHA384, SHA512
set ROOT_SHA_ALGORITHM=-SHA256 
set SIGNER_SHA_ALGORITHM=-SHA256

set ROOT_PRIVATE_KEYNAME=root_ca_private_rsa_key.pem
set ROOT_PUBLIC_KEYNAME=root_ca_public_rsa_cert.pem

set SIGNER_PRIVATE_KEYNAME=signer_private_rsa_key.pem
set SIGNER_PUBLIC_KEYNAME=signer_public_rsa_key.pem
set SIGNER_CSR=signer_rsa.csr
set SIGNER_CERT=signer_public_rsa_cert.pem

openssl genrsa -out %ROOT_PRIVATE_KEYNAME% 4096

openssl req -x509 -key %ROOT_PRIVATE_KEYNAME% -out %ROOT_PUBLIC_KEYNAME% -days 5000 %ROOT_SHA_ALGORITHM% -subj "/CN=Manifest Signer Client RSA Root CA"

openssl genrsa -out %SIGNER_PRIVATE_KEYNAME% 2048

openssl req -out %SIGNER_CSR% -key %SIGNER_PRIVATE_KEYNAME% -new -days 365 %SIGNER_SHA_ALGORITHM% -subj "/CN=Manifest Signer Client RSA Signer Cert"

openssl x509 -req -days 365 -in %SIGNER_CSR% -CA %ROOT_PUBLIC_KEYNAME% -CAkey %ROOT_PRIVATE_KEYNAME% -CAcreateserial -out %SIGNER_CERT% -sha256
