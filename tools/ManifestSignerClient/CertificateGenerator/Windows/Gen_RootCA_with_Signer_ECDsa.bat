REM ECDSA scripts
REM ###############################################################################
REM This script generates test certs and keys for Manifest Trust feature 
REM
REM !!!MUST not be used in production!!!
REM ###############################################################################
@echo off
REM EC algorithm choices : secp521r1 , secp384r1, prime256v1
set ROOT_KEY_ALGO=secp521r1
set SIGNER_KEY_ALGO=prime256v1

REM SHA algorithm choices : SHA256, SHA384, SHA512
set ROOT_SHA_ALGORITHM=-SHA256 
set SIGNER_SHA_ALGORITHM=-SHA256

set ROOT_PRIVATE_KEYNAME=root_ca_private_ecdsa_key.pem
set ROOT_PUBLIC_KEYNAME=root_ca_public_ecdsa_cert.pem

set SIGNER_PRIVATE_KEYNAME=signer_private_ecdsa_key.pem
set SIGNER_PUBLIC_KEYNAME=signer_public_ecdsa_key.pem
set SIGNER_CSR=signer_ecdsa.csr
set SIGNER_CERT=signer_public_ecdsa_cert.pem

openssl ecparam -genkey -name %ROOT_KEY_ALGO% -noout -out %ROOT_PRIVATE_KEYNAME%

openssl req -x509 -key %ROOT_PRIVATE_KEYNAME% -out %ROOT_PUBLIC_KEYNAME% -days 30 %ROOT_SHA_ALGORITHM% -subj "/CN=Manifest Signer Client ECDSA Root CA"

openssl ecparam -genkey -name %SIGNER_KEY_ALGO% -out %SIGNER_PRIVATE_KEYNAME%

openssl req -out %SIGNER_CSR% -key %SIGNER_PRIVATE_KEYNAME% -new -days 30 %SIGNER_SHA_ALGORITHM% -subj "/CN=Manifest Signer Client ECDSA Signer Cert"

openssl x509 -req -days 30 -in %SIGNER_CSR% -CA %ROOT_PUBLIC_KEYNAME% -CAkey %ROOT_PRIVATE_KEYNAME% -CAcreateserial -out %SIGNER_CERT% -sha256
