// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_utils.h"
#include "hsm_log.h"

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_client_data.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

#define TEST_CA_ALIAS "test_ca_alias"
#define TEST_SERVER_ALIAS "test_server_alias"
#define TEST_CLIENT_ALIAS "test_client_alias"
#define TEST_CA_COMMON_NAME "test_ca_cert"
#define TEST_SERVER_COMMON_NAME "test_server_cert"
#define TEST_CLIENT_COMMON_NAME "test_client_cert"

static unsigned char TEST_ID[] = {'M', 'O', 'D', 'U', 'L', 'E', '1'};
static size_t TEST_ID_SIZE = sizeof(TEST_ID);

static unsigned char TEST_PLAINTEXT[] = {'P', 'L', 'A', 'I', 'N', 'T', 'E', 'X', 'T'};
static size_t TEST_PLAINTEXT_SIZE = sizeof(TEST_PLAINTEXT);

static unsigned char TEST_IV[] = {'A', 'B', 'C', 'D', 'E', 'F', 'G'};
static size_t TEST_IV_SIZE = sizeof(TEST_IV);

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_homedir(void)
{
#if defined(TESTONLY_IOTEDGE_HOMEDIR)
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        errno_t status = _putenv_s("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR);
    #else
        int status = setenv("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR, 1);
    #endif
    printf("IoT Edge home dir set to %s\n", TESTONLY_IOTEDGE_HOMEDIR);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
#else
    #error "Could not find symbol TESTONLY_IOTEDGE_HOMEDIR"
#endif
}

static HSM_CLIENT_HANDLE test_helper_crypto_init(void)
{
    int status;
    status = hsm_client_crypto_init();
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    HSM_CLIENT_HANDLE result = interface->hsm_client_crypto_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
    return result;
}

static void test_helper_crypto_deinit(HSM_CLIENT_HANDLE hsm_handle)
{
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    interface->hsm_client_crypto_destroy(hsm_handle);
    hsm_client_crypto_deinit();
}

static CERT_PROPS_HANDLE test_helper_create_ca_cert_properties(void)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(certificate_props, "Line:" TOSTRING(__LINE__));
    set_common_name(certificate_props, TEST_CA_COMMON_NAME);
    set_validity_seconds(certificate_props, 3600);
    set_alias(certificate_props, TEST_CA_ALIAS);
    set_issuer_alias(certificate_props, hsm_get_device_ca_alias());
    set_certificate_type(certificate_props, CERTIFICATE_TYPE_CA);
    return certificate_props;
}

static CERT_PROPS_HANDLE test_helper_create_server_cert_properties(void)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(certificate_props, "Line:" TOSTRING(__LINE__));
    set_common_name(certificate_props, TEST_SERVER_COMMON_NAME);
    set_validity_seconds(certificate_props, 3600);
    set_alias(certificate_props, TEST_SERVER_ALIAS);
    set_issuer_alias(certificate_props, TEST_CA_ALIAS);
    set_certificate_type(certificate_props, CERTIFICATE_TYPE_SERVER);
    return certificate_props;
}

static CERT_PROPS_HANDLE test_helper_create_client_cert_properties(void)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(certificate_props, "Line:" TOSTRING(__LINE__));
    set_common_name(certificate_props, TEST_CLIENT_COMMON_NAME);
    set_validity_seconds(certificate_props, 3600);
    set_alias(certificate_props, TEST_CLIENT_ALIAS);
    set_issuer_alias(certificate_props, TEST_CA_ALIAS);
    set_certificate_type(certificate_props, CERTIFICATE_TYPE_CLIENT);
    return certificate_props;
}

//#############################################################################
// Test cases
//#############################################################################
// @todo add validations for certificate info parsing when available
BEGIN_TEST_SUITE(edge_hsm_crypto_int_tests)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
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

    TEST_FUNCTION(hsm_client_crypto_init_deinit_sanity)
    {
        //arrange

        // act
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();

        // assert

        //cleanup
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_crypto_random_bytes_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        unsigned char unexpected_buffer[4];
        unsigned char output_buffer[4];
        memset(unexpected_buffer, 0, sizeof(unexpected_buffer));
        memset(output_buffer, 0, sizeof(output_buffer));

        // act
        int result = interface->hsm_client_get_random_bytes(hsm_handle, output_buffer, sizeof(output_buffer));

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, memcmp(unexpected_buffer, output_buffer, sizeof(unexpected_buffer)), "Line:" TOSTRING(__LINE__));

        //cleanup
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_ca_certificate_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE certificate_props = test_helper_create_ca_cert_properties();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_server_certificate_smoke)
    {
        //arrange
        size_t pk_size = 0;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        CERT_INFO_HANDLE ca_cert_info = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void* private_key = certificate_info_get_private_key(result, &pk_size);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_server_certificate_with_larger_expiration_time_will_use_issuers_expiration)
    {
        //arrange
        size_t pk_size = 0;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        set_validity_seconds(ca_certificate_props, 3600);
        CERT_INFO_HANDLE ca_cert_info = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();
        set_validity_seconds(certificate_props, 3600 * 2);

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void* private_key = certificate_info_get_private_key(result, &pk_size);
        int64_t expiration_time = certificate_info_get_valid_to(result);
        int64_t issuer_expiration_time = certificate_info_get_valid_to(ca_cert_info);
        ASSERT_IS_TRUE_WITH_MSG((expiration_time <= issuer_expiration_time), "Line:" TOSTRING(__LINE__));

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_server_certificate_with_smaller_expiration_time_will_use_smaller_expiration)
    {
        //arrange
        size_t pk_size = 0;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        set_validity_seconds(ca_certificate_props, 3600 * 2);
        CERT_INFO_HANDLE ca_cert_info = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();
        set_validity_seconds(certificate_props, 3600);

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void* private_key = certificate_info_get_private_key(result, &pk_size);
        int64_t expiration_time = certificate_info_get_valid_to(result);
        int64_t issuer_expiration_time = certificate_info_get_valid_to(ca_cert_info);
        ASSERT_IS_TRUE_WITH_MSG((expiration_time < issuer_expiration_time), "Line:" TOSTRING(__LINE__));

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_and_get_client_certificate_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        CERT_INFO_HANDLE ca_cert_info = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_client_cert_properties();

        // act, assert multiple calls to create certificate only creates if not created
        CERT_INFO_HANDLE result_first, result_second;
        result_first = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(result_first, "Line:" TOSTRING(__LINE__));
        result_second = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(result_second, "Line:" TOSTRING(__LINE__));
        const char *first_certificate = certificate_info_get_certificate(result_first);
        const char *second_certificate = certificate_info_get_certificate(result_second);
        size_t first_len = strlen(first_certificate);
        size_t second_len = strlen(second_certificate);
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, first_len, second_len, "Line:" TOSTRING(__LINE__));
        int cmp_result = memcmp(first_certificate, second_certificate, first_len);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));

        // destroy the certificate in the HSM and create a new one and test if different from prior call
        certificate_info_destroy(result_second);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CLIENT_ALIAS);
        result_second = NULL;
        result_second = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(result_second, "Line:" TOSTRING(__LINE__));
        second_certificate = certificate_info_get_certificate(result_second);
        cmp_result = memcmp(first_certificate, second_certificate, first_len);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CLIENT_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result_first);
        certificate_info_destroy(result_second);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_destroy_client_certificate_for_invalid_cert_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();

        // act
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CLIENT_ALIAS);

        // assert

        // cleanup
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_create_client_certificate_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        CERT_INFO_HANDLE ca_cert_info = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL_WITH_MSG(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_client_cert_properties();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CLIENT_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_get_trust_bundle_smoke)
    {
        //arrange
        size_t pk_size = 0;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_trust_bundle(hsm_handle);
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));

        // assert
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void *private_key = certificate_info_get_private_key(result, &pk_size);
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pk_size, "Line:" TOSTRING(__LINE__));

        // cleanup
        certificate_info_destroy(result);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_encryption_key_smoke)
    {
        // arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        int status;

        // act, assert
        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_encrypt_decrypt_smoke)
    {
        // arrange
        int status;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        SIZED_BUFFER id = {TEST_ID, TEST_ID_SIZE};
        SIZED_BUFFER pt = {TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = { NULL, 0 };
        SIZED_BUFFER plaintext_result = { NULL, 0 };

        // act, assert
        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(ciphertext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(size_t, 0, ciphertext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(TEST_PLAINTEXT, ciphertext_result.buffer, TEST_PLAINTEXT_SIZE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_decrypt_data(hsm_handle, &id, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, TEST_PLAINTEXT_SIZE, plaintext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(TEST_PLAINTEXT, plaintext_result.buffer, TEST_PLAINTEXT_SIZE);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(plaintext_result.buffer);
        free(ciphertext_result.buffer);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_multiple_masterkey_create_idempotent_success)
    {
        // arrange
        int status;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        SIZED_BUFFER id = {TEST_ID, TEST_ID_SIZE};
        SIZED_BUFFER pt = {TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result_1 = { NULL, 0 };
        SIZED_BUFFER ciphertext_result_2 = { NULL, 0 };

        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result_1);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act, assert
        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result_2);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        ASSERT_ARE_EQUAL_WITH_MSG(size_t, ciphertext_result_1.size, ciphertext_result_2.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(ciphertext_result_1.buffer, ciphertext_result_2.buffer, ciphertext_result_1.size);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result_1.buffer);
        free(ciphertext_result_2.buffer);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_multiple_masterkey_destroy_idempotent_success)
    {
        // arrange
        int status;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        test_helper_crypto_deinit(hsm_handle);
    }

END_TEST_SUITE(edge_hsm_crypto_int_tests)
