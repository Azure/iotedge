// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

//#############################################################################
// Memory allocator test hooks
//#############################################################################

static void* test_hook_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void* test_hook_gballoc_calloc(size_t num, size_t size)
{
    return calloc(num, size);
}

static void* test_hook_gballoc_realloc(void* ptr, size_t size)
{
    return realloc(ptr, size);
}

static void test_hook_gballoc_free(void* ptr)
{
    free(ptr);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umocktypes_charptr.h"
#include "hsm_client_store.h"
#include "hsm_log.h"
#include "hsm_log.h"
#include "hsm_utils.h"

// //#############################################################################
// // Declare and enable MOCK definitions
// //#############################################################################

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

#define TEST_VALIDITY         3600
#define TEST_SERIAL_NUM       1000

#define TEST_CA_CN_1            "cn_ca_1"
#define TEST_CA_CN_2            "cn_ca_2"
#define TEST_SERVER_CN_1        "cn_server_1"
#define TEST_SERVER_CN_3        "cn_server_3"
#define TEST_CLIENT_CN_1        "cn_client_1"

#define TEST_CA_ALIAS_1         "test_ca_alias_1"
#define TEST_CA_ALIAS_2         "test_ca_alias_2"
#define TEST_SERVER_ALIAS_1     "test_server_alias_1"
#define TEST_SERVER_ALIAS_3     "test_server_alias_3"
#define TEST_CLIENT_ALIAS_1     "test_client_alias_1"

#define TEST_CA_CERT_RSA_FILE_1     "ca_rsa_cert_1.cert.pem"
#define TEST_CA_CERT_RSA_FILE_2     "ca_rsa_cert_2.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_1 "server_rsa_cert_1.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_3 "server_rsa_cert_3.cert.pem"
#define TEST_CLIENT_CERT_RSA_FILE_1 "client_rsa_cert_1.cert.pem"

#define TEST_CA_PK_RSA_FILE_1       "ca_rsa_cert_1.key.pem"
#define TEST_CA_PK_RSA_FILE_2       "ca_rsa_cert_2.key.pem"
#define TEST_SERVER_PK_RSA_FILE_1   "server_rsa_cert_1.key.pem"
#define TEST_SERVER_PK_RSA_FILE_3   "server_rsa_cert_3.key.pem"
#define TEST_CLIENT_PK_RSA_FILE_1   "client_rsa_cert_1.key.pem"

#define TEST_CA_CERT_ECC_FILE_1     "ca_ecc_cert_1.cert.pem"
#define TEST_CA_CERT_ECC_FILE_2     "ca_ecc_cert_2.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_1 "server_ecc_cert_1.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_3 "server_ecc_cert_3.cert.pem"
#define TEST_CLIENT_CERT_ECC_FILE_1 "client_ecc_cert_1.cert.pem"

#define TEST_CA_PK_ECC_FILE_1       "ca_ecc_cert_1.key.pem"
#define TEST_CA_PK_ECC_FILE_2       "ca_ecc_cert_2.key.pem"
#define TEST_SERVER_PK_ECC_FILE_1   "server_ecc_cert_1.key.pem"
#define TEST_SERVER_PK_ECC_FILE_3   "server_ecc_cert_3.key.pem"
#define TEST_CLIENT_PK_ECC_FILE_1   "client_ecc_cert_1.key.pem"

//#############################################################################
// Test helpers
//#############################################################################

static CERT_PROPS_HANDLE test_helper_create_certificate_props
(
    const char *common_name,
    const char *alias,
    const char *issuer_alias,
    CERTIFICATE_TYPE type,
    uint64_t validity
)
{
    int result;
    CERT_PROPS_HANDLE cert_props_handle = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(cert_props_handle, "Line:" TOSTRING(__LINE__));
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

static void test_helper_generate_pki_certificate
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    const char *private_key_file,
    const char *cert_file,
    const char *issuer_private_key_file,
    const char *issuer_cert_file
)
{
    int result = generate_pki_cert_and_key(cert_props_handle,
                                           serial_num,
                                           private_key_file,
                                           cert_file,
                                           issuer_private_key_file,
                                           issuer_cert_file);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
}

static void test_helper_generate_self_signed
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    const char *private_key_file,
    const char *cert_file,
    const PKI_KEY_PROPS *key_props
)
{
    int result = generate_pki_cert_and_key_with_props(cert_props_handle,
                                                      serial_num,
                                                      private_key_file,
                                                      cert_file,
                                                      key_props);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
}

void test_helper_server_chain_validator(const PKI_KEY_PROPS *key_props)
{
    // arrange
    CERT_PROPS_HANDLE ca_root_handle;
    CERT_PROPS_HANDLE int_ca_root_handle;
    CERT_PROPS_HANDLE server_root_handle;

    ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                          TEST_CA_ALIAS_1,
                                                          TEST_CA_ALIAS_1,
                                                          CERTIFICATE_TYPE_CA,
                                                          TEST_VALIDITY);

    int_ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_2,
                                                              TEST_CA_ALIAS_2,
                                                              TEST_CA_ALIAS_1,
                                                              CERTIFICATE_TYPE_CA,
                                                              TEST_VALIDITY);

    server_root_handle = test_helper_create_certificate_props(TEST_SERVER_CN_3,
                                                              TEST_SERVER_ALIAS_3,
                                                              TEST_CA_ALIAS_2,
                                                              CERTIFICATE_TYPE_SERVER,
                                                              TEST_VALIDITY);

    // act
    test_helper_generate_self_signed(ca_root_handle,
                                    TEST_SERIAL_NUM + 1,
                                    TEST_CA_PK_RSA_FILE_1,
                                    TEST_CA_CERT_RSA_FILE_1,
                                    key_props);

    test_helper_generate_pki_certificate(int_ca_root_handle,
                                         TEST_SERIAL_NUM + 2,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         TEST_SERIAL_NUM + 3,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    // assert

    // cleanup
    delete_file(TEST_SERVER_PK_RSA_FILE_3);
    delete_file(TEST_SERVER_CERT_RSA_FILE_3);
    delete_file(TEST_CA_PK_RSA_FILE_2);
    delete_file(TEST_CA_CERT_RSA_FILE_2);
    delete_file(TEST_CA_PK_RSA_FILE_1);
    delete_file(TEST_CA_CERT_RSA_FILE_1);
    cert_properties_destroy(server_root_handle);
    cert_properties_destroy(int_ca_root_handle);
    cert_properties_destroy(ca_root_handle);
}

//#############################################################################
// Mocked functions test hooks
//#############################################################################

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s",
                   ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_int_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);

        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        umock_c_deinit();
        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }

        umock_c_reset_all_calls();
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(test_self_signed_rsa_server)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_SERVER_CN_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 CERTIFICATE_TYPE_SERVER,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_SERVER_PK_RSA_FILE_1,
                                         TEST_SERVER_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_SERVER_PK_RSA_FILE_1);
        delete_file(TEST_SERVER_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_rsa_client)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CLIENT_CN_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CLIENT,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_CLIENT_PK_RSA_FILE_1,
                                         TEST_CLIENT_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CLIENT_PK_RSA_FILE_1);
        delete_file(TEST_CLIENT_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_rsa_ca)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CA,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_RSA_FILE_1);
        delete_file(TEST_CA_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_ecc_server)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_SERVER_CN_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 CERTIFICATE_TYPE_SERVER,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_SERVER_PK_ECC_FILE_1,
                                         TEST_SERVER_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_SERVER_PK_ECC_FILE_1);
        delete_file(TEST_SERVER_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_ecc_client)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CLIENT_CN_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CLIENT,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_CLIENT_PK_ECC_FILE_1,
                                         TEST_CLIENT_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CLIENT_PK_ECC_FILE_1);
        delete_file(TEST_CLIENT_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_ecc_ca)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CA,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         TEST_CA_PK_ECC_FILE_1,
                                         TEST_CA_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_ECC_FILE_1);
        delete_file(TEST_CA_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_rsa_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

    TEST_FUNCTION(test_self_signed_ecc_default_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

    TEST_FUNCTION(test_self_signed_ecc_primes_curve_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, "prime256v1" };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

END_TEST_SUITE(edge_openssl_int_tests)
