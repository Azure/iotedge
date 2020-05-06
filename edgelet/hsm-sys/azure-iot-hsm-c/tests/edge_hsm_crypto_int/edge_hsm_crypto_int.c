// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/threadapi.h"
#include "test_utils.h"
#include "hsm_client_store.h"
#include "hsm_key.h"
#include "hsm_utils.h"
#include "hsm_log.h"
#include "hsm_constants.h"

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

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;

static unsigned char TEST_ID[] = {'M', 'O', 'D', 'U', 'L', 'E', '1'};
static size_t TEST_ID_SIZE = sizeof(TEST_ID);

static unsigned char TEST_PLAINTEXT[] = {'P', 'L', 'A', 'I', 'N', 'T', 'E', 'X', 'T'};
static size_t TEST_PLAINTEXT_SIZE = sizeof(TEST_PLAINTEXT);

static unsigned char TEST_IV[] = {'A', 'B', 'C', 'D', 'E', 'F', 'G'};
static size_t TEST_IV_SIZE = sizeof(TEST_IV);

// transparent gateway scenario test data
#define TEST_VALIDITY 3600 * 24 // 1 day
#define TEST_SERIAL_NUM 1000
#define ROOT_CA_CN "Root CA"
#define ROOT_CA_ALIAS "test_root"
#define ROOT_CA_PATH_LEN 5
#define INT_CA_1_CN "Int 1 CA"
#define INT_CA_1_ALIAS "test_int_1"
#define INT_CA_1_PATH_LEN ((ROOT_CA_PATH_LEN) - 1)
#define INT_CA_2_CN "Int 2 CA"
#define INT_CA_2_ALIAS "test_int_2"
#define INT_CA_2_PATH_LEN ((INT_CA_1_PATH_LEN) - 1)
#define NUM_TRUSTED_CERTS 3 //root, int1, int2
#define DEVICE_CA_CN "Device CA"
#define DEVICE_CA_ALIAS "test_device_ca"
#define DEVICE_CA_PATH_LEN ((INT_CA_2_PATH_LEN) - 1)

#define HMAC_SHA256_DIGEST_LEN 256

static STRING_HANDLE BASE_TG_CERTS_PATH = NULL;
static STRING_HANDLE VALID_DEVICE_CA_PATH = NULL;
static STRING_HANDLE VALID_DEVICE_PK_PATH = NULL;
static STRING_HANDLE VALID_TRUSTED_CA_PATH = NULL;
static STRING_HANDLE ROOT_CA_PATH = NULL;
static STRING_HANDLE ROOT_PK_PATH = NULL;
static STRING_HANDLE INT_1_CA_PATH = NULL;
static STRING_HANDLE INT_1_PK_PATH = NULL;
static STRING_HANDLE INT_2_CA_PATH = NULL;
static STRING_HANDLE INT_2_PK_PATH = NULL;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    static const char *SLASH = "\\";
#else
    static const char *SLASH = "/";
#endif
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
    ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
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
    ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
}

static void test_helper_prepare_transparent_gateway_certs(void)
{
    CERT_PROPS_HANDLE ca_root_handle;
    CERT_PROPS_HANDLE int_ca_1_root_handle;
    CERT_PROPS_HANDLE int_ca_2_root_handle;
    CERT_PROPS_HANDLE device_ca_handle;

    int status;
    PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

    const char *device_ca_path = STRING_c_str(VALID_DEVICE_CA_PATH);
    const char *device_pk_path = STRING_c_str(VALID_DEVICE_PK_PATH);
    const char *trusted_ca_path = STRING_c_str(VALID_TRUSTED_CA_PATH);
    const char *root_ca_path = STRING_c_str(ROOT_CA_PATH);
    const char *root_pk_path = STRING_c_str(ROOT_PK_PATH);
    const char *int_ca_1_path = STRING_c_str(INT_1_CA_PATH);
    const char *int_pk_1_path = STRING_c_str(INT_1_PK_PATH);
    const char *int_ca_2_path = STRING_c_str(INT_2_CA_PATH);
    const char *int_pk_2_path = STRING_c_str(INT_2_PK_PATH);

    ca_root_handle = test_helper_create_certificate_props(ROOT_CA_CN,
                                                          ROOT_CA_ALIAS,
                                                          ROOT_CA_ALIAS,
                                                          CERTIFICATE_TYPE_CA,
                                                          TEST_VALIDITY);

    test_helper_generate_self_signed(ca_root_handle,
                                     TEST_SERIAL_NUM + 1,
                                     ROOT_CA_PATH_LEN,
                                     root_pk_path,
                                     root_ca_path,
                                     &key_props);

    int_ca_1_root_handle = test_helper_create_certificate_props(INT_CA_1_CN,
                                                                INT_CA_1_ALIAS,
                                                                ROOT_CA_ALIAS,
                                                                CERTIFICATE_TYPE_CA,
                                                                TEST_VALIDITY);

    test_helper_generate_pki_certificate(int_ca_1_root_handle,
                                         TEST_SERIAL_NUM + 2,
                                         INT_CA_1_PATH_LEN,
                                         int_pk_1_path,
                                         int_ca_1_path,
                                         root_pk_path,
                                         root_ca_path);

    int_ca_2_root_handle = test_helper_create_certificate_props(INT_CA_2_CN,
                                                                INT_CA_2_ALIAS,
                                                                INT_CA_1_ALIAS,
                                                                CERTIFICATE_TYPE_CA,
                                                                TEST_VALIDITY);

    test_helper_generate_pki_certificate(int_ca_2_root_handle,
                                         TEST_SERIAL_NUM + 3,
                                         INT_CA_2_PATH_LEN,
                                         int_pk_2_path,
                                         int_ca_2_path,
                                         int_pk_1_path,
                                         int_ca_1_path);

    device_ca_handle = test_helper_create_certificate_props(INT_CA_2_CN,
                                                            INT_CA_2_ALIAS,
                                                            INT_CA_1_ALIAS,
                                                            CERTIFICATE_TYPE_CA,
                                                            TEST_VALIDITY);

    test_helper_generate_pki_certificate(device_ca_handle,
                                         TEST_SERIAL_NUM + 4,
                                         DEVICE_CA_PATH_LEN,
                                         device_pk_path,
                                         device_ca_path,
                                         int_pk_2_path,
                                         int_ca_2_path);

    const char *trusted_files[1] = { NULL };
    trusted_files[0] = int_ca_2_path;
    char* trusted_ca_certs = concat_files_to_cstring(trusted_files, sizeof(trusted_files)/sizeof(trusted_files[0]));
    ASSERT_IS_NOT_NULL(trusted_ca_certs, "Line:" TOSTRING(__LINE__));
    status = write_cstring_to_file(trusted_ca_path, trusted_ca_certs);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    // cleanup
    free(trusted_ca_certs);
    cert_properties_destroy(device_ca_handle);
    cert_properties_destroy(int_ca_2_root_handle);
    cert_properties_destroy(int_ca_1_root_handle);
    cert_properties_destroy(ca_root_handle);
}

static void test_helper_setup_homedir(void)
{
    int status;

    TEST_IOTEDGE_HOMEDIR = hsm_test_util_create_temp_dir(&TEST_IOTEDGE_HOMEDIR_GUID);
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR_GUID, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR, "Line:" TOSTRING(__LINE__));

    printf("Temp dir created: [%s]\r\n", TEST_IOTEDGE_HOMEDIR);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);

    STRING_HANDLE BASE_TG_CERTS_PATH = STRING_construct(TEST_IOTEDGE_HOMEDIR);
    ASSERT_IS_NOT_NULL(BASE_TG_CERTS_PATH, "Line:" TOSTRING(__LINE__));
    status = STRING_concat(BASE_TG_CERTS_PATH, SLASH);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    VALID_DEVICE_CA_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(VALID_DEVICE_CA_PATH, "device_ca_cert.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    VALID_DEVICE_PK_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(VALID_DEVICE_PK_PATH, "device_pk_cert.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    VALID_TRUSTED_CA_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(VALID_TRUSTED_CA_PATH, "trusted_ca_certs.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    ROOT_CA_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(ROOT_CA_PATH, "root_ca_cert.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    ROOT_PK_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(ROOT_PK_PATH, "root_ca_pk.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    INT_1_CA_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(INT_1_CA_PATH, "int_1_ca_cert.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    INT_1_PK_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(INT_1_PK_PATH, "int_1_ca_pk.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    INT_2_CA_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(INT_2_CA_PATH, "int_2_ca_cert.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    INT_2_PK_PATH = STRING_clone(BASE_TG_CERTS_PATH);
    status = STRING_concat(INT_2_PK_PATH, "int_2_ca_pk.pem");
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

    test_helper_prepare_transparent_gateway_certs();

    STRING_delete(BASE_TG_CERTS_PATH);
    BASE_TG_CERTS_PATH = NULL;
}

static void test_helper_teardown_homedir(void)
{
    delete_file(STRING_c_str(VALID_DEVICE_CA_PATH));
    STRING_delete(VALID_DEVICE_CA_PATH);
    VALID_DEVICE_CA_PATH = NULL;

    delete_file(STRING_c_str(VALID_DEVICE_PK_PATH));
    STRING_delete(VALID_DEVICE_PK_PATH);
    VALID_DEVICE_PK_PATH = NULL;

    delete_file(STRING_c_str(VALID_TRUSTED_CA_PATH));
    STRING_delete(VALID_TRUSTED_CA_PATH);
    VALID_TRUSTED_CA_PATH = NULL;

    delete_file(STRING_c_str(ROOT_CA_PATH));
    STRING_delete(ROOT_CA_PATH);
    ROOT_CA_PATH = NULL;

    delete_file(STRING_c_str(ROOT_PK_PATH));
    STRING_delete(ROOT_PK_PATH);
    ROOT_PK_PATH = NULL;

    delete_file(STRING_c_str(INT_1_CA_PATH));
    STRING_delete(INT_1_CA_PATH);
    INT_1_CA_PATH = NULL;

    delete_file(STRING_c_str(INT_1_PK_PATH));
    STRING_delete(INT_1_PK_PATH);
    INT_1_PK_PATH = NULL;

    delete_file(STRING_c_str(INT_2_CA_PATH));
    STRING_delete(INT_2_CA_PATH);
    INT_2_CA_PATH = NULL;

    delete_file(STRING_c_str(INT_2_PK_PATH));
    STRING_delete(INT_2_PK_PATH);
    INT_2_PK_PATH = NULL;

    if ((TEST_IOTEDGE_HOMEDIR != NULL) && (TEST_IOTEDGE_HOMEDIR_GUID != NULL))
    {
        hsm_test_util_delete_dir(TEST_IOTEDGE_HOMEDIR_GUID);
        free(TEST_IOTEDGE_HOMEDIR);
        TEST_IOTEDGE_HOMEDIR = NULL;
        free(TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_IOTEDGE_HOMEDIR_GUID = NULL;
    }
}

static HSM_CLIENT_HANDLE test_helper_crypto_init(void)
{
    int status;
    status = hsm_client_crypto_init(CA_VALIDITY);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
    HSM_CLIENT_HANDLE result = interface->hsm_client_crypto_create();
    ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
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
    ASSERT_IS_NOT_NULL(certificate_props, "Line:" TOSTRING(__LINE__));
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
    ASSERT_IS_NOT_NULL(certificate_props, "Line:" TOSTRING(__LINE__));
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
    ASSERT_IS_NOT_NULL(certificate_props, "Line:" TOSTRING(__LINE__));
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
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_NOT_EQUAL(int, 0, memcmp(unexpected_buffer, output_buffer, sizeof(unexpected_buffer)), "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void *private_key = certificate_info_get_private_key(result, &pk_size);
        const char *common_name = certificate_info_get_common_name(result);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(private_key, "Line:" TOSTRING(__LINE__));
        int cmp = strcmp(TEST_SERVER_COMMON_NAME, common_name);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_mulitple_destroy_create_destroy_certificate_smoke)
    {
        //arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE certificate_props = test_helper_create_ca_cert_properties();

        // act
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
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
        ASSERT_IS_NOT_NULL(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();
        set_validity_seconds(certificate_props, 3600 * 2);

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void *private_key = certificate_info_get_private_key(result, &pk_size);
        const char *common_name = certificate_info_get_common_name(result);
        int64_t expiration_time = certificate_info_get_valid_to(result);
        int64_t issuer_expiration_time = certificate_info_get_valid_to(ca_cert_info);
        ASSERT_IS_TRUE((expiration_time <= issuer_expiration_time), "Line:" TOSTRING(__LINE__));

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(private_key, "Line:" TOSTRING(__LINE__));
        int cmp = strcmp(TEST_SERVER_COMMON_NAME, common_name);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();
        set_validity_seconds(certificate_props, 3600);

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void *private_key = certificate_info_get_private_key(result, &pk_size);
        const char *common_name = certificate_info_get_common_name(result);
        int64_t expiration_time = certificate_info_get_valid_to(result);
        int64_t issuer_expiration_time = certificate_info_get_valid_to(ca_cert_info);
        ASSERT_IS_TRUE((expiration_time < issuer_expiration_time), "Line:" TOSTRING(__LINE__));

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(private_key, "Line:" TOSTRING(__LINE__));
        int cmp = strcmp(TEST_SERVER_COMMON_NAME, common_name);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_client_cert_properties();

        // act, assert multiple calls to create certificate only creates if not created
        CERT_INFO_HANDLE result_first, result_second;
        result_first = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(result_first, "Line:" TOSTRING(__LINE__));
        result_second = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(result_second, "Line:" TOSTRING(__LINE__));
        const char *first_certificate = certificate_info_get_certificate(result_first);
        const char *second_certificate = certificate_info_get_certificate(result_second);
        size_t first_len = strlen(first_certificate);
        size_t second_len = strlen(second_certificate);
        ASSERT_ARE_EQUAL(size_t, first_len, second_len, "Line:" TOSTRING(__LINE__));
        int cmp_result = memcmp(first_certificate, second_certificate, first_len);
        ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));

        // destroy the certificate in the HSM and create a new one and test if different from prior call
        certificate_info_destroy(result_second);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CLIENT_ALIAS);
        result_second = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(result_second, "Line:" TOSTRING(__LINE__));
        second_certificate = certificate_info_get_certificate(result_second);
        cmp_result = memcmp(first_certificate, second_certificate, first_len);
        ASSERT_ARE_NOT_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(ca_cert_info, "Line:" TOSTRING(__LINE__));
        CERT_PROPS_HANDLE certificate_props = test_helper_create_client_cert_properties();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        const char *common_name = certificate_info_get_common_name(result);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        int cmp = strcmp(TEST_CLIENT_COMMON_NAME, common_name);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

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
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));

        // assert
        const char *certificate = certificate_info_get_certificate(result);
        const void *private_key = certificate_info_get_private_key(result, &pk_size);
        ASSERT_IS_NOT_NULL(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL(private_key, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(size_t, 0, pk_size, "Line:" TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(ciphertext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_NOT_EQUAL(size_t, 0, ciphertext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(TEST_PLAINTEXT, ciphertext_result.buffer, TEST_PLAINTEXT_SIZE);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_decrypt_data(hsm_handle, &id, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(size_t, TEST_PLAINTEXT_SIZE, plaintext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(TEST_PLAINTEXT, plaintext_result.buffer, TEST_PLAINTEXT_SIZE);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result_1);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        // destroy crypto and then recreate to make sure the same master key is used
        test_helper_crypto_deinit(hsm_handle);
        hsm_handle = test_helper_crypto_init();

        // act, assert
        status = interface->hsm_client_create_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_encrypt_data(hsm_handle, &id, &pt, &iv, &ciphertext_result_2);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        ASSERT_ARE_EQUAL(size_t, ciphertext_result_1.size, ciphertext_result_2.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(ciphertext_result_1.buffer, ciphertext_result_2.buffer, ciphertext_result_1.size);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = interface->hsm_client_destroy_master_encryption_key(hsm_handle);

        // assert
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_transparent_gateway_trust_bundle_smoke)
    {
        // arrange
        const char *device_ca_path = STRING_c_str(VALID_DEVICE_CA_PATH);
        const char *device_pk_path = STRING_c_str(VALID_DEVICE_PK_PATH);
        const char *trusted_ca_path = STRING_c_str(VALID_TRUSTED_CA_PATH);
        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();

        // act, assert
        CERT_INFO_HANDLE result = interface->hsm_client_get_trust_bundle(hsm_handle);
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *certificate = certificate_info_get_certificate(result);
        ASSERT_IS_NOT_NULL(certificate, "Line:" TOSTRING(__LINE__));
        char *expected_trust_bundle = read_file_into_cstring(trusted_ca_path, NULL);
        ASSERT_ARE_EQUAL(size_t, strlen(certificate), strlen(expected_trust_bundle), "Line:" TOSTRING(__LINE__));
        int cmp = memcmp(certificate, expected_trust_bundle, strlen(certificate));
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(expected_trust_bundle);
        certificate_info_destroy(result);
        test_helper_crypto_deinit(hsm_handle);
        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
    }

    TEST_FUNCTION(hsm_client_transparent_gateway_ca_cert_create_smoke)
    {
        // arrange
        const char *device_ca_path = STRING_c_str(VALID_DEVICE_CA_PATH);
        const char *device_pk_path = STRING_c_str(VALID_DEVICE_PK_PATH);
        const char *trusted_ca_path = STRING_c_str(VALID_TRUSTED_CA_PATH);
        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();

        // act, assert
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *chain_certificate = certificate_info_get_chain(result);
        ASSERT_IS_NOT_NULL(chain_certificate, "Line:" TOSTRING(__LINE__));
        char *expected_chain_certificate = read_file_into_cstring(device_ca_path, NULL);
        ASSERT_ARE_EQUAL(size_t, strlen(expected_chain_certificate), strlen(chain_certificate), "Line:" TOSTRING(__LINE__));
        int cmp = memcmp(expected_chain_certificate, chain_certificate, strlen(chain_certificate));
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(expected_chain_certificate);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
    }

    TEST_FUNCTION(hsm_client_transparent_gateway_ca_cert_create_expiration_smoke)
    {
        // arrange
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE ca_certificate_props = test_helper_create_ca_cert_properties();
        set_validity_seconds(ca_certificate_props, 1);
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));

        // act
        ThreadAPI_Sleep(2000);
        CERT_INFO_HANDLE temp_info_handle = interface->hsm_client_create_certificate(hsm_handle, ca_certificate_props);

        // assert
        ASSERT_IS_NULL(temp_info_handle, "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(ca_certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_transparent_gateway_server_cert_create_smoke)
    {
        // arrange
        const char *device_ca_path = STRING_c_str(VALID_DEVICE_CA_PATH);
        const char *device_pk_path = STRING_c_str(VALID_DEVICE_PK_PATH);
        const char *trusted_ca_path = STRING_c_str(VALID_TRUSTED_CA_PATH);
        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE certificate_props = test_helper_create_server_cert_properties();
        set_issuer_alias(certificate_props, hsm_get_device_ca_alias());

        // act, assert
        CERT_INFO_HANDLE result = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        const char *chain_certificate = certificate_info_get_chain(result);
        ASSERT_IS_NOT_NULL(chain_certificate, "Line:" TOSTRING(__LINE__));
        char *expected_chain_certificate = read_file_into_cstring(device_ca_path, NULL);
        ASSERT_ARE_EQUAL(size_t, strlen(expected_chain_certificate), strlen(chain_certificate), "Line:" TOSTRING(__LINE__));
        int cmp = memcmp(expected_chain_certificate, chain_certificate, strlen(chain_certificate));
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(expected_chain_certificate);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        test_helper_crypto_deinit(hsm_handle);
        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
    }

    TEST_FUNCTION(hsm_client_transparent_gateway_erroneous_config)
    {
        // arrange
        int status;
        const char INVALID_PATH[] = "b_l_a_h.txt";
        const char *device_ca_path = STRING_c_str(VALID_DEVICE_CA_PATH);
        const char *device_pk_path = STRING_c_str(VALID_DEVICE_PK_PATH);
        const char *trusted_ca_path = STRING_c_str(VALID_TRUSTED_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);

        // act, assert
        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, device_ca_path);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, device_pk_path);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, trusted_ca_path);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        hsm_test_util_setenv(ENV_DEVICE_CA_PATH, INVALID_PATH);
        hsm_test_util_setenv(ENV_DEVICE_PK_PATH, INVALID_PATH);
        hsm_test_util_setenv(ENV_TRUSTED_CA_CERTS_PATH, INVALID_PATH);
        status = hsm_client_crypto_init(CA_VALIDITY);
        ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        hsm_test_util_unsetenv(ENV_DEVICE_CA_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_PK_PATH);
        hsm_test_util_unsetenv(ENV_TRUSTED_CA_CERTS_PATH);
    }

    TEST_FUNCTION(hsm_client_crypto_sign_with_private_key_smoke)
    {
        // arrange
        int status;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
        CERT_PROPS_HANDLE certificate_props = test_helper_create_ca_cert_properties();
        CERT_INFO_HANDLE ca_handle = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(ca_handle, "Line:" TOSTRING(__LINE__));

        unsigned char data[] = { 'a', 'b', 'c' };
        size_t data_size = sizeof(data);
        unsigned char* digest = NULL;
        size_t digest_size = 0;

        // act
        status = interface->hsm_client_crypto_sign_with_private_key(hsm_handle, TEST_CA_ALIAS, data, data_size, &digest, &digest_size);

        // assert
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL(digest, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_TRUE((HMAC_SHA256_DIGEST_LEN <= digest_size), "Line:" TOSTRING(__LINE__));

        // cleanup
        free(digest);
        certificate_info_destroy(ca_handle);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        cert_properties_destroy(certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

    TEST_FUNCTION(hsm_client_crypto_get_certificate_smoke)
    {
        // arrange 1
        int status;
        CERT_INFO_HANDLE result;
        HSM_CLIENT_HANDLE hsm_handle = test_helper_crypto_init();
        const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();

        // act, 1 ensure certificate get fails when it has not yet been created
        result = interface->hsm_client_crypto_get_certificate(hsm_handle, TEST_CA_ALIAS);

        // assert 1
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));


        // arrange 2
        CERT_PROPS_HANDLE certificate_props = test_helper_create_ca_cert_properties();
        CERT_INFO_HANDLE ca_handle = interface->hsm_client_create_certificate(hsm_handle, certificate_props);
        ASSERT_IS_NOT_NULL(ca_handle, "Line:" TOSTRING(__LINE__));

        // act 2 get the same certificate
        result = interface->hsm_client_crypto_get_certificate(hsm_handle, TEST_CA_ALIAS);

        // assert 2 ensure both certificate and key returned are identical
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        status = strcmp(certificate_info_get_certificate(ca_handle), certificate_info_get_certificate(result));
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        size_t ca_pk_size = 0, result_pk_size = 0;
        const void *ca_pk = certificate_info_get_private_key(ca_handle, &ca_pk_size);
        const void *result_pk = certificate_info_get_private_key(result, &result_pk_size);
        ASSERT_ARE_EQUAL(size_t, ca_pk_size, result_pk_size, "Line:" TOSTRING(__LINE__));
        status = memcmp(ca_pk, result_pk, ca_pk_size);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_TRUE((certificate_info_private_key_type(ca_handle) == certificate_info_private_key_type(result)), "Line:" TOSTRING(__LINE__));

        // cleanup
        certificate_info_destroy(result);
        certificate_info_destroy(ca_handle);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        cert_properties_destroy(certificate_props);
        test_helper_crypto_deinit(hsm_handle);
    }

END_TEST_SUITE(edge_hsm_crypto_int_tests)
