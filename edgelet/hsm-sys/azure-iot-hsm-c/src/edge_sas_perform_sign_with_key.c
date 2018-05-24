// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/hmacsha256.h"
#include "azure_c_shared_utility/macro_utils.h"

#include "hsm_log.h"

int perform_sign_with_key
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
        result = __FAILURE__;
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
            result =  __FAILURE__;
        }
        else if ((signed_payload_size = BUFFER_length(signed_payload_handle)) == 0)
        {
            LOG_ERROR("Error computing HMAC256SHA. Signature size is 0");
            result =  __FAILURE__;
        }
        else if ((src_digest = BUFFER_u_char(signed_payload_handle)) == NULL)
        {
            LOG_ERROR("Error obtaining underlying uchar buffer");
            result =  __FAILURE__;
        }
        else if ((result_digest = (unsigned char*)malloc(signed_payload_size)) == NULL)
        {
            LOG_ERROR("Error allocating memory for digest");
            result =  __FAILURE__;
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

