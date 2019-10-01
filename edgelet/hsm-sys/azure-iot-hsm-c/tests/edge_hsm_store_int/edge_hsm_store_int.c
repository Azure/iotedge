// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/hmacsha256.h"
#include "azure_c_shared_utility/azure_base64.h"
#include "azure_c_shared_utility/agenttime.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/xlogging.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//#############################################################################
// Interface(s) under test
//#############################################################################

#include "hsm_client_store.h"

//#############################################################################
// Test defines and data
//#############################################################################
#define EDGE_STORE_NAME "blah"
#define TEST_DATA_TO_BE_SIGNED "The quick brown fox jumped over the lazy dog"
#define TEST_KEY_BASE64 "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA="
#define HMAC_SHA256_SIZE 256

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

// 90 days.
static const uint64_t TEST_CA_VALIDITY =  90 * 24 * 3600;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_homedir(void)
{
    TEST_IOTEDGE_HOMEDIR = hsm_test_util_create_temp_dir(&TEST_IOTEDGE_HOMEDIR_GUID);
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR_GUID, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR, "Line:" TOSTRING(__LINE__));

    printf("Temp dir created: [%s]\r\n", TEST_IOTEDGE_HOMEDIR);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);
}

static void test_helper_teardown_homedir(void)
{
    if ((TEST_IOTEDGE_HOMEDIR != NULL) && (TEST_IOTEDGE_HOMEDIR_GUID != NULL))
    {
        hsm_test_util_delete_dir(TEST_IOTEDGE_HOMEDIR_GUID);
        free(TEST_IOTEDGE_HOMEDIR);
        TEST_IOTEDGE_HOMEDIR = NULL;
        free(TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_IOTEDGE_HOMEDIR_GUID = NULL;
    }
}

static CERT_PROPS_HANDLE test_helper_create_certificate_props
(
    const char *common_name,
    const char *alias,
    const char *issuer_alias,
    CERTIFICATE_TYPE type,
    uint64_t validity
)
{
    CERT_PROPS_HANDLE cert_props_handle = cert_properties_create();
    ASSERT_IS_NOT_NULL(cert_props_handle, "Line:" TOSTRING(__LINE__));
    set_validity_seconds(cert_props_handle, validity);
    set_common_name(cert_props_handle, common_name);
    set_country_name(cert_props_handle, "US");
    set_state_name(cert_props_handle, "Test State");
    set_locality(cert_props_handle, "Test Locality");
    set_organization_name(cert_props_handle, "Test Org");
    set_organization_unit(cert_props_handle, "Test Org Unit");
    set_certificate_type(cert_props_handle, type);
    set_issuer_alias(cert_props_handle, issuer_alias);
    set_alias(cert_props_handle, alias);
    return cert_props_handle;
}

static BUFFER_HANDLE test_helper_base64_converter(const char* input)
{
    BUFFER_HANDLE result = Azure_Base64_Decode(input);
    ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
    size_t out_len = BUFFER_length(result);
    ASSERT_ARE_NOT_EQUAL(size_t, 0, out_len, "Line:" TOSTRING(__LINE__));
    unsigned char* out_buffer = BUFFER_u_char(result);
    ASSERT_IS_NOT_NULL(out_buffer, "Line:" TOSTRING(__LINE__));
    return result;
}

static BUFFER_HANDLE test_helper_compute_hmac
(
    BUFFER_HANDLE key_handle,
    const unsigned char* input,
    size_t input_size
)
{
    int status;
    BUFFER_HANDLE result = BUFFER_new();
    ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
    status = HMACSHA256_ComputeHash(BUFFER_u_char(key_handle), BUFFER_length(key_handle),
                                    input, input_size, result);
    ASSERT_ARE_EQUAL(int, (int)HMACSHA256_OK, status, "Line:" TOSTRING(__LINE__));
    return result;
}

static void test_helper_sas_key_sign
(
    HSM_CLIENT_STORE_HANDLE store_handle,
    const char *key_name,
    const unsigned char *derived_identity,
    size_t derived_identity_size,
    const unsigned char *data,
    size_t data_len,
    BUFFER_HANDLE hash
)
{
    int status;
    unsigned char *digest;
    size_t digest_size;
    const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
    const HSM_CLIENT_KEY_INTERFACE *key_if = hsm_client_key_interface();
    KEY_HANDLE key_handle = store_if->hsm_client_store_open_key(store_handle,
                                                                HSM_KEY_SAS,
                                                                key_name);
    ASSERT_IS_NOT_NULL(key_handle, "Line:" TOSTRING(__LINE__));
    if (derived_identity != NULL)
    {
        status = key_if->hsm_client_key_derive_and_sign(key_handle,
                                                        data,
                                                        data_len,
                                                        derived_identity,
                                                        derived_identity_size,
                                                        &digest,
                                                        &digest_size);
    }
    else
    {
        status = key_if->hsm_client_key_sign(key_handle,
                                             data,
                                             data_len,
                                             &digest,
                                             &digest_size);
    }
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    status = BUFFER_build(hash, digest, digest_size);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    free(digest);
    status = store_if->hsm_client_store_close_key(store_handle, key_handle);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
}

static void test_helper_cert_key_sign
(
    HSM_CLIENT_STORE_HANDLE store_handle,
    const char *key_name,
    const unsigned char *data,
    size_t data_len
)
{
    int status;
    unsigned char *digest;
    size_t digest_size;
    const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
    const HSM_CLIENT_KEY_INTERFACE *key_if = hsm_client_key_interface();
    KEY_HANDLE key_handle = store_if->hsm_client_store_open_key(store_handle,
                                                                HSM_KEY_ASYMMETRIC_PRIVATE_KEY,
                                                                key_name);
    ASSERT_IS_NOT_NULL(key_handle, "Line:" TOSTRING(__LINE__));
    status = key_if->hsm_client_key_sign(key_handle,
                                         data,
                                         data_len,
                                         &digest,
                                         &digest_size);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(digest, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE((digest_size >= HMAC_SHA256_SIZE), "Line:" TOSTRING(__LINE__));
    free(digest);
    status = store_if->hsm_client_store_close_key(store_handle, key_handle);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_store_int_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        test_helper_teardown_homedir();
        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(create_destroy_smoke)
    {
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));
        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

    TEST_FUNCTION(open_close_smoke)
    {
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_STORE_HANDLE store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

    TEST_FUNCTION(insert_remove_sas_key_smoke)
    {
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_STORE_HANDLE store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_remove_key(store_handle, HSM_KEY_SAS, "bad_sas_key_name");
        ASSERT_ARE_NOT_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_insert_sas_key(store_handle, "my_sas_key", (unsigned char*)"ABCD", 5);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_insert_sas_key(store_handle, "my_sas_key", (unsigned char*)"1234", 5);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_remove_key(store_handle, HSM_KEY_SAS, "my_sas_key");
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

    TEST_FUNCTION(insert_overwrite_sign_remove_sas_key_smoke)
    {
        // arrange
        int result;
        unsigned char test_data_to_be_signed[] = TEST_DATA_TO_BE_SIGNED;
        size_t test_data_to_be_signed_size = sizeof(test_data_to_be_signed);
        char test_key[] = TEST_KEY_BASE64;
        BUFFER_HANDLE decoded_key = test_helper_base64_converter(test_key);

        // compute expected result
        BUFFER_HANDLE test_expected_digest = test_helper_compute_hmac(decoded_key,
                                                                      test_data_to_be_signed,
                                                                      test_data_to_be_signed_size);

        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_STORE_HANDLE store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        // act
        BUFFER_HANDLE test_output_digest = BUFFER_new();
        ASSERT_IS_NOT_NULL(test_output_digest, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_insert_sas_key(store_handle, "my_sas_key", (unsigned char*)"ABCD", 5);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
        result = store_if->hsm_client_store_insert_sas_key(store_handle, "my_sas_key",
                                                           BUFFER_u_char(decoded_key),
                                                           BUFFER_length(decoded_key));
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        test_helper_sas_key_sign(store_handle,
                                 "my_sas_key",
                                 NULL, 0,
                                 test_data_to_be_signed, test_data_to_be_signed_size,
                                 test_output_digest);

        // assert
        STRING_HANDLE expected_buffer = Azure_Base64_Encode(test_expected_digest);
        STRING_HANDLE result_buffer = Azure_Base64_Encode(test_output_digest);
        printf("Expected: %s\r\n", STRING_c_str(expected_buffer));
        printf("Got Result: %s\r\n", STRING_c_str(result_buffer));
        ASSERT_ARE_EQUAL(int, 0, STRING_compare(expected_buffer, result_buffer));

        // cleanup
        STRING_delete(expected_buffer);
        STRING_delete(result_buffer);
        BUFFER_delete(test_output_digest);
        BUFFER_delete(test_expected_digest);
        BUFFER_delete(decoded_key);
        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

    TEST_FUNCTION(insert_default_trusted_ca_cert_smoke)
    {
        // arrange
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_STORE_HANDLE store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        // act
        CERT_INFO_HANDLE cert_info = store_if->hsm_client_store_get_pki_trusted_certs(store_handle);

        // assert
        ASSERT_IS_NOT_NULL(cert_info, "Line:" TOSTRING(__LINE__));
        // todo validate cert props

        // cleanup
        certificate_info_destroy(cert_info);
        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

    TEST_FUNCTION(insert_generated_cert_and_perform_key_sign_smoke)
    {
        // arrange
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        ASSERT_IS_NOT_NULL(store_if, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create(EDGE_STORE_NAME, TEST_CA_VALIDITY);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_STORE_HANDLE store_handle = store_if->hsm_client_store_open(EDGE_STORE_NAME);
        ASSERT_IS_NOT_NULL(store_handle, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_remove_pki_cert(store_handle, "my_test_alias");
        ASSERT_ARE_NOT_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        CERT_PROPS_HANDLE cert_props = test_helper_create_certificate_props("test_cn",
                                                                            "my_test_alias",
                                                                            hsm_get_device_ca_alias(),
                                                                            CERTIFICATE_TYPE_CLIENT,
                                                                            3600);
        // act, assert
        CERT_INFO_HANDLE cert_info = store_if->hsm_client_store_get_pki_cert(store_handle, "my_test_alias");
        ASSERT_IS_NULL(cert_info, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create_pki_cert(store_handle, cert_props);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        cert_info = store_if->hsm_client_store_get_pki_cert(store_handle, "my_test_alias");
        ASSERT_IS_NOT_NULL(cert_info, "Line:" TOSTRING(__LINE__));
        // todo validate cert props

        // perform a key sign test using the created key
        unsigned char tbs[] = {'t','e','s','t'};
        test_helper_cert_key_sign(store_handle, "my_test_alias", tbs, sizeof(tbs));

        result = store_if->hsm_client_store_remove_pki_cert(store_handle, "my_test_alias");
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
        cert_properties_destroy(cert_props);
        certificate_info_destroy(cert_info);
        result = store_if->hsm_client_store_close(store_handle);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
        result = store_if->hsm_client_store_destroy(EDGE_STORE_NAME);
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
    }

END_TEST_SUITE(edge_hsm_store_int_tests)
