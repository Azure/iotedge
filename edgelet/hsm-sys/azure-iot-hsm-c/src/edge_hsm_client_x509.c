// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_client_data.h"
#include "hsm_log.h"
#include "hsm_utils.h"

extern const char* const EDGE_DEVICE_ALIAS;
extern const char* const ENV_DEVICE_ID;

int hsm_client_x509_init()
{
    return hsm_client_crypto_init();
}

void hsm_client_x509_deinit()
{
    hsm_client_crypto_deinit();
}

void iothub_hsm_free_buffer(void * buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

HSM_CLIENT_HANDLE iothub_x509_hsm_create()
{
    HSM_CLIENT_HANDLE result;
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    char *common_name = NULL;

    if ((hsm_get_env(ENV_DEVICE_ID, &common_name) != 0) || (common_name == NULL))
    {
        LOG_ERROR("Environment variable %s is not set or empty", ENV_DEVICE_ID);
        result = NULL;
    }
    else
    {
        result = interface->hsm_client_crypto_create();
    }
    return result;
}

void iothub_x509_hsm_destroy(HSM_CLIENT_HANDLE handle)
{
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    interface->hsm_client_crypto_destroy(handle);
}

static CERT_PROPS_HANDLE create_edge_device_properties
(
    const char *common_name,
    uint64_t validity_seconds
)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    if (certificate_props)
    {
        set_common_name(certificate_props, common_name);
        set_validity_seconds(certificate_props, validity_seconds);
        set_alias(certificate_props, EDGE_DEVICE_ALIAS);
        set_issuer_alias(certificate_props, hsm_get_device_ca_alias());
        set_certificate_type(certificate_props, CERTIFICATE_TYPE_CLIENT);
    }
    return certificate_props;
}

static CERT_INFO_HANDLE get_or_create_device_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    CERT_PROPS_HANDLE certificate_props;
    char *common_name = NULL;
    hsm_get_env(ENV_DEVICE_ID, &common_name);
    certificate_props = create_edge_device_properties(common_name, 315360000);
    return interface->hsm_client_create_certificate(hsm_handle, certificate_props);
}

char* iothub_x509_hsm_get_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE handle = get_or_create_device_certificate(hsm_handle);
    const char * cert = certificate_info_get_certificate(handle);
    char* result = NULL;
    mallocAndStrcpy_s(&result, cert);
    return result;
}

char* iothub_x509_hsm_get_certificate_key(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE handle = get_or_create_device_certificate(hsm_handle);
    size_t priv_key_len = 0;
    const char* key = certificate_info_get_private_key(handle, &priv_key_len);
    char* result = NULL;
    mallocAndStrcpy_s(&result, key);
    return result;
}

char* iothub_x509_hsm_get_common_name(HSM_CLIENT_HANDLE handle)
{
    (void)handle;
    char *common_name = NULL;
    hsm_get_env(ENV_DEVICE_ID, &common_name);
    char* result = NULL;
    mallocAndStrcpy_s(&result, common_name);
    return result;
}

static int iothub_x509_sign_with_private_key
(
    HSM_CLIENT_HANDLE hsm_handle,
    const unsigned char* data,
    size_t data_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    return interface->hsm_client_sign_with_private_key(hsm_handle, EDGE_DEVICE_ALIAS, data, data_size, digest, digest_size);
}

static const HSM_CLIENT_X509_INTERFACE x509_interface =
{
    iothub_x509_hsm_create,
    iothub_x509_hsm_destroy,
    iothub_x509_hsm_get_certificate,
    iothub_x509_hsm_get_certificate_key,
    iothub_x509_hsm_get_common_name,
    iothub_hsm_free_buffer,
    iothub_x509_sign_with_private_key
};

const HSM_CLIENT_X509_INTERFACE* hsm_client_x509_interface()
{
    return &x509_interface;
}
