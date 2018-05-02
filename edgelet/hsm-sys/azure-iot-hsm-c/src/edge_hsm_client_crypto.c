#include <assert.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "hsm_client_data.h"
#include "hsm_log.h"

static const char* const CERTIFICATE = "-----BEGIN CERTIFICATE-----""\n"
"MIIBbDCCARGgAwIBAgIDIj6BMAoGCCqGSM49BAMCMD4xEjAQBgNVBAoMCW1pY3Jv""\n"
"c29mdDELMAkGA1UEBhMCVVMxGzAZBgNVBAMMEmlvdGh1Yi1oc20tZXhhbXBsZTAe""\n"
"Fw0xODAzMjExNTI3MzJaFw0xODA0MjAxNTI3MzJaMD4xEjAQBgNVBAoMCW1pY3Jv""\n"
"c29mdDELMAkGA1UEBhMCVVMxGzAZBgNVBAMMEmlvdGh1Yi1oc20tZXhhbXBsZTBZ""\n"
"MBMGByqGSM49AgEGCCqGSM49AwEHA0IABPKKgaOHPnUH1iPI6+PCSoU1rc9tbXMa""\n"
"U6vhyNIsijIyE2uBkWKMAAL6SHdJNeRGj/d+zxzsqIuIPDEV+alwNfQwCgYIKoZI""\n"
"zj0EAwIDSQAwRgIhAMf0x3q2TlmLy9RixcANJC8UiK3mnoTApY8LVL1Mn5KiAiEA""\n"
"9RI4qMtjYPsvCREcR8aOoSdo9f+MNUz3sGQBDLmlRv0=""\n"
"-----END CERTIFICATE-----";

static const char* const PRIVATE_KEY = "-----BEGIN PRIVATE KEY-----""\n"
"MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg+oE7K7T/aNysJhi8""\n"
"WHjLK5CSxnq4V+G9NhMxjSgeLQihRANCAATyioGjhz51B9YjyOvjwkqFNa3PbW1z""\n"
"GlOr4cjSLIoyMhNrgZFijAAC+kh3STXkRo/3fs8c7KiLiDwxFfmpcDX0""\n"
"-----END PRIVATE KEY-----";

static const char* const PUBLIC_KEY = "-----BEGIN PUBLIC KEY-----""\n"
"MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg+oE7K7T/aNysJhi8""\n"
"WHjLK5CSxnq4V+G9NhMxjSgeLQihRANCAATyioGjhz51B9YjyOvjwkqFNa3PbW1z""\n"
"GlOr4cjSLIoyMhNrgZFijAAC+kh3STXkRo/3fs8c7KiLiDwxFfmpcDX0""\n"
"-----END PRIVATE KEY-----";

struct HSM_CERTIFICATE_TAG
{
    char* certificate;
    char* private_key;
    char* public_key;
    char* certificate_chain;
};
typedef struct HSM_CERTIFICATE_TAG CRYPTO_CERT;

int hsm_client_crypto_init()
{
    srand(time(NULL));
    return 0;
}

void hsm_client_crypto_deinit()
{

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
    result = (HSM_CLIENT_HANDLE)0x12345;
    return (HSM_CLIENT_HANDLE)result;
}

static void edge_hsm_client_crypto_destroy(HSM_CLIENT_HANDLE handle)
{

}

static int edge_hsm_client_get_random_bytes(HSM_CLIENT_HANDLE handle, unsigned char* rand_buffer, size_t num_bytes)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (rand_buffer == NULL)
    {
        LOG_ERROR("Invalid buffer specified");
        result = 1;
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
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else
    {
        result = 0;
    }
    return result;
}

static int edge_hsm_client_destroy_master_encryption_key(HSM_CLIENT_HANDLE handle)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else
    {
        result = 0;
    }
    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_create_certificate(HSM_CLIENT_HANDLE handle, CERT_PROPS_HANDLE certificate_props)
{
    CERT_INFO_HANDLE result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = NULL;
    }
    else if (certificate_props == NULL)
    {
        LOG_ERROR("Invalid cert props value specified");
        result = NULL;
    }
    else
    {
        result = certificate_info_create(CERTIFICATE, PRIVATE_KEY, sizeof(PRIVATE_KEY), PRIVATE_KEY_PAYLOAD);
        assert(result != NULL);
    }

    return result;
}

static void edge_hsm_client_destroy_certificate(HSM_CLIENT_HANDLE handle, const char* alias)
{
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid cert bundle alias specified");
    }
}

static int edge_hsm_client_encrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *identity,
                                        const SIZED_BUFFER *plaintext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *ciphertext)
{
    return 1;
}

static int edge_hsm_client_decrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *identity,
                                        const SIZED_BUFFER *plaintext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *ciphertext)
{
    return 1;
}

static CERT_INFO_HANDLE edge_hsm_client_get_trust_bundle(HSM_CLIENT_HANDLE handle)
{
    return NULL;
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
