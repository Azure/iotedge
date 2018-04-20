#include "azure_c_shared_utility/gballoc.h"

#include "hsm_client_store.h"
#include "hsm_key.h"
#include "hsm_log.h"

static int perform_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size,
    int do_derive_and_sign
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
    return perform_sign(key_handle, data_to_be_signed, data_to_be_signed_size,
                        NULL, 0, digest, digest_size, 0);
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
    return perform_sign(key_handle, data_to_be_signed, data_to_be_signed_size,
                        identity, identity_size, digest, digest_size, 1);
}

static const HSM_CLIENT_KEY_INTERFACE edge_hsm_key_interface =
{
    edge_hsm_client_key_sign,
    edge_hsm_client_key_derive_and_sign
};

const HSM_CLIENT_KEY_INTERFACE* hsm_client_key_interface(void)
{
    return &edge_hsm_key_interface;
}
