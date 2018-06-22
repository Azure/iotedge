#ifndef HSM_KEY_INTERFACE_H
#define HSM_KEY_INTERFACE_H

#include "hsm_client_data.h"

typedef void* KEY_HANDLE;

enum HSM_KEY_TAG_T
{
    HSM_KEY_UNKNOWN = 0,
    HSM_KEY_SAS,
    HSM_KEY_ENCRYPTION
};
typedef enum HSM_KEY_TAG_T HSM_KEY_T;

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

typedef int (*HSM_KEY_ENCRYPT)(KEY_HANDLE key_handle,
                               const SIZED_BUFFER *identity,
                               const SIZED_BUFFER *plaintext,
                               const SIZED_BUFFER *initialization_vector,
                               SIZED_BUFFER *ciphertext);

typedef int (*HSM_KEY_DECRYPT)(KEY_HANDLE key_handle,
                               const SIZED_BUFFER *identity,
                               const SIZED_BUFFER *ciphertext,
                               const SIZED_BUFFER *initialization_vector,
                               SIZED_BUFFER *plaintext);

typedef void (*HSM_KEY_DESTROY)(KEY_HANDLE key_handle);

struct HSM_CLIENT_KEY_INTERFACE_TAG
{
    HSM_KEY_SIGN hsm_client_key_sign;
    HSM_KEY_DERIVE_AND_SIGN hsm_client_key_derive_and_sign;
    HSM_KEY_ENCRYPT hsm_client_key_encrypt;
    HSM_KEY_DECRYPT hsm_client_key_decrypt;
    HSM_KEY_DESTROY hsm_client_key_destroy;
};
typedef struct HSM_CLIENT_KEY_INTERFACE_TAG HSM_CLIENT_KEY_INTERFACE;
extern const HSM_CLIENT_KEY_INTERFACE* hsm_client_key_interface(void);

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

static inline int key_encrypt(KEY_HANDLE key_handle,
                              const SIZED_BUFFER *identity,
                              const SIZED_BUFFER *plaintext,
                              const SIZED_BUFFER *initialization_vector,
                              SIZED_BUFFER *ciphertext)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_encrypt(key_handle,
                                                 identity,
                                                 plaintext,
                                                 initialization_vector,
                                                 ciphertext);
}

static inline int key_decrypt(KEY_HANDLE key_handle,
                              const SIZED_BUFFER *identity,
                              const SIZED_BUFFER *ciphertext,
                              const SIZED_BUFFER *initialization_vector,
                              SIZED_BUFFER *plaintext)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    return key_interface->hsm_client_key_decrypt(key_handle,
                                                 identity,
                                                 ciphertext,
                                                 initialization_vector,
                                                 plaintext);
}

static inline void key_destroy(KEY_HANDLE key_handle)
{
    HSM_CLIENT_KEY_INTERFACE* key_interface = (HSM_CLIENT_KEY_INTERFACE*)key_handle;
    key_interface->hsm_client_key_destroy(key_handle);
}

#endif //HSM_KEY_INTERFACE_H