#!/bin/bash
###############################################################################
# This script generates test certs and keys for Manifest Trust feature.
#
# MUST not be used in production
###############################################################################

# ECDSA scripts
# EC algorithm choices : secp521r1 , secp384r1, prime256v1

set -e
ROOT_KEY_ALGO="secp521r1"
SIGNER_KEY_ALGO="prime256v1"

# SHA algorithm choices : SHA256, SHA384, SHA512
ROOT_SHA_ALGORITHM="-SHA256 "
SIGNER_SHA_ALGORITHM="-SHA256"

ROOT_PRIVATE_KEYNAME="root_ca_private_ecdsa_key.pem"
ROOT_PUBLIC_KEYNAME="root_ca_public_ecdsa_cert.pem"

SIGNER_PRIVATE_KEYNAME="signer_private_ecdsa_key.pem"
SIGNER_PUBLIC_KEYNAME="signer_public_ecdsa_key.pem"
SIGNER_CSR="signer_ecdsa.csr"
SIGNER_CERT="signer_public_ecdsa_cert.pem"

openssl ecparam -genkey -name ${ROOT_KEY_ALGO} -noout -out ${ROOT_PRIVATE_KEYNAME}

openssl req -x509 -key ${ROOT_PRIVATE_KEYNAME} -out ${ROOT_PUBLIC_KEYNAME} -days 30 ${ROOT_SHA_ALGORITHM} -subj "/CN=Manifest Signer Client ECDSA Root CA"

openssl ecparam -genkey -name ${SIGNER_KEY_ALGO} -out ${SIGNER_PRIVATE_KEYNAME}

openssl req -out ${SIGNER_CSR} -key ${SIGNER_PRIVATE_KEYNAME} -new -days 30 ${SIGNER_SHA_ALGORITHM} -subj "/CN=Manifest Signer Client ECDSA Signer Cert"

openssl x509 -req -days 30 -in ${SIGNER_CSR} -CA ${ROOT_PUBLIC_KEYNAME} -CAkey ${ROOT_PRIVATE_KEYNAME} -CAcreateserial -out ${SIGNER_CERT} -sha256
