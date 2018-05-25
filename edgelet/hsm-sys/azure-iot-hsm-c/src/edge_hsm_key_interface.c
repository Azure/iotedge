#include <stdbool.h>
#include "azure_c_shared_utility/gballoc.h"

#include "hsm_client_store.h"
#include "hsm_key.h"
#include "hsm_log.h"

static int perform_sign
(
    bool do_derive_and_sign,
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result = 0;
    if (digest == NULL)
    {
        LOG_ERROR("Invalid digest parameter");
        result = 1;
    }
    else
    {
        *digest = NULL;
    }
    if (digest_size == NULL)
    {
        LOG_ERROR("Invalid digest size parameter");
        result = 1;
    }
    else
    {
        *digest_size = 0;
    }
    if (result == 0)
    {
        if (key_handle == NULL)
        {
            LOG_ERROR("Invalid key handle parameter");
            result = 1;
        }
        else if (data_to_be_signed == NULL)
        {
            LOG_ERROR("Invalid data to be signed parameter");
            result = 1;
        }
        else if (data_to_be_signed_size == 0)
        {
            LOG_ERROR("Data to be signed size is 0");
            result = 1;
        }
        else if (do_derive_and_sign)
        {
            if (identity == NULL)
            {
                LOG_ERROR("Invalid identity parameter");
                result = 1;
            }
            else if (identity_size == 0)
            {
                LOG_ERROR("Invalid identity size parameter");
                result = 1;
            }
            else
            {
                result = key_derive_and_sign(key_handle, data_to_be_signed, data_to_be_signed_size,
                                             identity, identity_size, digest, digest_size);
            }
        }
        else
        {
            result = key_sign(key_handle, data_to_be_signed, data_to_be_signed_size, digest, digest_size);
        }
    }

    return result;
}

static int edge_hsm_client_key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    return perform_sign(false, key_handle, data_to_be_signed, data_to_be_signed_size,
                        NULL, 0, digest, digest_size);
}

static int edge_hsm_client_key_derive_and_sign
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
    return perform_sign(true, key_handle, data_to_be_signed, data_to_be_signed_size,
                        identity, identity_size, digest, digest_size);
}

static int enc_dec_validation(KEY_HANDLE key_handle,
                              const SIZED_BUFFER *identity,
                              const SIZED_BUFFER *plaintext,
                              const SIZED_BUFFER *iv,
                              const SIZED_BUFFER *ciphertext)
{
    int result;
    if ((identity == NULL) || (identity->buffer == NULL) || (identity->size == 0))
    {
        LOG_ERROR("Invalid identity parameter");
        result = 1;
    }
    else if ((plaintext == NULL) || (plaintext->buffer == NULL) || (plaintext->size == 0))
    {
        LOG_ERROR("Invalid plaintext parameter");
        result = 1;
    }
    else if ((iv == NULL) || (iv->buffer == NULL) || (iv->size == 0))
    {
        LOG_ERROR("Invalid initialization vector parameter");
        result = 1;
    }
    else if ((ciphertext == NULL) || (ciphertext->buffer == NULL) || (ciphertext->size == 0))
    {
        LOG_ERROR("Invalid ciphertext parameter");
        result = 1;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int perform_verify
(
    bool do_derive_and_verify,
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    const unsigned char* signature_to_verify,
    size_t signature_to_verify_size,
    bool *verification_status
)
{
    int result = 0;
    if (verification_status == NULL)
    {
        LOG_ERROR("Invalid verification status parameter");
        result = 1;
    }
    else
    {
        *verification_status = false;
    }

    if (result == 0)
    {
        if (key_handle == NULL)
        {
            LOG_ERROR("Invalid key handle parameter");
            result = 1;
        }
        else if (data_to_be_signed == NULL)
        {
            LOG_ERROR("Invalid data to be signed parameter");
            result = 1;
        }
        else if (data_to_be_signed_size == 0)
        {
            LOG_ERROR("Data to be signed size is 0");
            result = 1;
        }
        else if (signature_to_verify == NULL)
        {
            LOG_ERROR("Invalid signature parameter");
            result = 1;
        }
        else if (signature_to_verify_size == 0)
        {
            LOG_ERROR("Invalid signature size parameter");
            result = 1;
        }
        else if (do_derive_and_verify)
        {
            if (identity == NULL)
            {
                LOG_ERROR("Invalid identity parameter");
                result = 1;
            }
            else if (identity_size == 0)
            {
                LOG_ERROR("Invalid identity size parameter");
                result = 1;
            }
            else
            {
                result = key_derive_and_verify(key_handle,
                                               data_to_be_signed, data_to_be_signed_size,
                                               identity, identity_size,
                                               signature_to_verify, signature_to_verify_size,
                                               verification_status);
            }
        }
        else
        {
            result = key_verify(key_handle, data_to_be_signed, data_to_be_signed_size,
                                signature_to_verify, signature_to_verify_size,
                                verification_status);

        }
    }

    return result;
}

static int edge_hsm_client_key_verify(KEY_HANDLE key_handle,
                                      const unsigned char* data_to_be_signed,
                                      size_t data_to_be_signed_size,
                                      const unsigned char* signature_to_verify,
                                      size_t signature_to_verify_size,
                                      bool* verification_status)
{
    return perform_verify(false, key_handle,
                          data_to_be_signed, data_to_be_signed_size,
                          NULL, 0, signature_to_verify, signature_to_verify_size,
                          verification_status);
}

int edge_hsm_client_key_derive_and_verify(KEY_HANDLE key_handle,
                                          const unsigned char* data_to_be_signed,
                                          size_t data_to_be_signed_size,
                                          const unsigned char* identity,
                                          size_t identity_size,
                                          const unsigned char* signature_to_verify,
                                          size_t signature_to_verify_size,
                                          bool* verification_status)
{
    return perform_verify(true, key_handle,
                          data_to_be_signed, data_to_be_signed_size,
                          identity, identity_size,
                          signature_to_verify, signature_to_verify_size,
                          verification_status);
}

static int edge_hsm_client_key_encrypt(KEY_HANDLE key_handle,
                                       const SIZED_BUFFER *identity,
                                       const SIZED_BUFFER *plaintext,
                                       const SIZED_BUFFER *iv,
                                       SIZED_BUFFER *ciphertext)
{
    int result = enc_dec_validation(key_handle, identity, plaintext, iv, ciphertext);
    if (result == 0)
    {
        result = key_encrypt(key_handle, identity, plaintext, iv, ciphertext);
    }

    return result;
}

static int edge_hsm_client_key_decrypt(KEY_HANDLE key_handle,
                                       const SIZED_BUFFER *identity,
                                       const SIZED_BUFFER *ciphertext,
                                       const SIZED_BUFFER *iv,
                                       SIZED_BUFFER *plaintext)
{
    int result = enc_dec_validation(key_handle, identity, plaintext, iv, ciphertext);
    if (result == 0)
    {
        result = key_decrypt(key_handle, identity, ciphertext, iv, plaintext);
    }

    return result;
}

static const HSM_CLIENT_KEY_INTERFACE edge_hsm_key_interface =
{
    edge_hsm_client_key_sign,
    edge_hsm_client_key_derive_and_sign,
    edge_hsm_client_key_verify,
    edge_hsm_client_key_derive_and_verify,
    edge_hsm_client_key_encrypt,
    edge_hsm_client_key_decrypt
};

const HSM_CLIENT_KEY_INTERFACE* hsm_client_key_interface(void)
{
    return &edge_hsm_key_interface;
}
