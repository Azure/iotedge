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
static unsigned int g_ref_cnt = 0;

int hsm_client_x509_init()
{
    int result;

    if (g_ref_cnt == 0)
    {
        result = hsm_client_crypto_init();
        if (result == 0)
        {
            g_ref_cnt = 1;
        }
    }
    else
    {
        g_ref_cnt++;
        result = 0;
    }

    return result;
}

void hsm_client_x509_deinit()
{
    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
    }
    else
    {
        g_ref_cnt--;
        if (g_ref_cnt == 0)
        {
            hsm_client_crypto_deinit();
        }
    }
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

    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
        result = NULL;
    }
    else
    {
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        if (interface == NULL)
        {
            LOG_ERROR("hsm_client_crypto_interface returned NULL");
            result = NULL;
        }
        else
        {
            result = interface->hsm_client_crypto_create();
        }
    }

    return result;
}

void iothub_x509_hsm_destroy(HSM_CLIENT_HANDLE handle)
{
    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
    }
    else
    {
        if (handle == NULL)
        {
            LOG_ERROR("Null handle parameter");
        }
        else
        {
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            if (interface == NULL)
            {
                LOG_ERROR("hsm_client_crypto_interface returned NULL");
            }
            else
            {
                interface->hsm_client_crypto_destroy(handle);
            }
        }
    }
}

static CERT_PROPS_HANDLE create_edge_device_properties
(
    const char* common_name,
    uint64_t validity_seconds,
    const char* issuer_alias
)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    const char* alias = EDGE_DEVICE_ALIAS;

    if (certificate_props == NULL)
    {
        LOG_ERROR("Could not create certificate props for %s", alias);
    }
    else if (set_common_name(certificate_props, common_name) != 0)
    {
        LOG_ERROR("Could not set common name for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_validity_seconds(certificate_props, validity_seconds) != 0)
    {
        LOG_ERROR("Could not set validity for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_alias(certificate_props, alias) != 0)
    {
        LOG_ERROR("Could not set alias for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_issuer_alias(certificate_props, issuer_alias) != 0)
    {
        LOG_ERROR("Could not set issuer alias for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_certificate_type(certificate_props, CERTIFICATE_TYPE_CLIENT) != 0)
    {
        LOG_ERROR("Could not set certificate type for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }

    return certificate_props;
}

static CERT_INFO_HANDLE get_or_create_device_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE result, issuer;
    uint64_t validity_seconds = 0;
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    const char* issuer_alias = hsm_get_device_ca_alias();
    char *common_name = NULL;

    if ((hsm_get_env(ENV_DEVICE_ID, &common_name) != 0) ||
        (common_name == NULL) ||
        (strlen(common_name) == 0))
    {
        LOG_ERROR("Environment variable %s is not set or empty", ENV_DEVICE_ID);
        result = NULL;
    }
    else if ((issuer = interface->hsm_client_crypto_get_certificate(hsm_handle,
                                                                    issuer_alias)) == NULL)
    {
        LOG_ERROR("Issuer alias %s does not exist", issuer_alias);
        result = NULL;
    }
    else
    {
        CERT_PROPS_HANDLE certificate_props;
        if ((validity_seconds = certificate_info_get_valid_to(issuer)) == 0)
        {
            LOG_ERROR("Issuer alias's %s certificate contains invalid expiration", issuer_alias);
            result = NULL;
        }
        else if ((certificate_props = create_edge_device_properties(common_name,
                                                                    validity_seconds,
                                                                    issuer_alias)) == NULL)
        {
            LOG_ERROR("Error creating certificate properties for device certificate");
            result = NULL;
        }
        else
        {
            result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
            if (result == NULL)
            {
                LOG_ERROR("Failed to create device certificate with CN %s", common_name);
            }
            cert_properties_destroy(certificate_props);
        }
        certificate_info_destroy(issuer);
    }

    return result;
}

char* iothub_x509_hsm_get_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    char* result;

    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
        result = NULL;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = NULL;
    }
    else
    {
        CERT_INFO_HANDLE handle = get_or_create_device_certificate(hsm_handle);
        if (handle == NULL)
        {
            LOG_ERROR("Could not obtain device certificate");
            result = NULL;
        }
        else
        {
            const char * certificate;
            if ((certificate = certificate_info_get_certificate(handle)) == NULL)
            {
                LOG_ERROR("Could retrieve device certificate buffer");
                result = NULL;
            }
            else
            {
                result = NULL;
                if (mallocAndStrcpy_s(&result, certificate) != 0)
                {
                    LOG_ERROR("Could not allocate memory to store device certificate");
                }
            }
        }
    }

    return result;
}

char* iothub_x509_hsm_get_certificate_key(HSM_CLIENT_HANDLE hsm_handle)
{
    char* result;

    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
        result = NULL;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = NULL;
    }
    else
    {
        size_t priv_key_len = 0;
        CERT_INFO_HANDLE handle = get_or_create_device_certificate(hsm_handle);
        if (handle == NULL)
        {
            LOG_ERROR("Could not obtain device private key");
            result = NULL;
        }
        else
        {
            const char* private_key;
            if ((private_key = certificate_info_get_private_key(handle, &priv_key_len)) == NULL)
            {
                LOG_ERROR("Could retrieve device private key buffer");
                result = NULL;
            }
            else
            {
                result = NULL;
                if (mallocAndStrcpy_s(&result, private_key) != 0)
                {
                    LOG_ERROR("Could not allocate memory to store device certificate");
                }
            }
        }
    }

    return result;
}

char* iothub_x509_hsm_get_common_name(HSM_CLIENT_HANDLE hsm_handle)
{
    (void)hsm_handle;
    char *result;

    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
        result = NULL;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = NULL;
    }
    else
    {
        char *common_name = NULL;
        if ((hsm_get_env(ENV_DEVICE_ID, &common_name) != 0) || (common_name == NULL))
        {
            LOG_ERROR("Environment variable %s is not set or empty", ENV_DEVICE_ID);
            result = NULL;
        }
        else
        {
            result = NULL;
            if (mallocAndStrcpy_s(&result, common_name) != 0)
            {
                LOG_ERROR("Could not allocate memory to store device certificate common name");
            }
        }
    }

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
    int result;

    if (g_ref_cnt == 0)
    {
        LOG_ERROR("hsm_client_x509_deinit not called");
        result = __FAILURE__;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = __FAILURE__;
    }
    else
    {
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        result = interface->hsm_client_crypto_sign_with_private_key(hsm_handle,
                                                                    EDGE_DEVICE_ALIAS,
                                                                    data,
                                                                    data_size,
                                                                    digest,
                                                                    digest_size);
    }

    return result;
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
