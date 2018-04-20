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

typedef void *HSM_CLIENT_HANDLE;
struct SIZED_BUFFER_TAG
{
    unsigned char *buffer;
    size_t size;
};
typedef struct SIZED_BUFFER_TAG SIZED_BUFFER;

typedef HSM_CLIENT_HANDLE (*HSM_CLIENT_CREATE)();
typedef void (*HSM_CLIENT_DESTROY)(HSM_CLIENT_HANDLE handle);

// TPM
typedef int (*HSM_CLIENT_ACTIVATE_IDENTITY_KEY)(HSM_CLIENT_HANDLE handle, const unsigned char *key, size_t key_len);
typedef int (*HSM_CLIENT_GET_ENDORSEMENT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char **key, size_t *key_len);
typedef int (*HSM_CLIENT_GET_STORAGE_ROOT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char **key, size_t *key_len);
typedef int (*HSM_CLIENT_SIGN_WITH_IDENTITY)(HSM_CLIENT_HANDLE handle, const unsigned char *data, size_t data_len, unsigned char **key, size_t *key_len);
/**
    API to derive the SAS key and use it to sign the data. The key
    should never leave the HSM.

    handle[in] -- A valid HSM client handle
    data_to_be_signed[in] -- Data to be signed
    data_to_be_signed_len[in] -- Length of the data to be signed
    identity[in] -- Identity to be used to derive the SAS key
    identity_size[in] -- Size of the identity buffer
    digest[out]  -- Pointer to a buffer to be filled with the signed digest
    digest_size[out]  -- Length of signed digest

    @note: If digest is NULL the API will return the size of the required
    buffer to hold the digest contents.

    Return
      0  -- On success
      Non 0 -- otherwise
*/
typedef int (*HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY)(HSM_CLIENT_HANDLE handle, const unsigned char *data_to_be_signed, size_t data_to_be_signed_len, const unsigned char *identity, size_t identity_size, unsigned char **digest, size_t *digest_size);

// x509
typedef char *(*HSM_CLIENT_GET_CERTIFICATE)(HSM_CLIENT_HANDLE handle);
typedef char *(*HSM_CLIENT_GET_CERT_KEY)(HSM_CLIENT_HANDLE handle);
typedef char *(*HSM_CLIENT_GET_COMMON_NAME)(HSM_CLIENT_HANDLE handle);

/**
   API to free buffers allocated by the HSM library.
   Used to ensure that the buffers allocated in one CRT are freed in the same
   CRT. Intended to be used for TPM keys, x509 buffers, and SIZED_BUFFER output.

   buffer[in] -- a buffer allocated and owned by HSM library.

   No return value.
*/
typedef void (*HSM_CLIENT_FREE_BUFFER)(void *buffer);

/**
    API to return the limits of a random number generated from HSM hardware.
    The API to return a random number is HSM_CLIENT_GET_RANDOM_NUMBER. The
    number is expected to be a number between a min and max both inclusive.

    handle[in] -- A valid HSM client handle
    min_random_num[out] -- Min random number limit will be returned via this parameter
    max_random_num[out] -- Max random number limit will be returned via this parameter

    Return
      0  -- On success
      Non 0 -- otherwise
*/
typedef int (*HSM_CLIENT_GET_RANDOM_NUMBER_LIMITS)(HSM_CLIENT_HANDLE handle, size_t *min_random_num, size_t *max_random_num);

/**
    API to return a random number generated from HSM hardware. The number
    is expected to be between the random number limits returned by API
    HSM_CLIENT_GET_RANDOM_NUMBER_LIMITS.

    handle[in] -- A valid HSM client handle
    random_num[out] -- Random number will be returned via this parameter

    Return
      0  -- On success
      Non 0 -- otherwise
*/
typedef int (*HSM_CLIENT_GET_RANDOM_NUMBER)(HSM_CLIENT_HANDLE handle, size_t *random_num);

/**
    API to provision a master symmetric encryption key in the HSM.
    This key will be used to derive all the module and edge runtime
    specific encryption keys. This is expected to be called once
    at provisioning.

    handle[in] -- A valid HSM client handle

    Return
      0  -- On success
      Non 0 -- otherwise
*/
typedef int (*HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY)(HSM_CLIENT_HANDLE handle);

/**
    API to remove the master encryption key from the HSM. This is expected
    to be called once during de-provisioning of the Edge device.

    @note: Once this is erased, all encrypted data is lost.

    handle[in] -- A valid HSM client handle

    Return
      0  -- On success
      Non 0 -- otherwise
*/
typedef int (*HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY)(HSM_CLIENT_HANDLE handle);

/**
    API to encrypt a blob of plaintext data and return its corresponding
    cipher text.

    handle[in]       -- A valid HSM client handle
    client_id[in]    -- Module or client identity string used in key generation
    plaintext[in]    -- Plaintext payload to encrypt
    passphrase[in]   -- Optional passphrase "secret" used to encrypt the
                        plaintext. NULL if no passphrase is desired.
    initialization_vector[in] -- Initialization vector used for any CBC cipher
    ciphertext[out]  -- Encrypted cipher text

    @note: The encryption/decryption algorithm ex. AES128CBC is not specified
    via this API and is left up to OEM/HSM implementors.

    Return
        0 - Success
        Non 0 otherwise
*/
typedef int (*HSM_CLIENT_ENCRYPT_DATA)(HSM_CLIENT_HANDLE handle,
                                       const SIZED_BUFFER *client_id,
                                       const SIZED_BUFFER *plaintext,
                                       const SIZED_BUFFER *passphrase,
                                       const SIZED_BUFFER *initialization_vector,
                                       SIZED_BUFFER *ciphertext);

/**
    API to decrypt a blob of cipher text data and return its corresponding
    plain text.

    handle[in]      -- A valid HSM client handle
    client_id[in]   -- Module or client identity string used in key generation
    ciphertext[in]  -- Cipher text payload to decrypt
    passphrase[in]  -- Optional passphrase "secret" used to encrypt the
                       plaintext. NULL if no passphrase is desired.
    initialization_vector[in] -- Initialization vector used for any CBC cipher
    plaintext[out]  -- Decrypted plain text

    @note: The encryption/decryption algorithm ex. AES128CBC is not specified
    via this API and is left up to OEM/HSM implementors.

    Return
        0 - Success
        Non 0 otherwise
*/
typedef int (*HSM_CLIENT_DECRYPT_DATA)(HSM_CLIENT_HANDLE handle,
                                       const SIZED_BUFFER *client_id,
                                       const SIZED_BUFFER *ciphertext,
                                       const SIZED_BUFFER *passphrase,
                                       const SIZED_BUFFER *initialization_vector,
                                       SIZED_BUFFER *plaintext);

enum CRYPTO_ENCODING_TAG
{
    ASCII = 0,
    PEM = 1,
    DER = 2
};
typedef enum CRYPTO_ENCODING_TAG CRYPTO_ENCODING;

enum PRIVATE_KEY_TYPE_TAG
{
    PRIVATE_KEY_TYPE_UNKNOWN = 0,
    PRIVATE_KEY_TYPE_PAYLOAD,
    PRIVATE_KEY_TYPE_REFERENCE
};
typedef enum PRIVATE_KEY_TYPE_TAG PRIVATE_KEY_TYPE;

enum CERTIFICATE_TYPE_TAG
{
    CERTIFICATE_TYPE_UNKNOWN = 0,
    CERTIFICATE_TYPE_CLIENT,
    CERTIFICATE_TYPE_SERVER,
    CERTIFICATE_TYPE_CA
};
typedef enum CERTIFICATE_TYPE_TAG CERTIFICATE_TYPE;

typedef struct HSM_CERTIFICATE_PROPS_TAG *CERT_PROPS_HANDLE;
typedef struct HSM_CERTIFICATE_TAG *CERT_HANDLE;

extern CERT_PROPS_HANDLE create_certificate_props(void);
extern void destroy_certificate_props(CERT_PROPS_HANDLE handle);
extern int set_validity_in_mins(CERT_PROPS_HANDLE handle, size_t validity_mins);
extern int get_validity_in_mins(CERT_PROPS_HANDLE handle, size_t *p_validity_mins);
extern int set_common_name(CERT_PROPS_HANDLE handle, const char *common_name);
extern int get_common_name(CERT_PROPS_HANDLE handle, char *common_name, size_t common_name_size);
extern int set_certificate_type(CERT_PROPS_HANDLE handle, CERTIFICATE_TYPE type);
extern int get_certificate_type(CERT_PROPS_HANDLE handle, CERTIFICATE_TYPE *p_type);
extern int set_issuer_alias(CERT_PROPS_HANDLE handle, const char *issuer_alias);
extern int get_issuer_alias(CERT_PROPS_HANDLE handle, char *issuer_alias, size_t alias_size);
extern int set_alias(CERT_PROPS_HANDLE handle, const char *alias);
extern int get_alias(CERT_PROPS_HANDLE handle, char *alias, size_t alias_size);

/**
    API generates a X.509 certificate and private key pair using the supplied
    certificate properties. Any CA certificates are expected to by issued by
    the Device CA. Other certificates may be issued by any intermediate CA
    certs or the device CA certificate.

    @note: Specifying the type of public-private key (ex. RSA, ECC etc.)
    to be used to generate the certificate is not specified via this API.
    This works because the key type should be similar to the one used to
    create the Device CA certificate. The key types must be
    the same so that the generated certificate could be used for TLS based
    communication. Since details of the Device CA are not publicly available
    outside of the HSM/crypto layer there is no need to specify the key type.

    @note: Either the private key contents itself could be returned or a
    reference to the private key. This can be controlled by setting
    "exportable_keys" as "true". If this is unspecified keys will not be
    exported.

    handle[in] -- A valid HSM client handle
    certificate_props[in]   -- Handle to certificate properties

    Sample code
        CERT_PROPS_HANDLE props_handle = create_certificate_props();
        set_validity_in_mins(props_handle, 120);
        // this could be "$edgeHub", "<Hostname>", "$edgeAgent", Module ID
        set_common_name(props_handle, common_name);
        // "server", "client", "ca"
        set_certificate_type(props_handle, cert_type);
        // this should be HSM alias of issuer. Ex. "device ca"
        set_issuer_alias(props_handle, issuer_alias);
        HSM alias of issuer. Ex. "device ca"
        Unique alias similar to a file name to associate a reference to HSM resources
        set_alias(props_handle, unique_id);

        CERT_HANDLE h = hsm_create_certificate(hsm_handle, props_handle);
        destroy_certificate_props(props_handle);

    Return
      CERT_HANDLE -- Valid non NULL handle on success
      NULL -- otherwise
*/
typedef CERT_HANDLE (*HSM_CLIENT_CREATE_CERTIFICATE)(HSM_CLIENT_HANDLE handle, CERT_PROPS_HANDLE certificate_props);

/**
    This API deletes any crypto assets associated with the handle
    returned by hsm_create_certificate API.

    handle[in]   -- Valid handle to certificate resources
    cert_handle[in]   -- Valid handle to certificate resources
*/
typedef void (*HSM_CLIENT_DESTROY_CERTIFICATE)(HSM_CLIENT_HANDLE handle, CERT_HANDLE cert_handle);

/**
    This API deletes any crypto assets associated with the id.

    handle[in]   -- Valid handle to certificate resources
*/
typedef void (*HSM_CLIENT_DESTROY_CERTIFICATE_BY_ID)(const char *id);

/**
    This API releases any memory associated with the handle
    returned by hsm_create_certificate API. Certificate files and keys
    are not destroyed using this call.

    handle[in]   -- Valid handle to certificate resources
*/
void (*HSM_CLIENT_CLEAR_CERTIFICATE_HANDLE)(CERT_HANDLE handle);

/**
    Obtain certificate associated with the supplied CERT_HANDLE.

    handle[in]   -- Valid handle to certificate
    cert_buffer[out]  -- Return parameter containing the cert buffer and size
    enc[out]     -- Return parameter containing the encoding of the buffer

    Return
      0  -- On success
      Non 0 -- otherwise
*/
extern int get_certificate(CERT_HANDLE handle,
                           SIZED_BUFFER *cert_buffer,
                           CRYPTO_ENCODING *enc);

/**
    Obtain certificate chain associated with the supplied CERT_HANDLE.
    Ex. [Owner CA -> (intermediate certs)* -> Device CA]

    handle[in]   -- Valid handle to certificate
    cert_buffer[out]  -- Return parameter containing the chain buffer and size
    enc[out]     -- Return parameter containing the encoding of the buffer

    Return
      0  -- On success
      Non 0 -- otherwise
*/
extern int get_certificate_chain(CERT_HANDLE handle,
                                 SIZED_BUFFER *cert_buffer,
                                 CRYPTO_ENCODING *enc);

/**
    Obtain public key associated with the supplied CERT_HANDLE.

    handle[in]   -- Valid handle to certificate
    key_buffer[out]  -- Return parameter containing the key buffer and size
    enc[out]     -- Return parameter containing the encoding of the buffer

    Return
      0  -- On success
      Non 0 -- otherwise
*/
extern int get_public_key(CERT_HANDLE handle,
                          SIZED_BUFFER *key_buffer,
                          CRYPTO_ENCODING *enc);

/**
    Obtain private key or reference associated with the supplied CERT_HANDLE.

    handle[in] -- Valid handle to certificate
    key_buffer[out]  -- Return parameter containing the key buffer and size
    enc[out]   -- Return parameter containing the encoding of the buffer
    type[out]  -- Private key type reference or actual payload
                  will be returned via this parameter

    @note: Private key returned by reference will be encoded as
           ASCII and will be null terminated.

    Return
      0  -- On success
      Non 0 -- otherwise
*/
extern int get_private_key(CERT_HANDLE handle,
                           SIZED_BUFFER *key_buffer,
                           PRIVATE_KEY_TYPE *type,
                           CRYPTO_ENCODING *enc);

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
} HSM_CLIENT_X509_INTERFACE;

typedef struct HSM_CLIENT_CRYPTO_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_crypto_create;
    HSM_CLIENT_DESTROY hsm_client_crypto_destroy;

    HSM_CLIENT_GET_RANDOM_NUMBER_LIMITS hsm_client_get_random_number_limits;
    HSM_CLIENT_GET_RANDOM_NUMBER hsm_client_get_random_number;
    HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY hsm_client_create_master_encryption_key;
    HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY hsm_client_destroy_master_encryption_key;
    HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate;
    HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate;
    HSM_CLIENT_ENCRYPT_DATA hsm_client_encrypt_data;
    HSM_CLIENT_DECRYPT_DATA hsm_client_decrypt_data;
    HSM_CLIENT_FREE_BUFFER hsm_client_free_buffer;

} HSM_CLIENT_CRYPTO_INTERFACE;

extern const HSM_CLIENT_TPM_INTERFACE *hsm_client_tpm_interface();
extern const HSM_CLIENT_X509_INTERFACE *hsm_client_x509_interface();
extern const HSM_CLIENT_CRYPTO_INTERFACE *hsm_client_crypto_interface();

extern int hsm_client_x509_init();
extern void hsm_client_x509_deinit();
extern int hsm_client_tpm_init();
extern void hsm_client_tpm_deinit();
extern int hsm_client_crypto_init();
extern void hsm_client_crypto_deinit();

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif // HSM_CLIENT_DATA_H