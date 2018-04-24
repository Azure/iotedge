#ifndef HSM_CLIENT_STORE_H
#define HSM_CLIENT_STORE_H

#ifdef __cplusplus
#include <cstdbool>
#include <cstddef>
#include <cstdlib>
extern "C" {
#else
#include <stdbool.h>
#include <stddef.h>
#include <stdlib.h>
#endif /* __cplusplus */

#include "hsm_client_data.h"
typedef void* KEY_HANDLE;

typedef int (*HSM_KEY_SIGN)(KEY_HANDLE key_handle,
                            const unsigned char* data_to_be_signed,
                            size_t data_to_be_signed_size,
                            unsigned char** digest,
                            size_t* digest_size);

typedef int (*HSM_KEY_DERIVE_AND_SIGN)(KEY_HANDLE key_handle,
                                       const unsigned char* data_to_be_signed,
                                       size_t data_to_be_signed_size,
                                       const unsigned char* identity,
                                       size_t identity_size,
                                       unsigned char** digest,
                                       size_t* digest_size);

typedef int (*HSM_KEY_VERIFY)(KEY_HANDLE key_handle,
                              const unsigned char* data_to_be_signed,
                              size_t data_to_be_signed_size,
                              const unsigned char* signature_to_verify,
                              size_t signature_to_verify_size,
                              bool* verification_status);

typedef int (*HSM_KEY_DERIVE_AND_VERIFY)(KEY_HANDLE key_handle,
                                         const unsigned char* data_to_be_signed,
                                         size_t data_to_be_signed_size,
                                         const unsigned char* identity,
                                         size_t identity_size,
                                         const unsigned char* signature_to_verify,
                                         size_t signature_to_verify_size,
                                         bool* verification_status);

typedef int (*HSM_KEY_ENCRYPT)(KEY_HANDLE key_handle,
                               const SIZED_BUFFER *identity,
                               const SIZED_BUFFER *plaintext,
                               const SIZED_BUFFER *passphrase,
                               const SIZED_BUFFER *initialization_vector,
                               SIZED_BUFFER *ciphertext);

typedef int (*HSM_KEY_DECRYPT)(KEY_HANDLE key_handle,
                               const SIZED_BUFFER *identity,
                               const SIZED_BUFFER *ciphertext,
                               const SIZED_BUFFER *passphrase,
                               const SIZED_BUFFER *initialization_vector,
                               SIZED_BUFFER *plaintext);

struct HSM_CLIENT_KEY_INTERFACE_TAG
{
    HSM_KEY_SIGN hsm_client_key_sign;
    HSM_KEY_DERIVE_AND_SIGN hsm_client_key_derive_and_sign;
    HSM_KEY_VERIFY hsm_client_key_verify;
    HSM_KEY_DERIVE_AND_VERIFY hsm_client_key_derive_and_verify;
    HSM_KEY_ENCRYPT hsm_client_key_encrypt;
    HSM_KEY_DECRYPT hsm_client_key_decrypt;
};
typedef struct HSM_CLIENT_KEY_INTERFACE_TAG HSM_CLIENT_KEY_INTERFACE;
const HSM_CLIENT_KEY_INTERFACE* hsm_client_key_interface(void);

typedef void* HSM_CLIENT_STORE_HANDLE;
typedef int (*HSM_CLIENT_STORE_CREATE)(const char* store_name);
typedef int (*HSM_CLIENT_STORE_DESTROY)(const char* store_name);
typedef HSM_CLIENT_STORE_HANDLE (*HSM_CLIENT_STORE_OPEN)(const char* store_name);
typedef int (*HSM_CLIENT_STORE_CLOSE)(HSM_CLIENT_STORE_HANDLE handle);
typedef int (*HSM_CLIENT_STORE_REMOVE_KEY)(HSM_CLIENT_STORE_HANDLE handle, const char* key_name);
typedef KEY_HANDLE (*HSM_CLIENT_STORE_OPEN_KEY)(HSM_CLIENT_STORE_HANDLE handle, const char* key_name);
typedef int (*HSM_CLIENT_STORE_CLOSE_KEY)(HSM_CLIENT_STORE_HANDLE handle, KEY_HANDLE key_handle);
typedef int (*HSM_CLIENT_STORE_INSERT_SAS_KEY)(HSM_CLIENT_STORE_HANDLE handle,
                                               const char* key_name,
                                               const unsigned char* key,
                                               size_t key_len);
typedef int (*HSM_CLIENT_STORE_INSERT_ENCRYPTION_KEY)(HSM_CLIENT_STORE_HANDLE handle, const char* key_name);

struct HSM_CLIENT_STORE_INTERFACE_TAG {
    HSM_CLIENT_STORE_CREATE hsm_client_store_create;
    HSM_CLIENT_STORE_DESTROY hsm_client_store_destroy;
    HSM_CLIENT_STORE_OPEN hsm_client_store_open;
    HSM_CLIENT_STORE_CLOSE hsm_client_store_close;
    HSM_CLIENT_STORE_OPEN_KEY hsm_client_store_open_key;
    HSM_CLIENT_STORE_CLOSE_KEY hsm_client_store_close_key;
    HSM_CLIENT_STORE_REMOVE_KEY hsm_client_store_remove_key;
    HSM_CLIENT_STORE_INSERT_SAS_KEY hsm_client_store_insert_sas_key;
    HSM_CLIENT_STORE_INSERT_ENCRYPTION_KEY hsm_client_store_insert_encryption_key;
};
typedef struct HSM_CLIENT_STORE_INTERFACE_TAG HSM_CLIENT_STORE_INTERFACE;
const HSM_CLIENT_STORE_INTERFACE* hsm_client_store_interface(void);

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif //HSM_CLIENT_STORE_H
