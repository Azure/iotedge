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

typedef void* HSM_CLIENT_HANDLE;

typedef HSM_CLIENT_HANDLE (*HSM_CLIENT_CREATE)();
typedef void (*HSM_CLIENT_DESTROY)(HSM_CLIENT_HANDLE handle);

// TPM
typedef int (*HSM_CLIENT_ACTIVATE_IDENTITY_KEY)(HSM_CLIENT_HANDLE handle, const unsigned char* key, size_t key_len);
typedef int (*HSM_CLIENT_GET_ENDORSEMENT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char** key, size_t* key_len);
typedef int (*HSM_CLIENT_GET_STORAGE_ROOT_KEY)(HSM_CLIENT_HANDLE handle, unsigned char** key, size_t* key_len);
typedef int (*HSM_CLIENT_SIGN_WITH_IDENTITY)(HSM_CLIENT_HANDLE handle, const unsigned char* data, size_t data_len, unsigned char** key, size_t* key_len);

// x509
typedef char* (*HSM_CLIENT_GET_CERTIFICATE)(HSM_CLIENT_HANDLE handle);
typedef char* (*HSM_CLIENT_GET_CERT_KEY)(HSM_CLIENT_HANDLE handle);
typedef char* (*HSM_CLIENT_GET_COMMON_NAME)(HSM_CLIENT_HANDLE handle);

typedef struct HSM_CLIENT_TPM_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_tpm_create;
    HSM_CLIENT_DESTROY hsm_client_tpm_destroy;

    HSM_CLIENT_ACTIVATE_IDENTITY_KEY hsm_client_activate_identity_key;
    HSM_CLIENT_GET_ENDORSEMENT_KEY hsm_client_get_ek;
    HSM_CLIENT_GET_STORAGE_ROOT_KEY hsm_client_get_srk;
    HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity;
} HSM_CLIENT_TPM_INTERFACE;

typedef struct HSM_CLIENT_X509_INTERFACE_TAG
{
    HSM_CLIENT_CREATE hsm_client_x509_create;
    HSM_CLIENT_DESTROY hsm_client_x509_destroy;

    HSM_CLIENT_GET_CERTIFICATE hsm_client_get_cert;
    HSM_CLIENT_GET_CERT_KEY hsm_client_get_key;
    HSM_CLIENT_GET_COMMON_NAME hsm_client_get_common_name;
} HSM_CLIENT_X509_INTERFACE;

extern int initialize_hsm_system();
extern void deinitialize_hsm_system();

extern const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_interface();
extern const HSM_CLIENT_X509_INTERFACE* hsm_client_x509_interface();

extern int hsm_client_x509_init();
extern void hsm_client_x509_deinit();
extern int hsm_client_tpm_init();
extern void hsm_client_tpm_deinit();

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif // HSM_CLIENT_DATA_H