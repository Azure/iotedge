#ifndef HSM_KEY_H
#define HSM_KEY_H

#ifdef __cplusplus
#include <cstddef>
extern "C" {
#else
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

MOCKABLE_FUNCTION(, KEY_HANDLE, create_sas_key, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, void, destroy_sas_key, KEY_HANDLE, key_handle);

#ifdef __cplusplus
}
#endif

#endif  //HSM_KEY_H
