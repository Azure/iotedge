// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "hsm_client_store.h"
#include "hsm_log.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;
static char* TEST_TEMP_DIR = NULL;
static char* TEST_TEMP_DIR_GUID = NULL;

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

#define TEST_CA_CERT_RSA_FILE_1_NAME     "ca_rsa_cert_1.cert.pem"
#define TEST_CA_CERT_RSA_FILE_2_NAME     "ca_rsa_cert_2.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_1_NAME "server_rsa_cert_1.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_3_NAME "server_rsa_cert_3.cert.pem"
#define TEST_CLIENT_CERT_RSA_FILE_1_NAME "client_rsa_cert_1.cert.pem"
static char *TEST_CA_CERT_RSA_FILE_1     = NULL;
static char *TEST_CA_CERT_RSA_FILE_2     = NULL;
static char *TEST_SERVER_CERT_RSA_FILE_1 = NULL;
static char *TEST_SERVER_CERT_RSA_FILE_3 = NULL;
static char *TEST_CLIENT_CERT_RSA_FILE_1 = NULL;

#define TEST_CA_PK_RSA_FILE_1_NAME       "ca_rsa_cert_1.key.pem"
#define TEST_CA_PK_RSA_FILE_2_NAME       "ca_rsa_cert_2.key.pem"
#define TEST_SERVER_PK_RSA_FILE_1_NAME   "server_rsa_cert_1.key.pem"
#define TEST_SERVER_PK_RSA_FILE_3_NAME   "server_rsa_cert_3.key.pem"
#define TEST_CLIENT_PK_RSA_FILE_1_NAME   "client_rsa_cert_1.key.pem"
static char *TEST_CA_PK_RSA_FILE_1       = NULL;
static char *TEST_CA_PK_RSA_FILE_2       = NULL;
static char *TEST_SERVER_PK_RSA_FILE_1   = NULL;
static char *TEST_SERVER_PK_RSA_FILE_3   = NULL;
static char *TEST_CLIENT_PK_RSA_FILE_1   = NULL;

#define TEST_CA_CERT_ECC_FILE_1_NAME     "ca_ecc_cert_1.cert.pem"
#define TEST_CA_CERT_ECC_FILE_2_NAME     "ca_ecc_cert_2.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_1_NAME "server_ecc_cert_1.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_3_NAME "server_ecc_cert_3.cert.pem"
#define TEST_CLIENT_CERT_ECC_FILE_1_NAME "client_ecc_cert_1.cert.pem"
static char *TEST_CA_CERT_ECC_FILE_1     = NULL;
static char *TEST_CA_CERT_ECC_FILE_2     = NULL;
static char *TEST_SERVER_CERT_ECC_FILE_1 = NULL;
static char *TEST_SERVER_CERT_ECC_FILE_3 = NULL;
static char *TEST_CLIENT_CERT_ECC_FILE_1 = NULL;

#define TEST_CA_PK_ECC_FILE_1_NAME       "ca_ecc_cert_1.key.pem"
#define TEST_CA_PK_ECC_FILE_2_NAME       "ca_ecc_cert_2.key.pem"
#define TEST_SERVER_PK_ECC_FILE_1_NAME   "server_ecc_cert_1.key.pem"
#define TEST_SERVER_PK_ECC_FILE_3_NAME   "server_ecc_cert_3.key.pem"
#define TEST_CLIENT_PK_ECC_FILE_1_NAME   "client_ecc_cert_1.key.pem"
static char *TEST_CA_PK_ECC_FILE_1       = NULL;
static char *TEST_CA_PK_ECC_FILE_2       = NULL;
static char *TEST_SERVER_PK_ECC_FILE_1   = NULL;
static char *TEST_SERVER_PK_ECC_FILE_3   = NULL;
static char *TEST_CLIENT_PK_ECC_FILE_1   = NULL;

#define TEST_CHAIN_FILE_PATH_NAME        "chain_file.pem"
static char *TEST_CHAIN_FILE_PATH        = NULL;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    char *temp_dir, *guid;
    temp_dir = hsm_test_util_create_temp_dir(&guid);
    ASSERT_IS_NOT_NULL_WITH_MSG(guid, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(temp_dir, "Line:" TOSTRING(__LINE__));
    printf("Temp dir created: [%s]\r\n", temp_dir);
    *pp_temp_dir = temp_dir;
    *pp_temp_dir_guid = guid;
}

static void test_helper_teardown_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    ASSERT_IS_NOT_NULL_WITH_MSG(pp_temp_dir, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(pp_temp_dir_guid, "Line:" TOSTRING(__LINE__));

    char *temp_dir = *pp_temp_dir;
    char *guid = *pp_temp_dir_guid;
    ASSERT_IS_NOT_NULL_WITH_MSG(temp_dir, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(guid, "Line:" TOSTRING(__LINE__));

    hsm_test_util_delete_dir(guid);
    free(temp_dir);
    free(guid);
    *pp_temp_dir = NULL;
    *pp_temp_dir_guid = NULL;
}

static char* prepare_file_path(const char* base_dir, const char* file_name)
{
    size_t path_size = get_max_file_path_size();
    char *file_path = calloc(path_size, 1);
    ASSERT_IS_NOT_NULL_WITH_MSG(file_path, "Line:" TOSTRING(__LINE__));
    int status = snprintf(file_path, path_size, "%s%s", base_dir, file_name);
    ASSERT_IS_TRUE_WITH_MSG(((status > 0) || (status < path_size)), "Line:" TOSTRING(__LINE__));

    return file_path;
}

static void test_helper_setup_homedir(void)
{
    test_helper_setup_temp_dir(&TEST_IOTEDGE_HOMEDIR, &TEST_IOTEDGE_HOMEDIR_GUID);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);
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
    int path_len,
    const char *private_key_file,
    const char *cert_file,
    const char *issuer_private_key_file,
    const char *issuer_cert_file
)
{
    int result = generate_pki_cert_and_key(cert_props_handle,
                                           serial_num,
                                           path_len,
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
    int path_len,
    const char *private_key_file,
    const char *cert_file,
    const PKI_KEY_PROPS *key_props
)
{
    int result = generate_pki_cert_and_key_with_props(cert_props_handle,
                                                      serial_num,
                                                      path_len,
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
                                     2,
                                     TEST_CA_PK_RSA_FILE_1,
                                     TEST_CA_CERT_RSA_FILE_1,
                                     key_props);

    test_helper_generate_pki_certificate(int_ca_root_handle,
                                         TEST_SERIAL_NUM + 2,
                                         1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         TEST_SERIAL_NUM + 3,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    // assert
    bool cert_verified = false;
    int status = verify_certificate(TEST_CA_CERT_RSA_FILE_2, TEST_CA_PK_RSA_FILE_2, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_2, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_SERVER_CERT_RSA_FILE_3, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_FALSE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, cert_verified, "Line:" TOSTRING(__LINE__));

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
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_int_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
        test_helper_setup_temp_dir(&TEST_TEMP_DIR, &TEST_TEMP_DIR_GUID);

        TEST_CA_CERT_RSA_FILE_1     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_RSA_FILE_1_NAME);
        TEST_CA_CERT_RSA_FILE_2     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_RSA_FILE_2_NAME);
        TEST_SERVER_CERT_RSA_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_RSA_FILE_1_NAME);
        TEST_SERVER_CERT_RSA_FILE_3 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_RSA_FILE_3_NAME);
        TEST_CLIENT_CERT_RSA_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_CERT_RSA_FILE_1_NAME);

        TEST_CA_PK_RSA_FILE_1       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_RSA_FILE_1_NAME);
        TEST_CA_PK_RSA_FILE_2       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_RSA_FILE_2_NAME);
        TEST_SERVER_PK_RSA_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_RSA_FILE_1_NAME);
        TEST_SERVER_PK_RSA_FILE_3   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_RSA_FILE_3_NAME);
        TEST_CLIENT_PK_RSA_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_PK_RSA_FILE_1_NAME);

        TEST_CA_CERT_ECC_FILE_1     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_ECC_FILE_1_NAME);
        TEST_CA_CERT_ECC_FILE_2     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_ECC_FILE_2_NAME);
        TEST_SERVER_CERT_ECC_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_ECC_FILE_1_NAME);
        TEST_SERVER_CERT_ECC_FILE_3 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_ECC_FILE_3_NAME);
        TEST_CLIENT_CERT_ECC_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_CERT_ECC_FILE_1_NAME);

        TEST_CA_PK_ECC_FILE_1       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_ECC_FILE_1_NAME);
        TEST_CA_PK_ECC_FILE_2       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_ECC_FILE_2_NAME);
        TEST_SERVER_PK_ECC_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_ECC_FILE_1_NAME);
        TEST_SERVER_PK_ECC_FILE_3   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_ECC_FILE_3_NAME);
        TEST_CLIENT_PK_ECC_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_PK_ECC_FILE_1_NAME);

        TEST_CHAIN_FILE_PATH = prepare_file_path(TEST_TEMP_DIR, TEST_CHAIN_FILE_PATH_NAME);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        free(TEST_CA_CERT_RSA_FILE_1); TEST_CA_CERT_RSA_FILE_1 = NULL;
        free(TEST_CA_CERT_RSA_FILE_2); TEST_CA_CERT_RSA_FILE_2 = NULL;
        free(TEST_SERVER_CERT_RSA_FILE_1); TEST_SERVER_CERT_RSA_FILE_1 = NULL;
        free(TEST_SERVER_CERT_RSA_FILE_3); TEST_SERVER_CERT_RSA_FILE_3 = NULL;
        free(TEST_CLIENT_CERT_RSA_FILE_1); TEST_CLIENT_CERT_RSA_FILE_1 = NULL;

        free(TEST_CA_PK_RSA_FILE_1); TEST_CA_PK_RSA_FILE_1 = NULL;
        free(TEST_CA_PK_RSA_FILE_2); TEST_CA_PK_RSA_FILE_2 = NULL;
        free(TEST_SERVER_PK_RSA_FILE_1); TEST_SERVER_PK_RSA_FILE_1 = NULL;
        free(TEST_SERVER_PK_RSA_FILE_3); TEST_SERVER_PK_RSA_FILE_3 = NULL;
        free(TEST_CLIENT_PK_RSA_FILE_1); TEST_CLIENT_PK_RSA_FILE_1 = NULL;

        free(TEST_CA_CERT_ECC_FILE_1); TEST_CA_CERT_ECC_FILE_1 = NULL;
        free(TEST_CA_CERT_ECC_FILE_2); TEST_CA_CERT_ECC_FILE_2 = NULL;
        free(TEST_SERVER_CERT_ECC_FILE_1); TEST_SERVER_CERT_ECC_FILE_1 = NULL;
        free(TEST_SERVER_CERT_ECC_FILE_3); TEST_SERVER_CERT_ECC_FILE_3 = NULL;
        free(TEST_CLIENT_CERT_ECC_FILE_1); TEST_CLIENT_CERT_ECC_FILE_1 = NULL;

        free(TEST_CA_PK_ECC_FILE_1); TEST_CA_PK_ECC_FILE_1 = NULL;
        free(TEST_CA_PK_ECC_FILE_2); TEST_CA_PK_ECC_FILE_2 = NULL;
        free(TEST_SERVER_PK_ECC_FILE_1); TEST_SERVER_PK_ECC_FILE_1 = NULL;
        free(TEST_SERVER_PK_ECC_FILE_3); TEST_SERVER_PK_ECC_FILE_3 = NULL;
        free(TEST_CLIENT_PK_ECC_FILE_1); TEST_CLIENT_PK_ECC_FILE_1 = NULL;

        free(TEST_CHAIN_FILE_PATH); TEST_CHAIN_FILE_PATH = NULL;

        test_helper_teardown_temp_dir(&TEST_TEMP_DIR, &TEST_TEMP_DIR_GUID);
        test_helper_teardown_temp_dir(&TEST_IOTEDGE_HOMEDIR, &TEST_IOTEDGE_HOMEDIR_GUID);
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
                                         0,
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
                                         0,
                                         TEST_CLIENT_PK_RSA_FILE_1,
                                         TEST_CLIENT_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CLIENT_PK_RSA_FILE_1);
        delete_file(TEST_CLIENT_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_non_ca_cert_with_non_zero_path_fails)
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
        int result = generate_pki_cert_and_key_with_props(cert_props_handle,
                                                          TEST_SERIAL_NUM,
                                                          2,
                                                          TEST_SERVER_PK_RSA_FILE_1,
                                                          TEST_SERVER_CERT_RSA_FILE_1,
                                                          &key_props);
        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
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
                                         2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_RSA_FILE_1);
        delete_file(TEST_CA_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

#if USE_ECC_KEYS
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
                                         0,
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
                                         0,
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
                                         2,
                                         TEST_CA_PK_ECC_FILE_1,
                                         TEST_CA_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_ECC_FILE_1);
        delete_file(TEST_CA_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }
#endif //USE_ECC_KEYS

    TEST_FUNCTION(test_self_signed_rsa_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

#if USE_ECC_KEYS
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
#endif //USE_ECC_KEYS

END_TEST_SUITE(edge_openssl_int_tests)
