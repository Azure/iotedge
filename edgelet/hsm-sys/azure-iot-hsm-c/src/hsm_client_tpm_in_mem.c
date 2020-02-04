// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_err.h"
#include "hsm_log.h"
#include "hsm_constants.h"

struct EDGE_TPM_TAG
{
    HSM_CLIENT_STORE_HANDLE hsm_store_handle;
};
typedef struct EDGE_TPM_TAG EDGE_TPM;

static const HSM_CLIENT_STORE_INTERFACE* g_hsm_store_if = NULL;
static const HSM_CLIENT_KEY_INTERFACE* g_hsm_key_if = NULL;
static bool g_is_tpm_initialized = false;

int hsm_client_tpm_store_init(void)
{
    int result;
    int status;

    if (!g_is_tpm_initialized)
    {
        const HSM_CLIENT_STORE_INTERFACE* store_if;
        const HSM_CLIENT_KEY_INTERFACE* key_if;

        log_init(LVL_INFO);

        if ((store_if = hsm_client_store_interface()) == NULL)
        {
            LOG_ERROR("HSM store interface not available");
            result = __FAILURE__;
        }
        else if ((key_if = hsm_client_key_interface()) == NULL)
        {
            LOG_ERROR("HSM key interface not available");
            result = __FAILURE__;
        }
        else if ((status = store_if->hsm_client_store_create(EDGE_STORE_NAME, CA_VALIDITY)) != 0)
        {
            LOG_ERROR("Could not create store. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            g_is_tpm_initialized = true;
            g_hsm_store_if = store_if;
            g_hsm_key_if = key_if;
            result = 0;
        }
    }
    else
    {
        LOG_ERROR("Re-initializing TPM without de-initializing");
        result = __FAILURE__;
    }
    return result;
}

void hsm_client_tpm_store_deinit(void)
{
    if (!g_is_tpm_initialized)
    {
        LOG_ERROR("hsm_client_tpm_init not called");
    }
    else
    {
        g_hsm_store_if = NULL;
        g_hsm_key_if = NULL;
        g_is_tpm_initialized = false;
    }
}

static HSM_CLIENT_HANDLE edge_hsm_client_tpm_create(void)
{
    HSM_CLIENT_HANDLE result;
    EDGE_TPM* edge_tpm;
    const HSM_CLIENT_STORE_INTERFACE* store_if = g_hsm_store_if;

    if (!g_is_tpm_initialized)
    {
        LOG_ERROR("hsm_client_tpm_init not called");
        result = NULL;
    }
    else if ((edge_tpm = (EDGE_TPM*)calloc(1, sizeof(EDGE_TPM))) == NULL)
    {
        LOG_ERROR("Could not allocate memory for TPM client");
        result = NULL;
    }
    else if ((edge_tpm->hsm_store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME)) == NULL)
    {
        LOG_ERROR("Could not open store");
        free(edge_tpm);
        result = NULL;
    }
    else
    {
        result = (HSM_CLIENT_HANDLE)edge_tpm;
    }
    return result;
}

static void edge_hsm_client_tpm_destroy(HSM_CLIENT_HANDLE handle)
{
    if (!g_is_tpm_initialized)
    {
        LOG_ERROR("hsm_client_tpm_init not called");
    }
    else if (handle != NULL)
    {
        int status;
        EDGE_TPM *edge_tpm = (EDGE_TPM*)handle;
        const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
        if ((status = store_if->hsm_client_store_close(edge_tpm->hsm_store_handle)) != 0)
        {
            LOG_ERROR("Could not close store handle. Error code %d", status);
        }
        free(edge_tpm);
    }
}

static int edge_hsm_client_activate_identity_key
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* key,
    size_t key_len
)
{
    int result;
    if (!g_is_tpm_initialized)
    {
        LOG_ERROR("hsm_client_tpm_init not called");
        result = __FAILURE__;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = __FAILURE__;
    }
    else if (key == NULL)
    {
        LOG_ERROR("Invalid key specified");
        result = __FAILURE__;
    }
    else if (key_len == 0)
    {
        LOG_ERROR("Key len length cannot be 0");
        result = __FAILURE__;
    }
    else
    {
        int status;
        const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
        EDGE_TPM *edge_tpm = (EDGE_TPM*)handle;
        if ((status = store_if->hsm_client_store_insert_sas_key(edge_tpm->hsm_store_handle,
                                                                EDGELET_IDENTITY_SAS_KEY_NAME,
                                                                key, key_len)) != 0)
        {
            LOG_ERROR("Could not insert SAS key. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int ek_srk_unsupported
(
    HSM_CLIENT_HANDLE handle,
    unsigned char** key,
    size_t* key_len
)
{
    int result = 0;

    if (key == NULL)
    {
        LOG_ERROR("Invalid key specified");
        result = __FAILURE__;
    }
    else
    {
        *key = NULL;
    }
    if (key_len == NULL)
    {
        LOG_ERROR("Invalid key len specified");
        result = __FAILURE__;
    }
    else
    {
        *key_len = 0;
    }
    if (result == 0)
    {
        if (!g_is_tpm_initialized)
        {
            LOG_ERROR("hsm_client_tpm_init not called");
            result = __FAILURE__;
        }
        else if (handle == NULL)
        {
            LOG_ERROR("Invalid handle value specified");
            result = __FAILURE__;
        }
        else
        {
            LOG_ERROR("API unsupported");
            result = __FAILURE__;
        }
    }
    return result;
}

static int edge_hsm_client_get_ek
(
    HSM_CLIENT_HANDLE handle,
    unsigned char** key,
    size_t* key_len
)
{
    return ek_srk_unsupported(handle, key, key_len);
}

static int edge_hsm_client_get_srk
(
    HSM_CLIENT_HANDLE handle,
    unsigned char** key,
    size_t* key_len
)
{
    return ek_srk_unsupported(handle, key, key_len);
}

static int perform_sign
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size,
    int do_derive
)
{
    int result = 0;
    if (digest == NULL)
    {
        LOG_ERROR("Invalid digest specified");
        result = __FAILURE__;
    }
    else
    {
        *digest = NULL;
    }
    if (digest_size == NULL)
    {
        LOG_ERROR("Invalid digest size specified");
        result = __FAILURE__;
    }
    else
    {
        *digest_size = 0;
    }
    if (result == 0)
    {
        if (!g_is_tpm_initialized)
        {
            LOG_ERROR("hsm_client_tpm_init not called");
            result = __FAILURE__;
        }
        else if (handle == NULL)
        {
            LOG_ERROR("Invalid handle value specified");
            result = __FAILURE__;
        }
        else if (data_to_be_signed == NULL)
        {
            LOG_ERROR("Invalid data to be signed specified");
            result = __FAILURE__;
        }
        else if (data_to_be_signed_size == 0)
        {
            LOG_ERROR("Invalid data to be signed length specified");
            result = __FAILURE__;
        }
        else if ((identity == NULL) && do_derive)
        {
            LOG_ERROR("Invalid identity specified");
            result = __FAILURE__;
        }
        else if ((identity_size == 0) && do_derive)
        {
            LOG_ERROR("Invalid identity length specified");
            result = __FAILURE__;
        }
        else
        {
            KEY_HANDLE key_handle;
            const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
            const HSM_CLIENT_KEY_INTERFACE *key_if = g_hsm_key_if;
            EDGE_TPM* edge_tpm = (EDGE_TPM*)handle;
            key_handle = store_if->hsm_client_store_open_key(edge_tpm->hsm_store_handle,
                                                             HSM_KEY_SAS,
                                                             EDGELET_IDENTITY_SAS_KEY_NAME);
            if (key_handle == NULL)
            {
                LOG_ERROR("Could not get SAS key by name '%s'", EDGELET_IDENTITY_SAS_KEY_NAME);
                result = __FAILURE__;
            }
            else
            {
                int status;
                if (identity != NULL)
                {
                    status = key_if->hsm_client_key_derive_and_sign(key_handle,
                                                                    data_to_be_signed,
                                                                    data_to_be_signed_size,
                                                                    identity,
                                                                    identity_size,
                                                                    digest,
                                                                    digest_size);
                }
                else
                {
                    status = key_if->hsm_client_key_sign(key_handle,
                                                        data_to_be_signed,
                                                        data_to_be_signed_size,
                                                        digest,
                                                        digest_size);
                }

                if (status != 0)
                {
                    LOG_ERROR("Error computing signature using identity key. Error code %d", status);
                    result = __FAILURE__;
                }
                else
                {
                    result = 0;
                }
                // always close the key handle
                status = store_if->hsm_client_store_close_key(edge_tpm->hsm_store_handle, key_handle);
                if (status != 0)
                {
                    LOG_ERROR("Error closing key handle. Error code %d", status);
                    result = __FAILURE__;
                }
            }
        }
    }
    return result;
}

static int edge_hsm_client_sign_with_identity
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    return perform_sign(handle, data_to_be_signed, data_to_be_signed_size,
                        NULL, 0, digest, digest_size, 0);
}

static int edge_hsm_client_derive_and_sign_with_identity
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    return perform_sign(handle, data_to_be_signed, data_to_be_signed_size,
                        identity, identity_size, digest, digest_size, 1);
}

static void edge_hsm_free_buffer(void *buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

static const HSM_CLIENT_TPM_INTERFACE edge_tpm_interface =
{
    edge_hsm_client_tpm_create,
    edge_hsm_client_tpm_destroy,
    edge_hsm_client_activate_identity_key,
    edge_hsm_client_get_ek,
    edge_hsm_client_get_srk,
    edge_hsm_client_sign_with_identity,
    edge_hsm_client_derive_and_sign_with_identity,
    edge_hsm_free_buffer
};

const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_store_interface()
{
    return &edge_tpm_interface;
}
