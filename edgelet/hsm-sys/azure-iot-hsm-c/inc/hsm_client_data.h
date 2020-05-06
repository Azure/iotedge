// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef HSM_CLIENT_DATA_H
#define HSM_CLIENT_DATA_H

#ifdef __cplusplus
#include <cstddef>
#include <cstdlib>
extern "C" {
#else
#include <stddef.h>
#include <stdlib.h>
#endif /* __cplusplus */

#include "hsm_certificate_props.h"
#include "certificate_info.h"

/** @file */

#define AZURE_IOT_HSM_VERSION "1.0.3"

typedef void* HSM_CLIENT_HANDLE;

/**
 * An allocated buffer and its associated size. If this struct is created by the caller but
 * populated by one of the HSM functions, the 'buffer' member should be passed to
 * ::HSM_CLIENT_FREE_BUFFER when it is no longer needed.
 */
typedef struct SIZED_BUFFER_TAG
{
    unsigned char* buffer;
    size_t size;
} SIZED_BUFFER;

/**
 * @brief   Creates a client for the associated interface
 *
 * @return  An instance handle that is passed into most functions of the interface.
 */
typedef HSM_CLIENT_HANDLE (*HSM_CLIENT_CREATE)();

/**
 * @brief   Releases a client instance created with ::HSM_CLIENT_CREATE
 */
typedef void (*HSM_CLIENT_DESTROY)(HSM_CLIENT_HANDLE handle);

/**
* @brief    Frees buffers allocated by the HSM library.
*           Used to ensure that the buffers allocated in one CRT are freed in the same
*           CRT. Intended to be used for TPM keys, x509 buffers, and ::SIZED_BUFFER_TAG output.
*
* @param buffer     A buffer allocated and owned by HSM library.
*
*/
typedef void (*HSM_CLIENT_FREE_BUFFER)(void * buffer);

// TPM
/**
* @brief            Imports a key that has been previously encrypted with the endorsement
*                   key and storage root key into the TPM key storage
*
* @param handle     The ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param key        The key that needs to be imported to the TPM
* @param key_size   The size of the key
*
* @return           On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_ACTIVATE_IDENTITY_KEY)(HSM_CLIENT_HANDLE handle, const unsigned char* key, size_t key_size);

/**
* @brief                Retrieves the endorsement key of the TPM
*
* @param handle         The ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param[out] key       The returned endorsement key. This function allocates memory for a buffer
*                       which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] key_size  The size of the returned key
*
* @return               On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_GET_ENDORSEMENT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char** key, size_t* key_size);

/**
* @brief                Retrieves the storage root key of the TPM
*
* @param handle         The ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param[out] key       The returned storage root key. This function allocates memory for a buffer
*                       which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] key_size  The size of the returned key
*
* @return               On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_GET_STORAGE_ROOT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char** key, size_t* key_size);

/**
* @brief                    Hashes the data with the key stored in the TPM
*
* @param handle             ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param data               Data that will need to be hashed
* @param data_size          The size of the data parameter
* @param[out] digest        The returned digest. This function allocates memory for a buffer
*                           which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] digest_size   The size of the returned digest
*
* @return                   On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_SIGN_WITH_IDENTITY)(HSM_CLIENT_HANDLE handle, const unsigned char* data, size_t data_size, unsigned char** digest, size_t* digest_size);

/**
* @brief                    Hashes the data with the device private key stored in the HSM
*
* @param handle             ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param data               Data that will need to be hashed
* @param data_size          The size of the data parameter
* @param[out] digest        The returned digest. This function allocates memory for a buffer
*                           which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] digest_size   The size of the returned digest
*
* @return                   On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_SIGN_WITH_PRIVATE_KEY)(HSM_CLIENT_HANDLE handle, const unsigned char* data, size_t data_size, unsigned char** digest, size_t* digest_size);

/**
* @brief    Derives the SAS key and uses it to sign the data. The key
*           should never leave the HSM.
*
* @param handle             A valid HSM client handle
* @param data               Data to be signed
* @param data_size          The size of the data to be signed
* @param identity           Identity to be used to derive the SAS key
* @param[out] digest        The returned digest. This function allocates memory for a buffer
*                           which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] digest_size   The size of the returned digest
*
* @note If digest is NULL the API will return the size of the required
* buffer to hold the digest contents.
*
* @return   Zero on success. Non-zero on failure
*/
typedef int (*HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY)(HSM_CLIENT_HANDLE handle, const unsigned char* data, size_t data_size, const unsigned char* identity, size_t identity_size, unsigned char** digest, size_t* digest_size);

// x509
/**
* @brief        Retrieves the certificate to be used for x509 communication. This value is
*               sent unmodified to the tlsio layer as a set_options of OPTION_X509_ECC_CERT.
*
* @param handle A valid HSM client handle
*
* @return       On success the value of a certificate. NULL on failure
*/
typedef char* (*HSM_CLIENT_GET_CERTIFICATE)(HSM_CLIENT_HANDLE handle);

/**
* @brief    Retrieves the alias key from the x509 certificate. This value is sent unmodified
*           to the tlsio layer as a set_options of OPTION_X509_ECC_KEY.
*
* @param handle A valid HSM client handle
*
* @return       On success the value representing the certificate key. NULL on failure
*/
typedef char* (*HSM_CLIENT_GET_CERT_KEY)(HSM_CLIENT_HANDLE handle);

/**
* @brief    Retrieves the common name from the x509 certificate. Passed to the
*           Device Provisioning Service as a registration Id.
*
* @param handle A valid HSM client handle
*
* @return       On success the value of the common name. NULL on failure
*/
typedef char* (*HSM_CLIENT_GET_COMMON_NAME)(HSM_CLIENT_HANDLE handle);

/**
* @brief    Retrieves the certificate info handle which can be used for X509 communication.
*           This serves as the IoT device's identity.
*
* @param handle A valid HSM client handle
*
* @return       On success a valid CERT_INFO_HANDLE. NULL on failure.
*/
typedef CERT_INFO_HANDLE (*HSM_CLIENT_GET_CERTIFICATE_INFO)(HSM_CLIENT_HANDLE handle);

// Cryptographic utilities not tied to a specific hardware implementation
/**
* @brief        Provisions a master symmetric encryption key in the HSM. This key will
*               be used to derive all the module and IoT Edge runtime-specific encryption
*               keys. This is expected to be called once at provisioning.
*
* @param handle A valid HSM client handle
*
* @return       Zero on success, nonzero otherwise
*/
typedef int (*HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY)(HSM_CLIENT_HANDLE handle);

/**
* @brief        Removes the master encryption key from the HSM. This is expected
*               to be called once during de-provisioning of the device.
*
* @note         Once this is erased, all encrypted data is lost.
*
* @param handle A valid HSM client handle
*
* @return       Zero on success, nonzero otherwise
*/
typedef int (*HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY)(HSM_CLIENT_HANDLE handle);

/**
* @brief    Returns a series of random bytes.
*
* @param    handle      A valid HSM client handle
* @param    buffer      Buffer used to store the random data
* @param    num         The number of the bytes to be stored in buffer
*
* @return   Zero on success, nonzero otherwise
*/
typedef int (*HSM_CLIENT_GET_RANDOM_BYTES)(HSM_CLIENT_HANDLE handle, unsigned char* buffer, size_t num);

/**
* @brief    Generates an X.509 certificate and private key pair using the supplied
*           certificate properties. Any CA certificates are expected to be issued by
*           the Device CA. Other certificates may be issued by any intermediate CA
*           certs or the device CA certificate.
*
* @param handle       A valid HSM client handle
* @param cert_props   Handle to certificate properties
*
* @return CERT_INFO_HANDLE -- Valid non NULL handle on success, NULL on error
*/
typedef CERT_INFO_HANDLE (*HSM_CLIENT_CREATE_CERTIFICATE)(HSM_CLIENT_HANDLE handle, CERT_PROPS_HANDLE certificate_props);

/**
* @brief    Obtains an X.509 certificate info handle fir the supplied alias.
*           If the alias is invalid or does not exists a NULL is returned.
*
* @param handle       A valid HSM client handle
* @param alias        The alias to certificate and private key to
*
* @return CERT_INFO_HANDLE -- Valid non NULL handle on success, NULL on error
*/
typedef CERT_INFO_HANDLE (*HSM_CLIENT_CRYPTO_GET_CERTIFICATE)(HSM_CLIENT_HANDLE handle, const char *alias);

/**
* @brief    Deletes any crypto assets associated with the handle
*           returned by ::HSM_CLIENT_CREATE_CERTIFICATE.
*
* @param handle     A valid HSM client handle
* @param alias      The alias given to the certificate bundle in the properties
*
*/
typedef void (*HSM_CLIENT_DESTROY_CERTIFICATE)(HSM_CLIENT_HANDLE handle, const char* alias);

/**
* @brief    Encrypts a blob of plaintext data and returns its corresponding cipher text.
*
* @param handle             A valid HSM client handle
* @param client_id          Module or client identity string used in key generation
* @param plaintext          Plaintext payload to encrypt
* @param init_vector        Initialization vector used for any CBC cipher
* @param[out] ciphertext    The returned cipher. This function allocates memory for a buffer
*                           which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
*
* @note The encryption/decryption algorithm ex. AES128CBC is not specified
* via this API and is left up to implementors.
*
* @return   Zero on success, nonzero otherwise
*/
typedef int (*HSM_CLIENT_ENCRYPT_DATA)(HSM_CLIENT_HANDLE handle, const SIZED_BUFFER* identity, const SIZED_BUFFER* plaintext, const SIZED_BUFFER* init_vector, SIZED_BUFFER* ciphertext);


/**
* @brief    Decrypts a blob of cipher text data and returns its corresponding plaintext.
*
* @param handle         A valid HSM client handle
* @param client_id      Module or client identity string used in key generation
* @param ciphertext     Cipher text payload to decrypt
* @param init_vector    Initialization vector used for any CBC cipher
* @param[out] plaintext Returned plaintext. This function allocates memory for a buffer
*                       which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
*
* @note The encryption/decryption algorithm ex. AES128CBC is not specified
* via this API and is left up to OEM/HSM implementors.
*
* @return   Zero on success, nonzero otherwise
*/
typedef int (*HSM_CLIENT_DECRYPT_DATA)(HSM_CLIENT_HANDLE handle, const SIZED_BUFFER* identity, const SIZED_BUFFER* ciphertext, const SIZED_BUFFER* init_vector, SIZED_BUFFER* plaintext);


/**
* @brief                    Hashes the data with the device private key stored in the HSM
*
* @param handle             ::HSM_CLIENT_HANDLE that was created by the ::HSM_CLIENT_CREATE call
* @param alias              Private key associated with the alias used to sign the data
* @param data               Data that will need to be hashed
* @param data_size          The size of the data parameter
* @param[out] digest        The returned digest. This function allocates memory for a buffer
*                           which must be freed by a call to ::HSM_CLIENT_FREE_BUFFER.
* @param[out] digest_size   The size of the returned digest
*
* @return                   On success 0 on. Non-zero on failure
*/
typedef int (*HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY)(HSM_CLIENT_HANDLE handle, const char* alias, const unsigned char* data, size_t data_size, unsigned char** digest, size_t* digest_size);

/**
* @brief    Retrieves the trusted certificate bundle used to authenticate the server.
*
* @param handle       A valid HSM client handle
* @param cert_props   Handle to certificate properties
*
* @return CERT_INFO_HANDLE -- Valid non NULL handle on success, NULL on error
*/
typedef CERT_INFO_HANDLE (*HSM_CLIENT_GET_TRUST_BUNDLE)(HSM_CLIENT_HANDLE handle);

typedef struct HSM_CLIENT_TPM_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_tpm_create;
    HSM_CLIENT_DESTROY hsm_client_tpm_destroy;

    HSM_CLIENT_ACTIVATE_IDENTITY_KEY hsm_client_activate_identity_key;
    HSM_CLIENT_GET_ENDORSEMENT_KEY hsm_client_get_ek;
    HSM_CLIENT_GET_STORAGE_ROOT_KEY hsm_client_get_srk;
    HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity;
    HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY hsm_client_derive_and_sign_with_identity;
    HSM_CLIENT_FREE_BUFFER hsm_client_free_buffer;
} HSM_CLIENT_TPM_INTERFACE;

typedef struct HSM_CLIENT_X509_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_x509_create;
    HSM_CLIENT_DESTROY hsm_client_x509_destroy;

    HSM_CLIENT_GET_CERTIFICATE hsm_client_get_cert;
    HSM_CLIENT_GET_CERT_KEY hsm_client_get_key;
    HSM_CLIENT_GET_COMMON_NAME hsm_client_get_common_name;
    HSM_CLIENT_FREE_BUFFER hsm_client_free_buffer;
    HSM_CLIENT_SIGN_WITH_PRIVATE_KEY hsm_client_sign_with_private_key;
    HSM_CLIENT_GET_CERTIFICATE_INFO hsm_client_get_cert_info;
} HSM_CLIENT_X509_INTERFACE;

typedef struct HSM_CLIENT_CRYPTO_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_crypto_create;
    HSM_CLIENT_DESTROY hsm_client_crypto_destroy;

    HSM_CLIENT_GET_RANDOM_BYTES hsm_client_get_random_bytes;
    HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY hsm_client_create_master_encryption_key;
    HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY hsm_client_destroy_master_encryption_key;
    HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate;
    HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate;
    HSM_CLIENT_ENCRYPT_DATA hsm_client_encrypt_data;
    HSM_CLIENT_DECRYPT_DATA hsm_client_decrypt_data;
    HSM_CLIENT_GET_TRUST_BUNDLE hsm_client_get_trust_bundle;
    HSM_CLIENT_FREE_BUFFER hsm_client_free_buffer;
    HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY hsm_client_crypto_sign_with_private_key;
    HSM_CLIENT_CRYPTO_GET_CERTIFICATE hsm_client_crypto_get_certificate;
} HSM_CLIENT_CRYPTO_INTERFACE;

extern const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_interface();
extern const HSM_CLIENT_X509_INTERFACE* hsm_client_x509_interface();
extern const HSM_CLIENT_CRYPTO_INTERFACE* hsm_client_crypto_interface();

extern int hsm_client_x509_init(uint64_t);
extern void hsm_client_x509_deinit();
extern int hsm_client_tpm_init();
extern void hsm_client_tpm_deinit();
extern int hsm_client_crypto_init(uint64_t);
extern void hsm_client_crypto_deinit();
extern const char* hsm_get_device_ca_alias(void);
extern const char* hsm_get_version(void);

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif // HSM_CLIENT_DATA_H
