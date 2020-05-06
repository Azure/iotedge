// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <math.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_client_data.h"
#include "hsm_constants.h"
#include "hsm_err.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//##############################################################################
// Static data variables
//##############################################################################
static bool g_is_x509_initialized = false;
static unsigned int g_ref_cnt = 0;

//##############################################################################
// Forward declarations
//##############################################################################
static CERT_INFO_HANDLE edge_x509_hsm_get_cert_info(HSM_CLIENT_HANDLE hsm_handle);

//##############################################################################
// Interface implementation
//##############################################################################
int hsm_client_x509_init(uint64_t  auto_generated_cert_lifetime)
{
    int result;

    if (!g_is_x509_initialized)
    {
        log_init(LVL_INFO);

        result = hsm_client_crypto_init(auto_generated_cert_lifetime);
        if (result == 0)
        {
            g_is_x509_initialized = true;
        }
    }
    else
    {
        result = 0;
    }

    return result;
}

void hsm_client_x509_deinit()
{
    if (!g_is_x509_initialized)
    {
        LOG_ERROR("hsm_client_x509_init not called");
    }
    else
    {
        if (g_ref_cnt == 0)
        {
            g_is_x509_initialized = false;
            hsm_client_crypto_deinit();
        }
    }
}

void edge_x509_hsm_free_buffer(void * buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

HSM_CLIENT_HANDLE edge_x598_hsm_create()
{
    HSM_CLIENT_HANDLE result;

    if (!g_is_x509_initialized)
    {
        LOG_ERROR("hsm_client_x509_init not called");
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
            if (result != NULL)
            {
                g_ref_cnt++;
            }
        }
    }

    return result;
}

void edge_x509_hsm_destroy(HSM_CLIENT_HANDLE hsm_handle)
{
    if (!g_is_x509_initialized)
    {
        LOG_ERROR("hsm_client_x509_init not called");
    }
    else
    {
        if (hsm_handle == NULL)
        {
            LOG_ERROR("Null hsm handle parameter");
        }
        else if (g_ref_cnt == 0)
        {
            LOG_ERROR("Mismatch in overall handle create and destroy calls");
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
                interface->hsm_client_crypto_destroy(hsm_handle);
            }
            g_ref_cnt--;
        }
    }
}

static int get_device_id_cert_env_vars(char **device_cert_file_path, char **device_pk_file_path)
{
    int result;
    char *cert_path = NULL;
    char *key_path = NULL;

    if (hsm_get_env(ENV_DEVICE_ID_CERTIFICATE_PATH, &cert_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_ID_CERTIFICATE_PATH);
        result = __FAILURE__;
    }
    else if (hsm_get_env(ENV_DEVICE_ID_PRIVATE_KEY_PATH, &key_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_ID_PRIVATE_KEY_PATH);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    if (result != 0)
    {
        FREEIF(cert_path);
        FREEIF(key_path);
    }

    *device_cert_file_path = cert_path;
    *device_pk_file_path = key_path;

    return result;
}

static CERT_INFO_HANDLE get_device_id_cert_if_exists(HSM_CLIENT_HANDLE hsm_handle)
{
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();

    CERT_INFO_HANDLE result = interface->hsm_client_crypto_get_certificate(hsm_handle,
                                                                           EDGE_DEVICE_ALIAS);
    if (result == NULL)
    {
        LOG_INFO("Failed to obtain device identity certificate");
    }

    return result;
}

static CERT_INFO_HANDLE get_device_identity_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE result;
    char *device_cert_file_path = NULL;
    char *device_pk_file_path = NULL;

    if (get_device_id_cert_env_vars(&device_cert_file_path, &device_pk_file_path) != 0)
    {
        result = NULL;
    }
    else
    {
        if ((device_cert_file_path != NULL) && (device_pk_file_path != NULL))
        {
            // obtain provisioned device id certificate
            result = get_device_id_cert_if_exists(hsm_handle);
        }
        else
        {
            // no device certificate and key were provided so we return NULL
            LOG_INFO("Env vars [%s, %s] for the Edge device identity certificate "
                     "and private key were not set",
                     ENV_DEVICE_ID_CERTIFICATE_PATH, ENV_DEVICE_ID_PRIVATE_KEY_PATH);
            result = NULL;
        }
    }

    FREEIF(device_cert_file_path);
    FREEIF(device_pk_file_path);

    return result;
}

char* edge_x509_hsm_get_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    (void)hsm_handle;
    LOG_ERROR("API unsupported");

    return NULL;
}

char* edge_x509_hsm_get_certificate_key(HSM_CLIENT_HANDLE hsm_handle)
{
    (void)hsm_handle;
    LOG_ERROR("API unsupported");

    return NULL;
}

char* edge_x509_hsm_get_common_name(HSM_CLIENT_HANDLE hsm_handle)
{
    (void)hsm_handle;
    LOG_ERROR("API unsupported");

    return NULL;
}

static int edge_x509_sign_with_private_key
(
    HSM_CLIENT_HANDLE hsm_handle,
    const unsigned char* data,
    size_t data_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;
    CERT_INFO_HANDLE cert_info;

    if (!g_is_x509_initialized)
    {
        LOG_ERROR("hsm_client_x509_init not called");
        result = __FAILURE__;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = __FAILURE__;
    }
    // check if the device certificate exists and valid before performing
    // any sign operations
    else if ((cert_info = edge_x509_hsm_get_cert_info(hsm_handle)) == NULL)
    {
        LOG_ERROR("Device certificate info could not be obtained");
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
        certificate_info_destroy(cert_info);
    }

    return result;
}

static CERT_INFO_HANDLE edge_x509_hsm_get_cert_info(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE result;

    if (!g_is_x509_initialized)
    {
        LOG_ERROR("hsm_client_x509_init not called");
        result = NULL;
    }
    else if (hsm_handle == NULL)
    {
        LOG_ERROR("hsm_handle parameter is null");
        result = NULL;
    }
    else
    {
        result = get_device_identity_certificate(hsm_handle);
        if (result == NULL)
        {
            LOG_ERROR("Could not create device identity certificate info handle");
        }
    }

    return result;
}

static const HSM_CLIENT_X509_INTERFACE x509_interface =
{
    edge_x598_hsm_create,
    edge_x509_hsm_destroy,
    edge_x509_hsm_get_certificate,
    edge_x509_hsm_get_certificate_key,
    edge_x509_hsm_get_common_name,
    edge_x509_hsm_free_buffer,
    edge_x509_sign_with_private_key,
    edge_x509_hsm_get_cert_info
};

const HSM_CLIENT_X509_INTERFACE* hsm_client_x509_interface()
{
    return &x509_interface;
}
