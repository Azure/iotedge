#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_err.h"
#include "hsm_key.h"
#include "hsm_log.h"
#include "hsm_constants.h"

struct EDGE_CRYPTO_TAG
{
    HSM_CLIENT_STORE_HANDLE hsm_store_handle;
};
typedef struct EDGE_CRYPTO_TAG EDGE_CRYPTO;

static const HSM_CLIENT_STORE_INTERFACE* g_hsm_store_if = NULL;
static const HSM_CLIENT_KEY_INTERFACE* g_hsm_key_if = NULL;
static bool g_is_crypto_initialized = false;
static unsigned int g_crypto_ref = 0;

int hsm_client_crypto_init(uint64_t auto_generated_ca_lifetime)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        int status;
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
        else if ((status = store_if->hsm_client_store_create(EDGE_STORE_NAME, auto_generated_ca_lifetime)) != 0)
        {
            LOG_ERROR("Could not create store. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            g_is_crypto_initialized = true;
            g_hsm_store_if = store_if;
            g_hsm_key_if = key_if;
            result = 0;
        }
    }
    else
    {
        result = 0;
    }

    return result;
}

void hsm_client_crypto_deinit(void)
{
    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_tpm_init not called");
    }
    else
    {
        if (g_crypto_ref == 0)
        {
            int status;
            if ((status = g_hsm_store_if->hsm_client_store_destroy(EDGE_STORE_NAME)) != 0)
            {
                LOG_ERROR("Could not destroy store. Error code %d", status);
            }
            g_hsm_store_if = NULL;
            g_hsm_key_if = NULL;
            g_is_crypto_initialized = false;
        }
    }
}

static void edge_hsm_crypto_free_buffer(void * buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

static HSM_CLIENT_HANDLE edge_hsm_client_crypto_create(void)
{
    HSM_CLIENT_HANDLE result;
    EDGE_CRYPTO* edge_crypto;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = NULL;
    }
    else if ((edge_crypto = (EDGE_CRYPTO*)calloc(1, sizeof(EDGE_CRYPTO))) == NULL)
    {
        LOG_ERROR("Could not allocate memory for crypto client");
        result = NULL;
    }
    else if ((edge_crypto->hsm_store_handle = g_hsm_store_if->hsm_client_store_open(EDGE_STORE_NAME)) == NULL)
    {
        LOG_ERROR("Could not open store");
        free(edge_crypto);
        result = NULL;
    }
    else
    {
        result = (HSM_CLIENT_HANDLE)edge_crypto;
        g_crypto_ref++;
    }
    return result;
}

static void edge_hsm_client_crypto_destroy(HSM_CLIENT_HANDLE handle)
{
    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
    }
    else if (handle != NULL)
    {
        int status;
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        if ((status = g_hsm_store_if->hsm_client_store_close(edge_crypto->hsm_store_handle)) != 0)
        {
            LOG_ERROR("Could not close store handle. Error code %d", status);
        }
        free(edge_crypto);
        if (g_crypto_ref > 0)
        {
            g_crypto_ref--;
        }
    }
}

static int edge_hsm_client_get_random_bytes(HSM_CLIENT_HANDLE handle, unsigned char* rand_buffer, size_t num_bytes)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = __FAILURE__;
    }
    else if (rand_buffer == NULL)
    {
        LOG_ERROR("Invalid buffer specified");
        result = __FAILURE__;
    }
    else if (num_bytes == 0)
    {
        LOG_ERROR("Invalid number of bytes specified");
        result = __FAILURE__;
    }
    else
    {
        result = generate_rand_buffer(rand_buffer, num_bytes);
    }

    return result;
}

static int edge_hsm_client_create_master_encryption_key(HSM_CLIENT_HANDLE handle)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = __FAILURE__;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        if (g_hsm_store_if->hsm_client_store_insert_encryption_key(edge_crypto->hsm_store_handle,
                                                                   EDGELET_ENC_KEY_NAME) != 0)
        {
            LOG_ERROR("Could not insert encryption key %s", EDGELET_ENC_KEY_NAME);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int edge_hsm_client_destroy_master_encryption_key(HSM_CLIENT_HANDLE handle)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = __FAILURE__;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        if (g_hsm_store_if->hsm_client_store_remove_key(edge_crypto->hsm_store_handle,
                                                        HSM_KEY_ENCRYPTION,
                                                        EDGELET_ENC_KEY_NAME) != 0)
        {
            LOG_ERROR("Could not remove encryption key %s", EDGELET_ENC_KEY_NAME);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_create_certificate
(
    HSM_CLIENT_HANDLE handle,
    CERT_PROPS_HANDLE certificate_props
)
{
    CERT_INFO_HANDLE result;
    const char* alias;
    const char* issuer_alias;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = NULL;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = NULL;
    }
    else if (certificate_props == NULL)
    {
        LOG_ERROR("Invalid certificate props value specified");
        result = NULL;
    }
    else if ((alias = get_alias(certificate_props)) == NULL)
    {
        LOG_ERROR("Invalid certificate props alias value");
        result = NULL;
    }
    else if ((issuer_alias = get_issuer_alias(certificate_props)) == NULL)
    {
        LOG_ERROR("Invalid certificate props issuer alias value");
        result = NULL;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        if (g_hsm_store_if->hsm_client_store_create_pki_cert(edge_crypto->hsm_store_handle,
                                                             certificate_props) != 0)
        {
            LOG_ERROR("Could not create certificate in the store");
            result = NULL;
        }
        else
        {
            result = g_hsm_store_if->hsm_client_store_get_pki_cert(edge_crypto->hsm_store_handle,
                                                                   alias);
        }
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_get_trust_bundle(HSM_CLIENT_HANDLE handle)
{
    CERT_INFO_HANDLE result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = NULL;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = NULL;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        result = g_hsm_store_if->hsm_client_store_get_pki_trusted_certs(edge_crypto->hsm_store_handle);
    }

    return result;
}

static void edge_hsm_client_destroy_certificate(HSM_CLIENT_HANDLE handle, const char* alias)
{
    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid cert bundle alias specified");
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        if (g_hsm_store_if->hsm_client_store_remove_pki_cert(edge_crypto->hsm_store_handle,
                                                             alias) != 0)
        {
            LOG_INFO("Could not destroy certificate in the store for alias: %s", alias);
        }
    }
}

static bool validate_sized_buffer(const SIZED_BUFFER *sized_buffer)
{
    bool result = false;
    if ((sized_buffer != NULL) && (sized_buffer->buffer != NULL) && (sized_buffer->size != 0))
    {
        result = true;
    }
    return result;
}

static int encrypt_data
(
    EDGE_CRYPTO *edge_crypto,
    const SIZED_BUFFER *id,
    const SIZED_BUFFER *pt,
    const SIZED_BUFFER *iv,
    SIZED_BUFFER *ct
)
{
    int result;
    KEY_HANDLE key_handle;
    const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
    const HSM_CLIENT_KEY_INTERFACE *key_if = g_hsm_key_if;
    key_handle = store_if->hsm_client_store_open_key(edge_crypto->hsm_store_handle,
                                                     HSM_KEY_ENCRYPTION,
                                                     EDGELET_ENC_KEY_NAME);
    if (key_handle == NULL)
    {
        LOG_ERROR("Could not get encryption key by name '%s'", EDGELET_ENC_KEY_NAME);
        result = __FAILURE__;
    }
    else
    {
        int status = key_if->hsm_client_key_encrypt(key_handle, id, pt, iv, ct);
        if (status != 0)
        {
            LOG_ERROR("Error encrypting data. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        // always close the key handle
        status = store_if->hsm_client_store_close_key(edge_crypto->hsm_store_handle, key_handle);
        if (status != 0)
        {
            LOG_ERROR("Error closing key handle. Error code %d", status);
            result = __FAILURE__;
        }
    }

    return result;
}

static int decrypt_data
(
    EDGE_CRYPTO *edge_crypto,
    const SIZED_BUFFER *id,
    const SIZED_BUFFER *ct,
    const SIZED_BUFFER *iv,
    SIZED_BUFFER *pt
)
{
    int result;
    KEY_HANDLE key_handle;
    const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
    const HSM_CLIENT_KEY_INTERFACE *key_if = g_hsm_key_if;
    key_handle = store_if->hsm_client_store_open_key(edge_crypto->hsm_store_handle,
                                                     HSM_KEY_ENCRYPTION,
                                                     EDGELET_ENC_KEY_NAME);
    if (key_handle == NULL)
    {
        LOG_ERROR("Could not get encryption key by name '%s'", EDGELET_ENC_KEY_NAME);
        result = __FAILURE__;
    }
    else
    {
        int status = key_if->hsm_client_key_decrypt(key_handle, id, ct, iv, pt);
        if (status != 0)
        {
            LOG_ERROR("Error decrypting data. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        // always close the key handle
        status = store_if->hsm_client_store_close_key(edge_crypto->hsm_store_handle, key_handle);
        if (status != 0)
        {
            LOG_ERROR("Error closing key handle. Error code %d", status);
            result = __FAILURE__;
        }
    }

    return result;
}

static int edge_hsm_client_encrypt_data
(
    HSM_CLIENT_HANDLE handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *plaintext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *ciphertext
)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(identity))
    {
        LOG_ERROR("Invalid identity buffer provided");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(plaintext))
    {
        LOG_ERROR("Invalid plain text buffer provided");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(initialization_vector))
    {
        LOG_ERROR("Invalid initialization vector buffer provided");
        result = __FAILURE__;
    }
    else if (ciphertext == NULL)
    {
        LOG_ERROR("Invalid output cipher text buffer provided");
        result = __FAILURE__;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        result = encrypt_data(edge_crypto, identity, plaintext, initialization_vector, ciphertext);
    }

    return result;
}

static int edge_hsm_client_decrypt_data
(
    HSM_CLIENT_HANDLE handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *ciphertext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *plaintext
)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(identity))
    {
        LOG_ERROR("Invalid identity buffer provided");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(ciphertext))
    {
        LOG_ERROR("Invalid cipher text buffer provided");
        result = __FAILURE__;
    }
    else if (!validate_sized_buffer(initialization_vector))
    {
        LOG_ERROR("Invalid initialization vector buffer provided");
        result = __FAILURE__;
    }
    else if (plaintext == NULL)
    {
        LOG_ERROR("Invalid output plain text buffer provided");
        result = __FAILURE__;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        result = decrypt_data(edge_crypto, identity, ciphertext, initialization_vector, plaintext);
    }

    return result;
}

static int sign_using_private_key
(
    EDGE_CRYPTO *edge_crypto,
    const char* alias,
    const unsigned char* tbs,
    size_t tbs_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;
    KEY_HANDLE key_handle;
    const HSM_CLIENT_STORE_INTERFACE *store_if = g_hsm_store_if;
    const HSM_CLIENT_KEY_INTERFACE *key_if = g_hsm_key_if;
    key_handle = store_if->hsm_client_store_open_key(edge_crypto->hsm_store_handle,
                                                     HSM_KEY_ASYMMETRIC_PRIVATE_KEY,
                                                     alias);
    if (key_handle == NULL)
    {
        LOG_ERROR("Could not get private key for alias '%s'", alias);
        result = __FAILURE__;
    }
    else
    {
        int status = key_if->hsm_client_key_sign(key_handle, tbs, tbs_size, digest, digest_size);
        if (status != 0)
        {
            LOG_ERROR("Error signing data. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        // always close the key handle
        status = store_if->hsm_client_store_close_key(edge_crypto->hsm_store_handle, key_handle);
        if (status != 0)
        {
            LOG_ERROR("Error closing key handle. Error code %d", status);
            result = __FAILURE__;
        }
    }

    return result;
}

static int edge_hsm_client_crypto_sign_with_private_key
(
    HSM_CLIENT_HANDLE handle,
    const char* alias,
    const unsigned char* data,
    size_t data_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = __FAILURE__;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = __FAILURE__;
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid alias value");
        result = __FAILURE__;
    }
    else if ((data == NULL) || (data_size == 0))
    {
        LOG_ERROR("Invalid data and or data_size value");
        result = __FAILURE__;
    }
    else if ((digest == NULL) || (digest_size == NULL))
    {
        LOG_ERROR("Invalid digest and or digest_size value");
        result = __FAILURE__;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        result = sign_using_private_key(edge_crypto,
                                        alias,
                                        data,
                                        data_size,
                                        digest,
                                        digest_size);
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_crypto_get_certificate
(
    HSM_CLIENT_HANDLE handle,
    const char *alias
)
{
    CERT_INFO_HANDLE result;

    if (!g_is_crypto_initialized)
    {
        LOG_ERROR("hsm_client_crypto_init not called");
        result = NULL;
    }
    else if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = NULL;
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid alias value");
        result = NULL;
    }
    else
    {
        EDGE_CRYPTO *edge_crypto = (EDGE_CRYPTO*)handle;
        result = g_hsm_store_if->hsm_client_store_get_pki_cert(edge_crypto->hsm_store_handle,
                                                               alias);
    }

    return result;
}

static const HSM_CLIENT_CRYPTO_INTERFACE edge_hsm_crypto_interface =
{
    edge_hsm_client_crypto_create,
    edge_hsm_client_crypto_destroy,
    edge_hsm_client_get_random_bytes,
    edge_hsm_client_create_master_encryption_key,
    edge_hsm_client_destroy_master_encryption_key,
    edge_hsm_client_create_certificate,
    edge_hsm_client_destroy_certificate,
    edge_hsm_client_encrypt_data,
    edge_hsm_client_decrypt_data,
    edge_hsm_client_get_trust_bundle,
    edge_hsm_crypto_free_buffer,
    edge_hsm_client_crypto_sign_with_private_key,
    edge_hsm_client_crypto_get_certificate
};

const HSM_CLIENT_CRYPTO_INTERFACE* hsm_client_crypto_interface(void)
{
    return &edge_hsm_crypto_interface;
}
