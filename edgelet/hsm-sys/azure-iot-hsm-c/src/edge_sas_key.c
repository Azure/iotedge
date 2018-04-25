#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/hmacsha256.h"

#include "hsm_key.h"
#include "hsm_log.h"

struct SAS_KEY_TAG
{
    HSM_CLIENT_KEY_INTERFACE interface;
    unsigned char *key;
    size_t key_len;
};
typedef struct SAS_KEY_TAG SAS_KEY;

static int perform_sign_with_key
(
    const unsigned char* key,
    size_t key_len,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;
    BUFFER_HANDLE signed_payload_handle;

    if ((signed_payload_handle = BUFFER_new()) == NULL)
    {
        LOG_ERROR("Error allocating new buffer handle");
        result = 1;
    }
    else
    {
        size_t signed_payload_size;
        unsigned char *result_digest, *src_digest;
        int status = HMACSHA256_ComputeHash(key, key_len, data_to_be_signed,
                                            data_to_be_signed_size, signed_payload_handle);
        if (status != HMACSHA256_OK)
        {
            LOG_ERROR("Error computing HMAC256SHA signature");
            result = 1;
        }
        else if ((signed_payload_size = BUFFER_length(signed_payload_handle)) == 0)
        {
            LOG_ERROR("Error computing HMAC256SHA. Signature size is 0");
            result = 1;
        }
        else if ((src_digest = BUFFER_u_char(signed_payload_handle)) == NULL)
        {
            LOG_ERROR("Error obtaining underlying uchar buffer");
            result = 1;
        }
        else if ((result_digest = (unsigned char*)malloc(signed_payload_size)) == NULL)
        {
            LOG_ERROR("Error allocating memory for digest");
            result = 1;
        }
        else
        {
            memcpy(result_digest, src_digest, signed_payload_size);
            *digest = result_digest;
            *digest_size = signed_payload_size;
            result = 0;
        }
        BUFFER_delete(signed_payload_handle);
    }
    return result;
}

static int sas_key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;
    SAS_KEY *sas_key = (SAS_KEY*)key_handle;
    if (sas_key == NULL)
    {
        LOG_ERROR("Invalid key handle");
        result = 1;
    }
    else
    {
        result = perform_sign_with_key(sas_key->key,
                                       sas_key->key_len,
                                       data_to_be_signed,
                                       data_to_be_signed_size,
                                       digest,
                                       digest_size);
    }
    return result;
}

int sas_key_derive_and_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;
    unsigned char* derived_key = NULL;
    size_t derived_key_size = 0;
    SAS_KEY* sas_key = (SAS_KEY*)key_handle;

    if ((result = perform_sign_with_key(sas_key->key, sas_key->key_len,
                                        identity, identity_size,
                                        &derived_key, &derived_key_size)) != 0)
    {
        LOG_ERROR("Error deriving key for identity %s", identity);
    }
    else
    {
        if ((result = perform_sign_with_key(derived_key, derived_key_size,
                                            data_to_be_signed, data_to_be_signed_size,
                                            digest, digest_size)) != 0)
        {
            LOG_ERROR("Error signing payload for identity %s", identity);
        }
        free(derived_key);
    }
    return result;
}

static int sas_key_verify(KEY_HANDLE key_handle,
                           const unsigned char* data_to_be_signed,
                           size_t data_to_be_signed_size,
                           const unsigned char* signature_to_verify,
                           size_t signature_to_verify_size,
                           bool* verification_status)
{
    LOG_ERROR("Shared access key verify operation not supported");
    *verification_status = false;
    return 1;
}

static int sas_key_derive_and_verify(KEY_HANDLE key_handle,
                                      const unsigned char* data_to_be_signed,
                                      size_t data_to_be_signed_size,
                                      const unsigned char* identity,
                                      size_t identity_size,
                                      const unsigned char* signature_to_verify,
                                      size_t signature_to_verify_size,
                                      bool* verification_status)
{
    LOG_ERROR("Shared access key derive and verify operation not supported");
    *verification_status = false;
    return 1;
}

static int sas_key_encrypt(KEY_HANDLE key_handle,
                            const SIZED_BUFFER *identity,
                            const SIZED_BUFFER *plaintext,
                            const SIZED_BUFFER *passphrase,
                            const SIZED_BUFFER *initialization_vector,
                            SIZED_BUFFER *ciphertext)
{
    LOG_ERROR("Shared access key encrypt operation not supported");
    ciphertext->buffer = NULL;
    ciphertext->size = 0;
    return 1;
}

static int sas_key_decrypt(KEY_HANDLE key_handle,
                            const SIZED_BUFFER *identity,
                            const SIZED_BUFFER *ciphertext,
                            const SIZED_BUFFER *passphrase,
                            const SIZED_BUFFER *initialization_vector,
                            SIZED_BUFFER *plaintext)
{
    LOG_ERROR("Shared access key decrypt operation not supported");
    plaintext->buffer = NULL;
    plaintext->size = 0;
    return 1;
}

KEY_HANDLE create_sas_key(const unsigned char* key, size_t key_len)
{
    SAS_KEY* sas_key;
    if ((key == NULL) || (key_len == 0))
    {
        LOG_ERROR("Invalid SAS key create parameters");
        sas_key = NULL;
    }
    else
    {
        sas_key = (SAS_KEY*)malloc(sizeof(SAS_KEY));
        if (sas_key == NULL)
        {
            LOG_ERROR("Could not allocate memory for SAS_KEY");
        }
        else if ((sas_key->key = (unsigned char*)malloc(key_len)) == NULL)
        {
            LOG_ERROR("Could not allocate memory for sas key creation");
            free(sas_key);
            sas_key = NULL;
        }
        else
        {
            sas_key->interface.hsm_client_key_sign = sas_key_sign;
            sas_key->interface.hsm_client_key_derive_and_sign = sas_key_derive_and_sign;
            sas_key->interface.hsm_client_key_verify = sas_key_verify;
            sas_key->interface.hsm_client_key_derive_and_verify = sas_key_derive_and_verify;
            sas_key->interface.hsm_client_key_encrypt = sas_key_encrypt;
            sas_key->interface.hsm_client_key_decrypt = sas_key_decrypt;
            memcpy(sas_key->key, key, key_len);
            sas_key->key_len = key_len;
        }
    }
    return (KEY_HANDLE)sas_key;
}

void destroy_sas_key(KEY_HANDLE key_handle)
{
    SAS_KEY *sas_key = (SAS_KEY*)key_handle;
    if (sas_key != NULL)
    {
        if (sas_key->key != NULL)
        {
            free(sas_key->key);
        }
        free(sas_key);
    }
}
