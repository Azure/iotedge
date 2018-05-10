#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_data.h"
#include "hsm_client_store.h"
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

int hsm_client_crypto_init(void)
{
    int result;
    if (!g_is_crypto_initialized)
    {
        int status;
        const HSM_CLIENT_STORE_INTERFACE* store_if;
        const HSM_CLIENT_KEY_INTERFACE* key_if;
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
        else if ((status = store_if->hsm_client_store_create(EDGE_STORE_NAME)) != 0)
        {
            LOG_ERROR("Could not create store. Error code %d", status);
            result = __FAILURE__;
        }
        else
        {
            g_is_crypto_initialized = true;
            g_hsm_store_if = store_if;
            g_hsm_key_if = key_if;
			srand(time(NULL));
            result = 0;
        }
    }
    else
    {
        LOG_ERROR("Re-initializing crypto interface without de-initializing");
        result = __FAILURE__;
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
        g_hsm_store_if = NULL;
        g_hsm_key_if = NULL;
        g_is_crypto_initialized = false;
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
        // todo use OpenSSL RAND_BUFFER and CNG rand API
        size_t count;
        for (count = 0; count < num_bytes; count++)
        {
            *rand_buffer++ = rand() % 256;
        }
        result = 0;
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
        LOG_ERROR("API unsupported");
        result = __FAILURE__;
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
        LOG_ERROR("API unsupported");
        result = __FAILURE__;
    }
    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_create_certificate(HSM_CLIENT_HANDLE handle, CERT_PROPS_HANDLE certificate_props)
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
            LOG_ERROR("Could not destroy certificate in the store for alias: %s", alias);
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

static int edge_hsm_client_encrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *identity,
                                        const SIZED_BUFFER *plaintext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *ciphertext)
{
    int result;
    if (!validate_sized_buffer(identity))
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
    else if ((passphrase != NULL) && !validate_sized_buffer(passphrase))
    {
        LOG_ERROR("Invalid passphrase buffer provided");
        result = __FAILURE__;
    }
    else if (ciphertext == NULL)
    {
        LOG_ERROR("Invalid output cipher text buffer provided");
        result = __FAILURE__;
    }
    else if ((ciphertext->buffer = (unsigned char*)malloc(plaintext->size)) == NULL)
    {
        LOG_ERROR("Could not allocate memory for the output cipher text");
        result = __FAILURE__;
    }
    else
    {
        memcpy(ciphertext->buffer, plaintext->buffer, plaintext->size);
        ciphertext->size = plaintext->size;
        result = 0;
    }
    return result;
}

static int edge_hsm_client_decrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *identity,
                                        const SIZED_BUFFER *ciphertext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *plaintext)
{
    int result;
    if (!validate_sized_buffer(identity))
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
    else if ((passphrase != NULL) && !validate_sized_buffer(passphrase))
    {
        LOG_ERROR("Invalid passphrase buffer provided");
        result = __FAILURE__;
    }
    else if (plaintext == NULL)
    {
        LOG_ERROR("Invalid output plain text buffer provided");
        result = __FAILURE__;
    }
    else if ((plaintext->buffer = (unsigned char*)malloc(ciphertext->size)) == NULL)
    {
        LOG_ERROR("Could not allocate memory for the output plain text");
        result = __FAILURE__;
    }
    else
    {
        memcpy(plaintext->buffer, ciphertext->buffer, ciphertext->size);
        plaintext->size = ciphertext->size;
        result = 0;
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
    edge_hsm_crypto_free_buffer
};

const HSM_CLIENT_CRYPTO_INTERFACE* hsm_client_crypto_interface(void)
{
    return &edge_hsm_crypto_interface;
}
