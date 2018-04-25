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

static int edge_hsm_client_get_random_number_limits(HSM_CLIENT_HANDLE handle, size_t *min_random_num, size_t *max_random_num)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else
    {
        if (min_random_num) *min_random_num = 0;
        if (max_random_num) *max_random_num = RAND_MAX;
        result = 0;
    }
    return result;
}

static int edge_hsm_client_get_random_number(HSM_CLIENT_HANDLE handle, size_t *random_num)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (random_num == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else
    {
        *random_num = rand();
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

static CERT_HANDLE edge_hsm_client_create_certificate(HSM_CLIENT_HANDLE handle, CERT_PROPS_HANDLE certificate_props)
{
    CRYPTO_CERT *result;
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
        result = (CRYPTO_CERT*)malloc(sizeof(CRYPTO_CERT));
        assert(result != NULL);
        result->certificate = strdup(CERTIFICATE);
        result->certificate_chain = strdup(CERTIFICATE);
        result->private_key = strdup(PRIVATE_KEY);
        result->public_key = strdup(PUBLIC_KEY);
        assert(result->certificate != NULL);
        assert(result->certificate_chain != NULL);
        assert(result->private_key != NULL);
        assert(result->public_key != NULL);
    }

    return (CERT_HANDLE)result;
}

static void edge_hsm_client_destroy_certificate(HSM_CLIENT_HANDLE handle, CERT_HANDLE cert_handle)
{
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
    }
    else if (cert_handle == NULL)
    {
        LOG_ERROR("Invalid cert handle value specified");
    }
    else
    {
        CRYPTO_CERT *crypto_cert = cert_handle;
        if (crypto_cert->certificate) free(crypto_cert->certificate);
        if (crypto_cert->certificate_chain) free(crypto_cert->certificate_chain);
        if (crypto_cert->private_key) free(crypto_cert->private_key);
        if (crypto_cert->public_key) free(crypto_cert->public_key);
        memset(crypto_cert, 0 , sizeof(CRYPTO_CERT));
        free(crypto_cert);
    }
}

int get_certificate(CERT_HANDLE handle,
                    SIZED_BUFFER *cert_buffer,
                    CRYPTO_ENCODING *enc)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (cert_buffer == NULL)
    {
        LOG_ERROR("Invalid cert buffer specified");
        result = 1;
    }
    else if (enc == NULL)
    {
        LOG_ERROR("Invalid encoding parameter specified");
        result = 1;
    }
    else
    {
        CRYPTO_CERT *crypto_cert = (CRYPTO_CERT*)handle;
        cert_buffer->buffer = strdup(crypto_cert->certificate);
        cert_buffer->size = strlen(crypto_cert->certificate) + 1;
        *enc = PEM;
        result = 0;
    }
    return result;
}

int get_certificate_chain(CERT_HANDLE handle,
                          SIZED_BUFFER *cert_buffer,
                          CRYPTO_ENCODING *enc)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (cert_buffer == NULL)
    {
        LOG_ERROR("Invalid cert buffer specified");
        result = 1;
    }
    else if (enc == NULL)
    {
        LOG_ERROR("Invalid encoding parameter specified");
        result = 1;
    }
    else
    {
        CRYPTO_CERT *crypto_cert = (CRYPTO_CERT*)handle;
        cert_buffer->buffer = strdup(crypto_cert->certificate_chain);
        cert_buffer->size = strlen(crypto_cert->certificate_chain) + 1;
        *enc = PEM;
        result = 0;
    }
    return 0;
}

int get_public_key(CERT_HANDLE handle,
                   SIZED_BUFFER *key_buffer,
                   CRYPTO_ENCODING *enc)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (key_buffer == NULL)
    {
        LOG_ERROR("Invalid cert buffer specified");
        result = 1;
    }
    else if (enc == NULL)
    {
        LOG_ERROR("Invalid encoding parameter specified");
        result = 1;
    }
    else
    {
        CRYPTO_CERT *crypto_cert = (CRYPTO_CERT*)handle;
        key_buffer->buffer = strdup(crypto_cert->public_key);
        key_buffer->size = strlen(crypto_cert->public_key) + 1;
        *enc = PEM;
        result = 0;
    }
    return 0;
}

int get_private_key(CERT_HANDLE handle,
                    SIZED_BUFFER *key_buffer,
                    PRIVATE_KEY_TYPE *type,
                    CRYPTO_ENCODING *enc)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value specified");
        result = 1;
    }
    else if (key_buffer == NULL)
    {
        LOG_ERROR("Invalid cert buffer specified");
        result = 1;
    }
    else if (enc == NULL)
    {
        LOG_ERROR("Invalid encoding parameter specified");
        result = 1;
    }
    else
    {
        CRYPTO_CERT *crypto_cert = (CRYPTO_CERT*)handle;
        key_buffer->buffer = strdup(crypto_cert->private_key);
        key_buffer->size = strlen(crypto_cert->private_key) + 1;
        *enc = PEM;
        result = 0;
    }
    return 0;
}

static int edge_hsm_client_encrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *client_id,
                                        const SIZED_BUFFER *plaintext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *ciphertext)
{
    return 1;
}

static int edge_hsm_client_decrypt_data(HSM_CLIENT_HANDLE handle,
                                        const SIZED_BUFFER *client_id,
                                        const SIZED_BUFFER *plaintext,
                                        const SIZED_BUFFER *passphrase,
                                        const SIZED_BUFFER *initialization_vector,
                                        SIZED_BUFFER *ciphertext)
{
    return 1;
}

static const HSM_CLIENT_CRYPTO_INTERFACE edge_hsm_crypto_interface =
{
    edge_hsm_client_crypto_create,
    edge_hsm_client_crypto_destroy,
    edge_hsm_client_get_random_number_limits,
    edge_hsm_client_get_random_number,
    edge_hsm_client_create_master_encryption_key,
    edge_hsm_client_destroy_master_encryption_key,
    edge_hsm_client_create_certificate,
    edge_hsm_client_destroy_certificate,
    edge_hsm_client_encrypt_data,
    edge_hsm_client_decrypt_data,
    edge_hsm_crypto_free_buffer
};

const HSM_CLIENT_CRYPTO_INTERFACE* hsm_client_crypto_interface()
{
    return &edge_hsm_crypto_interface;
}
