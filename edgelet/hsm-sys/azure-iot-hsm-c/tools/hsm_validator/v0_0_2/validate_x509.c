// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <string.h>
#include "test_utils.h"
#include "hsm_client_data.h"

#define TEST_VALIDITY 1000

static int x509_init_succeeds_when_called_after_deinit(void)
{
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);
    hsm_client_x509_deinit();
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);
    hsm_client_x509_deinit();
    return 0;
}

static int x509_interface_pointer_is_always_the_same_after_init(void)
{
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);

    const HSM_CLIENT_X509_INTERFACE* if1 = hsm_client_x509_interface();
    const HSM_CLIENT_X509_INTERFACE* if2 = hsm_client_x509_interface();

    ASSERT(if1 == if2);

    hsm_client_x509_deinit();

    return 0;
}

static int x509_interface_implements_all_functions(void)
{
    const HSM_CLIENT_X509_INTERFACE* x509_interface = hsm_client_x509_interface();
    ASSERT(x509_interface != NULL);
    ASSERT(x509_interface->hsm_client_x509_create != NULL);
    ASSERT(x509_interface->hsm_client_x509_destroy != NULL);
    ASSERT(x509_interface->hsm_client_get_cert != NULL);
    ASSERT(x509_interface->hsm_client_get_key != NULL);
    ASSERT(x509_interface->hsm_client_get_common_name != NULL);
    ASSERT(x509_interface->hsm_client_free_buffer != NULL);
    hsm_client_x509_deinit();
    return 0;
}

static int get_cert_returns_a_non_null_value(void)
{
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);

    const HSM_CLIENT_X509_INTERFACE* x509 = hsm_client_x509_interface();
    ASSERT(x509 != NULL);

    HSM_CLIENT_HANDLE client = x509->hsm_client_x509_create();
    ASSERT(client != NULL);

    char* cert = x509->hsm_client_get_cert(client);
    ASSERT(cert != NULL);
    ASSERT(strlen(cert) != 0);
    ASSERT(cert[0] != 0);

    x509->hsm_client_x509_destroy(client);
    hsm_client_x509_deinit();

    return 0;
}

static int get_key_returns_a_non_null_value(void)
{
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);

    const HSM_CLIENT_X509_INTERFACE* x509 = hsm_client_x509_interface();
    ASSERT(x509 != NULL);

    HSM_CLIENT_HANDLE client = x509->hsm_client_x509_create();
    ASSERT(client != NULL);

    char* key = x509->hsm_client_get_key(client);
    ASSERT(key != NULL);
    ASSERT(strlen(key) != 0);
    ASSERT(key[0] != 0);

    x509->hsm_client_x509_destroy(client);
    hsm_client_x509_deinit();

    return 0;
}

static int get_common_name_returns_a_non_null_value(void)
{
    ASSERT(hsm_client_x509_init(TEST_VALIDITY) == 0);

    const HSM_CLIENT_X509_INTERFACE* x509 = hsm_client_x509_interface();
    ASSERT(x509 != NULL);

    HSM_CLIENT_HANDLE client = x509->hsm_client_x509_create();
    ASSERT(client != NULL);

    char* name = x509->hsm_client_get_common_name(client);
    ASSERT(name != NULL);
    ASSERT(strlen(name) != 0);
    ASSERT(name[0] != 0);

    x509->hsm_client_x509_destroy(client);
    hsm_client_x509_deinit();

    return 0;
}

RECORD_RESULTS x509_validation(void)
{
    INIT_RECORD;

    RECORD(x509_init_succeeds_when_called_after_deinit());
    RECORD(x509_interface_pointer_is_always_the_same_after_init());
    RECORD(x509_interface_implements_all_functions());
    RECORD(get_cert_returns_a_non_null_value());
    RECORD(get_key_returns_a_non_null_value());
    RECORD(get_common_name_returns_a_non_null_value());

    RETURN_RECORD;
}