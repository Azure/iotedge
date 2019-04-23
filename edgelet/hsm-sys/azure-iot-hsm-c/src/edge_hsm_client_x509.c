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
#include "hsm_log.h"
#include "hsm_utils.h"

extern const char* const EDGE_DEVICE_ALIAS;
extern const char* const ENV_DEVICE_CERTIFICATE_PATH;
extern const char* const ENV_DEVICE_PRIVATE_KEY_PATH;
extern const char* const ENV_REGISTRATION_ID;

static bool g_is_x509_initialized = false;
static unsigned int g_ref_cnt = 0;

int hsm_client_x509_init()
{
    int result;

    if (!g_is_x509_initialized)
    {
        result = hsm_client_crypto_init();
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

static CERT_INFO_HANDLE create_device_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE result, issuer;
    uint64_t validity_seconds = 0;
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    const char* issuer_alias = hsm_get_device_ca_alias();
    char *common_name;

    if ((hsm_get_env(ENV_REGISTRATION_ID, &common_name) != 0) ||
        (common_name == NULL))
    {
        LOG_ERROR("Environment variable %s is not set or empty."
                  "This value is required to create the device identity certificate",
                  ENV_REGISTRATION_ID);
        result = NULL;
    }
    else if (strlen(common_name) == 0)
    {
        LOG_ERROR("Environment variable %s is not set or empty."
                  "This value is required to create the device identity certificate",
                  ENV_REGISTRATION_ID);
        free(common_name);
        result = NULL;
    }
    else if ((issuer = interface->hsm_client_crypto_get_certificate(hsm_handle,
                                                                    issuer_alias)) == NULL)
    {
        LOG_ERROR("Issuer alias %s does not exist", issuer_alias);
        free(common_name);
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
        else
        {
            double seconds_left = 0;
            time_t now = time(NULL);

            if ((seconds_left = difftime(validity_seconds, now)) <= 0)
            {
                LOG_ERROR("Issuer certificate has expired");
                result = NULL;
            }
            else if ((certificate_props = create_edge_device_properties(common_name,
                                                                        (uint64_t)floor(seconds_left),
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
        }
        free(common_name);
        certificate_info_destroy(issuer);
    }

    return result;
}

static int get_device_id_cert_env_vars(char **device_cert_file_path, char **device_pk_file_path)
{
    int result;
    char *cert_path = NULL;
    char *key_path = NULL;

    if (hsm_get_env(ENV_DEVICE_CERTIFICATE_PATH, &cert_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_CERTIFICATE_PATH);
        result = __FAILURE__;
    }
    else if (hsm_get_env(ENV_DEVICE_PRIVATE_KEY_PATH, &key_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_PRIVATE_KEY_PATH);
        if (cert_path != NULL)
        {
            free(cert_path);
        }
        result = __FAILURE__;
    }
    else
    {
        *device_cert_file_path = cert_path;
        *device_pk_file_path = key_path;
        result = 0;
    }

    return result;
}

static CERT_INFO_HANDLE prepare_device_certifiicate_info
(
    const char *cert_file_path,
    const char *pk_file_path
)
{
    CERT_INFO_HANDLE result;
    char *cert_contents, *private_key_contents;
    size_t private_key_size = 0;

    if ((private_key_contents = read_file_into_buffer(pk_file_path, &private_key_size)) == NULL)
    {
        LOG_ERROR("Could not load private key into buffer %s", pk_file_path);
        result = NULL;
    }
    else if ((cert_contents = read_file_into_cstring(cert_file_path, NULL)) == NULL)
    {
        LOG_ERROR("Could not read certificate into buffer %s", cert_file_path);
        free(private_key_contents);
        result = NULL;
    }
    else
    {
        result = certificate_info_create(cert_contents,
                                         private_key_contents,
                                         private_key_size,
                                         (private_key_size != 0) ? PRIVATE_KEY_PAYLOAD :
                                                                   PRIVATE_KEY_UNKNOWN);
        free(private_key_contents);
        free(cert_contents);
    }

    return result;
}

static CERT_INFO_HANDLE get_or_create_device_certificate(HSM_CLIENT_HANDLE hsm_handle)
{
    CERT_INFO_HANDLE result;
    char *device_cert_file_path = NULL;
    char *device_pk_file_path = NULL;
    bool env_set = false;
    unsigned int mask = 0, i = 0;

    if (get_device_id_cert_env_vars(&device_cert_file_path, &device_pk_file_path) != 0)
    {
        result = NULL;
    }
    else
    {
        if (device_cert_file_path != NULL)
        {
            if ((strlen(device_cert_file_path) != 0) && is_file_valid(device_cert_file_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_CERTIFICATE_PATH, device_cert_file_path);
            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_CERTIFICATE_PATH, device_cert_file_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_CERTIFICATE_PATH);
        }

        if (device_pk_file_path != NULL)
        {
            if ((strlen(device_pk_file_path) != 0) && is_file_valid(device_pk_file_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_PRIVATE_KEY_PATH, device_pk_file_path);
            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_PRIVATE_KEY_PATH, device_pk_file_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_PRIVATE_KEY_PATH);
        }

        LOG_DEBUG("Device identity setup mask 0x%02x", mask);
        if (env_set && (mask != 0x3))
        {
            LOG_ERROR("To provisiong the Edge with device identity certificates, set "
                      "env variables with valid values:\n  %s\n  %s",
                      ENV_DEVICE_CERTIFICATE_PATH, ENV_DEVICE_PRIVATE_KEY_PATH);
            result = NULL;
        }
        else
        {
            if (env_set)
            {
                result = prepare_device_certifiicate_info(device_cert_file_path,
                                                          device_pk_file_path);
            }
            else
            {
                // no device certificate and key were provided so generate them
                result = create_device_certificate(hsm_handle);
            }
        }
        if (device_cert_file_path != NULL)
        {
            free(device_cert_file_path);
        }
        if (device_pk_file_path != NULL)
        {
            free(device_pk_file_path);
        }
    }

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
        result = get_or_create_device_certificate(hsm_handle);
        if (result == NULL)
        {
            LOG_ERROR("Could not create device certificate info handle");
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
