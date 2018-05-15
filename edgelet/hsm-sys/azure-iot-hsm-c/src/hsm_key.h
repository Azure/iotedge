#ifndef HSM_KEY_H
#define HSM_KEY_H

#ifdef __cplusplus
#include <cstdbool>
#include <cstddef>
extern "C" {
#else
#include <stdbool.h>
#include <stddef.h>
#endif

#include "azure_c_shared_utility/umock_c_prod.h"
#include "hsm_client_store.h"

static inline int key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_sign(key_handle,
                                              data_to_be_signed,
                                              data_to_be_signed_size,
                                              digest,
                                              digest_size);
}

static inline int key_derive_and_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_derive_and_sign(key_handle,
                                                         data_to_be_signed,
                                                         data_to_be_signed_size,
                                                         identity,
                                                         identity_size,
                                                         digest,
                                                         digest_size);
}


static inline int key_verify(KEY_HANDLE key_handle,
                             const unsigned char* data_to_be_signed,
                             size_t data_to_be_signed_size,
                             const unsigned char* signature_to_verify,
                             size_t signature_to_verify_size,
                             bool* verification_status)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_verify(key_handle,
                                                data_to_be_signed,
                                                data_to_be_signed_size,
                                                signature_to_verify,
                                                signature_to_verify_size,
                                                verification_status);
}

static inline int key_derive_and_verify(KEY_HANDLE key_handle,
                                        const unsigned char* data_to_be_signed,
                                        size_t data_to_be_signed_size,
                                        const unsigned char* identity,
                                        size_t identity_size,
                                        const unsigned char* signature_to_verify,
                                        size_t signature_to_verify_size,
                                        bool* verification_status)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_derive_and_verify(key_handle,
                                                           data_to_be_signed,
                                                           data_to_be_signed_size,
                                                           identity,
                                                           identity_size,
                                                           signature_to_verify,
                                                           signature_to_verify_size,
                                                           verification_status);
}

static inline int key_encrypt(KEY_HANDLE key_handle,
                              const SIZED_BUFFER *identity,
                              const SIZED_BUFFER *plaintext,
                              const SIZED_BUFFER *passphrase,
                              const SIZED_BUFFER *initialization_vector,
                              SIZED_BUFFER *ciphertext)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_encrypt(key_handle,
                                                 identity,
                                                 plaintext,
                                                 passphrase,
                                                 initialization_vector,
                                                 ciphertext);
}

static inline int key_decrypt(KEY_HANDLE key_handle,
                              const SIZED_BUFFER *identity,
                              const SIZED_BUFFER *ciphertext,
                              const SIZED_BUFFER *passphrase,
                              const SIZED_BUFFER *initialization_vector,
                              SIZED_BUFFER *plaintext)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_decrypt(key_handle,
                                                 identity,
                                                 ciphertext,
                                                 passphrase,
                                                 initialization_vector,
                                                 plaintext);
}

MOCKABLE_FUNCTION(, KEY_HANDLE, create_sas_key, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, void, destroy_sas_key, KEY_HANDLE, key_handle);
MOCKABLE_FUNCTION(, int, generate_pki_cert_and_key, CERT_PROPS_HANDLE, cert_props_handle,
                    int, serial_number, const char*, key_file_name, const char*, cert_file_name,
                    const char*, issuer_key_file, const char*, issuer_certificate_file);
MOCKABLE_FUNCTION(, KEY_HANDLE, create_cert_key, const char*, key_file_name);
MOCKABLE_FUNCTION(, void, destroy_cert_key, KEY_HANDLE, key_handle);

#ifdef __cplusplus
}
#endif

#endif  //HSM_KEY_H
