// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string.h>
#include "test_utils.h"
#include "hsm_client_data.h"

#define RAND_BYTES_BUF_SIZE 20
#define RAND_BYTES_FILL_SIZE (RAND_BYTES_BUF_SIZE / 2)
#define RAND_BYTES_FILL_OFFSET ((RAND_BYTES_BUF_SIZE - RAND_BYTES_FILL_SIZE) / 2)

static int crypto_init_succeeds_when_called_after_deinit(void)
{
    ASSERT(hsm_client_crypto_init() == 0);
    hsm_client_crypto_deinit();
    ASSERT(hsm_client_crypto_init() == 0);
    hsm_client_crypto_deinit();
    return 0;
}

static int crypto_interface_pointer_is_always_the_same_after_init(void)
{
    ASSERT(hsm_client_crypto_init() == 0);

    const HSM_CLIENT_CRYPTO_INTERFACE* if1 = hsm_client_crypto_interface();
    const HSM_CLIENT_CRYPTO_INTERFACE* if2 = hsm_client_crypto_interface();

    ASSERT(if1 == if2);

    hsm_client_crypto_deinit();

    return 0;
}

static int crypto_interface_implements_all_functions(void)
{
    ASSERT(hsm_client_crypto_init() == 0);
    const HSM_CLIENT_CRYPTO_INTERFACE* crypto_interface = hsm_client_crypto_interface();
    ASSERT(crypto_interface != NULL);
    ASSERT(crypto_interface->hsm_client_crypto_create != NULL);
    ASSERT(crypto_interface->hsm_client_crypto_destroy != NULL);
    ASSERT(crypto_interface->hsm_client_get_random_bytes != NULL);
    ASSERT(crypto_interface->hsm_client_create_master_encryption_key != NULL);
    ASSERT(crypto_interface->hsm_client_destroy_master_encryption_key != NULL);
    ASSERT(crypto_interface->hsm_client_create_certificate != NULL);
    ASSERT(crypto_interface->hsm_client_destroy_certificate != NULL);
    ASSERT(crypto_interface->hsm_client_encrypt_data != NULL);
    ASSERT(crypto_interface->hsm_client_decrypt_data != NULL);
    ASSERT(crypto_interface->hsm_client_get_trust_bundle != NULL);
    ASSERT(crypto_interface->hsm_client_free_buffer != NULL);
    hsm_client_crypto_deinit();
    return 0;
}

static int get_random_bytes_fills_buffer(void)
{
    unsigned char buffer[RAND_BYTES_BUF_SIZE];
    unsigned char accum[RAND_BYTES_BUF_SIZE] = {0};
    unsigned char mask[RAND_BYTES_BUF_SIZE] = {0};
    memset(mask + RAND_BYTES_FILL_OFFSET, 1, RAND_BYTES_FILL_SIZE);

    ASSERT(hsm_client_crypto_init() == 0);

    const HSM_CLIENT_CRYPTO_INTERFACE* crypto = hsm_client_crypto_interface();
    ASSERT(crypto != NULL);

    HSM_CLIENT_HANDLE client = crypto->hsm_client_crypto_create();
    ASSERT(client != NULL);

    // Ask hsm_client_get_random_bytes() to fill a buffer. Verify that the bytes
    // before and after the buffer are not written. Because the function could
    // randomly write NULL ('\0') to any one of the bytes in the buffer, call
    // the function 5 times and expect that each of the bytes will receive a
    // non-NULL value at least once.
    for (size_t pass = 0; pass < 5; ++pass)
    {
        memset(buffer, 0, sizeof(buffer));
        ASSERT(crypto->hsm_client_get_random_bytes(client,
            buffer + RAND_BYTES_FILL_OFFSET, RAND_BYTES_FILL_SIZE) == 0);

        for(size_t i = 0; i < sizeof(buffer); ++i)
        {
            if (buffer[i] != 0) accum[i] = buffer[i];
        }
    }

    size_t i = 0;
    for(; i < sizeof(buffer); ++i)
    {
        if (!accum[i] != !mask[i]) break;
    }

    ASSERT(i == sizeof(buffer));

    crypto->hsm_client_crypto_destroy(client);
    hsm_client_crypto_deinit();

    return 0;
}

static int TODO(void)
{
    printf("\nTODO: validate the following crypto functions:\n"
        "  create/destroy master encryption key\n"
        "  create/destroy certificate\n"
        "  encrypt/decrypt\n"
        "  get trust bundle\n\n");

    return 0;
}


RECORD_RESULTS crypto_validation(void)
{
    INIT_RECORD;

    RECORD(crypto_init_succeeds_when_called_after_deinit());
    RECORD(crypto_interface_pointer_is_always_the_same_after_init());
    RECORD(crypto_interface_implements_all_functions());
    RECORD(get_random_bytes_fills_buffer());
    RECORD(TODO());

    RETURN_RECORD;
}