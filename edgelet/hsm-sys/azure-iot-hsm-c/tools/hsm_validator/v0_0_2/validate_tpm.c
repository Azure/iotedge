// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string.h>
#include "test_utils.h"
#include "hsm_client_data.h"

const unsigned char identity_key[] = "a5551d09-82eb-42ec-8df5-56c244ea3ad0";
const size_t identity_key_size = sizeof(identity_key) - 1;

const unsigned char data_to_sign[] =
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nam elementum "
    "magna tristique justo dignissim aliquam. Aliquam ornare quam a pulvinar.";
const size_t data_to_sign_size = sizeof(data_to_sign) - 1;

const unsigned char derived_identity[] = "a/b/c/d";
const size_t derived_identity_size = sizeof(derived_identity) - 1;

const unsigned char expected_digest[] = {
    0xd7, 0xf2, 0xb7, 0x6c, 0x41, 0x69, 0xa3, 0x24, 0x20, 0xb9, 0x84, 0xf9,
    0xb1, 0xf3, 0xde, 0x85, 0x1e, 0x74, 0x7d, 0x3b, 0xda, 0xb7, 0xab, 0xf0,
    0x79, 0x6f, 0x23, 0x98, 0x22, 0xd3, 0xd9, 0xb7
};
const size_t expected_digest_size = sizeof(expected_digest);

const unsigned char ds_expected_digest[] = {
    0x97, 0x4b, 0x90, 0x08, 0x1e, 0xc1, 0x3c, 0x89, 0x7a, 0xe2, 0x37, 0x77,
    0x15, 0x40, 0x22, 0x1f, 0x53, 0x6b, 0x4c, 0x9a, 0xef, 0x58, 0x22, 0x6e,
    0xd8, 0x81, 0x15, 0xc5, 0x8c, 0xd5, 0xa8, 0xf5
};
const size_t ds_expected_digest_size = sizeof(expected_digest);


static int tpm_init_succeeds_when_called_after_deinit(void)
{
    ASSERT(hsm_client_tpm_init() == 0);
    hsm_client_tpm_deinit();
    ASSERT(hsm_client_tpm_init() == 0);
    hsm_client_tpm_deinit();
    return 0;
}

static int tpm_interface_pointer_is_always_the_same_after_init(void)
{
    ASSERT(hsm_client_tpm_init() == 0);

    const HSM_CLIENT_TPM_INTERFACE* if1 = hsm_client_tpm_interface();
    const HSM_CLIENT_TPM_INTERFACE* if2 = hsm_client_tpm_interface();

    ASSERT(if1 == if2);

    hsm_client_tpm_deinit();

    return 0;
}

static int tpm_interface_implements_all_functions(void)
{
    ASSERT(hsm_client_tpm_init() == 0);
    const HSM_CLIENT_TPM_INTERFACE* tpm_interface = hsm_client_tpm_interface();
    ASSERT(tpm_interface != NULL);
    ASSERT(tpm_interface->hsm_client_tpm_create != NULL);
    ASSERT(tpm_interface->hsm_client_tpm_destroy != NULL);
    ASSERT(tpm_interface->hsm_client_get_ek != NULL);
    ASSERT(tpm_interface->hsm_client_get_srk != NULL);
    ASSERT(tpm_interface->hsm_client_activate_identity_key != NULL);
    ASSERT(tpm_interface->hsm_client_sign_with_identity != NULL);
    hsm_client_tpm_deinit();
    return 0;
}

static int get_ek_returns_a_non_null_value(void)
{
    unsigned char* ekey = NULL;
    size_t ekey_size;

    ASSERT(hsm_client_tpm_init() == 0);

    const HSM_CLIENT_TPM_INTERFACE* tpm = hsm_client_tpm_interface();
    ASSERT(tpm != NULL);

    HSM_CLIENT_HANDLE client = tpm->hsm_client_tpm_create();
    ASSERT(client != NULL);

    ASSERT(tpm->hsm_client_get_ek(client, &ekey, &ekey_size) == 0);
    ASSERT(ekey_size != 0);
    ASSERT(ekey != NULL);
    ASSERT(ekey[0] != 0);

    tpm->hsm_client_free_buffer(ekey);
    tpm->hsm_client_tpm_destroy(client);
    hsm_client_tpm_deinit();

    return 0;
}

static int get_srk_returns_a_non_null_value(void)
{
    unsigned char* srkey = NULL;
    size_t srkey_size;

    ASSERT(hsm_client_tpm_init() == 0);

    const HSM_CLIENT_TPM_INTERFACE* tpm = hsm_client_tpm_interface();
    ASSERT(tpm != NULL);

    HSM_CLIENT_HANDLE client = tpm->hsm_client_tpm_create();
    ASSERT(client != NULL);

    ASSERT(tpm->hsm_client_get_srk(client, &srkey, &srkey_size) == 0);
    ASSERT(srkey_size != 0);
    ASSERT(srkey != NULL);
    ASSERT(srkey[0] != 0);

    tpm->hsm_client_free_buffer(srkey);
    tpm->hsm_client_tpm_destroy(client);
    hsm_client_tpm_deinit();

    return 0;
}

static int sign_with_identity_generates_expected_digest(void)
{
    unsigned char* digest = NULL;
    size_t digest_size;

    ASSERT(hsm_client_tpm_init() == 0);

    const HSM_CLIENT_TPM_INTERFACE* tpm = hsm_client_tpm_interface();
    ASSERT(tpm != NULL);

    HSM_CLIENT_HANDLE client = tpm->hsm_client_tpm_create();
    ASSERT(client != NULL);

    ASSERT(tpm->hsm_client_activate_identity_key(client, identity_key, identity_key_size) == 0);
    ASSERT(tpm->hsm_client_sign_with_identity(client, data_to_sign, data_to_sign_size, &digest, &digest_size) == 0);
    ASSERT(digest != NULL);
    ASSERT(digest_size == expected_digest_size);
    ASSERT(memcmp(expected_digest, digest, digest_size) == 0);

    tpm->hsm_client_free_buffer(digest);
    tpm->hsm_client_tpm_destroy(client);
    hsm_client_tpm_deinit();

    return 0;
}

static int derive_and_sign_with_identity_generates_expected_digest(void)
{
    unsigned char* digest = NULL;
    size_t digest_size;

    ASSERT(hsm_client_tpm_init() == 0);

    const HSM_CLIENT_TPM_INTERFACE* tpm = hsm_client_tpm_interface();
    ASSERT(tpm != NULL);

    HSM_CLIENT_HANDLE client = tpm->hsm_client_tpm_create();
    ASSERT(client != NULL);

    ASSERT(tpm->hsm_client_activate_identity_key(client, identity_key, identity_key_size) == 0);
    ASSERT(tpm->hsm_client_derive_and_sign_with_identity(
        client, data_to_sign, data_to_sign_size, derived_identity, derived_identity_size, &digest, &digest_size) == 0);
    ASSERT(digest != NULL);
    ASSERT(digest_size == ds_expected_digest_size);
    ASSERT(memcmp(ds_expected_digest, digest, digest_size) == 0);

    tpm->hsm_client_free_buffer(digest);
    tpm->hsm_client_tpm_destroy(client);
    hsm_client_tpm_deinit();

    return 0;
}

RECORD_RESULTS tpm_validation(void)
{
    INIT_RECORD;

    RECORD(tpm_init_succeeds_when_called_after_deinit());
    RECORD(tpm_interface_pointer_is_always_the_same_after_init());
    RECORD(tpm_interface_implements_all_functions());
    RECORD(get_ek_returns_a_non_null_value());
    RECORD(get_srk_returns_a_non_null_value());
    RECORD(sign_with_identity_generates_expected_digest());
    RECORD(derive_and_sign_with_identity_generates_expected_digest());

    RETURN_RECORD;
}