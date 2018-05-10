// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_utils.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static char *TEST_SERVER_CERT = NULL;
static char *TEST_SERVER_CHAIN_CERT = NULL;
static char *TEST_SERVER_PRIVATE_KEY = NULL;
static char *TEST_TRUSTED_CA_CERT = NULL;

#define TEST_CA_ALIAS "test_ca_alias"
#define TEST_SERVER_ALIAS "test_server_alias"
#define TEST_CLIENT_ALIAS "test_client_alias"

//#############################################################################
// Test helpers
//#############################################################################

static char* cert_file_path_helper(const char* relative_path)
{
    char* base_path = getenv("EDGEHOMEDIR");
    ASSERT_IS_NOT_NULL_WITH_MSG(base_path, "EDGEHOMEDIR env variable not set. Line: " TOSTRING(__LINE__));
    size_t file_path_size = strlen(relative_path) + strlen(base_path) + 1;
    char *file_path = (char*)malloc(file_path_size);
    ASSERT_IS_NOT_NULL_WITH_MSG(file_path, "Line:" TOSTRING(__LINE__));
    memset(file_path, 0, file_path_size);
    strcat(file_path, base_path);
    strcat(file_path, relative_path);
    return file_path;
}

static void test_helper_obtain_edge_hub_certs(void)
{
    static const char *EDGE_HUB_CERT_PATH = "/certs/edge-hub-server/cert/edge-hub-server.cert.pem";
    static const char *EDGE_HUB_CHAIN_PATH = "/certs/edge-chain-ca/cert/edge-chain-ca.cert.pem";
    static const char *EDGE_HUB_PK_PATH = "/certs/edge-hub-server/private/edge-hub-server.key.pem";
    static const char *EDGE_HUB_ROOT_CA_PATH = "/certs/edge-device-ca/cert/edge-device-ca-root.cert.pem";

    char *file_path;

    file_path = cert_file_path_helper(EDGE_HUB_CERT_PATH);
    TEST_SERVER_CERT = read_file_into_cstring(file_path, NULL);
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_SERVER_CERT, "Line:" TOSTRING(__LINE__));
    free(file_path);
    file_path = cert_file_path_helper(EDGE_HUB_CHAIN_PATH);
    TEST_SERVER_CHAIN_CERT = read_file_into_cstring(file_path, NULL);
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_SERVER_CHAIN_CERT, "Line:" TOSTRING(__LINE__));
    free(file_path);
    file_path = cert_file_path_helper(EDGE_HUB_PK_PATH);
    TEST_SERVER_PRIVATE_KEY = read_file_into_cstring(file_path, NULL);
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_SERVER_PRIVATE_KEY, "Line:" TOSTRING(__LINE__));
    free(file_path);
    file_path = cert_file_path_helper(EDGE_HUB_ROOT_CA_PATH);
    TEST_TRUSTED_CA_CERT = read_file_into_cstring(file_path, NULL);
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_TRUSTED_CA_CERT, "Line:" TOSTRING(__LINE__));
    free(file_path);
}

static void test_helper_release_edge_hub_certs(void)
{
    if (TEST_SERVER_CERT) free(TEST_SERVER_CERT);
    if (TEST_SERVER_CHAIN_CERT) free(TEST_SERVER_CHAIN_CERT);
    if (TEST_SERVER_PRIVATE_KEY) free(TEST_SERVER_PRIVATE_KEY);
    if (TEST_TRUSTED_CA_CERT) free(TEST_TRUSTED_CA_CERT);
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
    set_common_name(certificate_props, "test_ca_cert");
    set_validity_seconds(certificate_props, 3600);
    set_alias(certificate_props, TEST_CA_ALIAS);
    set_issuer_alias(certificate_props, DEVICE_CA_ALIAS);
    set_certificate_type(certificate_props, CERTIFICATE_TYPE_CA);
    return certificate_props;
}

static CERT_PROPS_HANDLE test_helper_create_server_cert_properties(void)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(certificate_props, "Line:" TOSTRING(__LINE__));
    set_common_name(certificate_props, "test_server_cert");
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
    set_common_name(certificate_props, "test_client_cert");
    set_validity_seconds(certificate_props, 3600);
    set_alias(certificate_props, TEST_CLIENT_ALIAS);
    set_issuer_alias(certificate_props, TEST_CA_ALIAS);
    set_certificate_type(certificate_props, CERTIFICATE_TYPE_CLIENT);
    return certificate_props;
}


//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_client_data.h"

//#############################################################################
// Test cases
//#############################################################################
BEGIN_TEST_SUITE(edge_hsm_crypto_int_tests)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_obtain_edge_hub_certs();
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        test_helper_release_edge_hub_certs();
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
        const char *certificate = certificate_info_get_certificate(result);
        const char *chain_certificate = certificate_info_get_chain(result);
        const void* private_key = certificate_info_get_private_key(result, &pk_size);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, strlen(TEST_SERVER_CERT), strlen(certificate), "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, strlen(TEST_SERVER_CHAIN_CERT), strlen(chain_certificate), "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, strlen(TEST_SERVER_PRIVATE_KEY), pk_size - 1, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, strcmp(TEST_SERVER_PRIVATE_KEY, (const char*)private_key), "Line:" TOSTRING(__LINE__));

        // cleanup
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_SERVER_ALIAS);
        interface->hsm_client_destroy_certificate(hsm_handle, TEST_CA_ALIAS);
        certificate_info_destroy(result);
        cert_properties_destroy(certificate_props);
        certificate_info_destroy(ca_cert_info);
        cert_properties_destroy(ca_certificate_props);
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
        ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));

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
        const void* private_key = certificate_info_get_private_key(result, &pk_size);
        ASSERT_IS_NOT_NULL_WITH_MSG(certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, strlen(TEST_TRUSTED_CA_CERT), strlen(certificate), "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(chain_certificate, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(private_key, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, pk_size, "Line:" TOSTRING(__LINE__));

        // cleanup
        certificate_info_destroy(result);
        test_helper_crypto_deinit(hsm_handle);
    }

END_TEST_SUITE(edge_hsm_crypto_int_tests)
