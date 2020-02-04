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

extern const char* const ENV_REGISTRATION_ID;
extern const char* const ENV_DEVICE_ID_CERTIFICATE_PATH;
extern const char* const ENV_DEVICE_ID_PRIVATE_KEY_PATH;

#define TEST_DEVICE_ID_CERT_RSA_FILE_NAME "rsa_device_cert.pem"
#define TEST_DEVICE_ID_PK_RSA_FILE_NAME "rsa_device_pk.pem"

static const char* TEST_RSA_CERT =
    "-----BEGIN CERTIFICATE-----\n" \
    "MIIEpzCCAo+gAwIBAgICEAEwDQYJKoZIhvcNAQELBQAwKjEoMCYGA1UEAwwfQXp1\n" \
    "cmVfSW9UX0h1Yl9DQV9DZXJ0X1Rlc3RfT25seTAeFw0xOTAxMDMyMjA3MjlaFw0y\n" \
    "MDAxMDMyMjA3MjlaMDsxOTA3BgNVBAMMMEE5QzM5MzY5ODQwNEMzNEQ1NEJFOUMx\n" \
    "OTFBRTA3QzBFMzI3QTJCMTkyQ0M1ODI5RjCCASIwDQYJKoZIhvcNAQEBBQADggEP\n" \
    "ADCCAQoCggEBAOVmKJmFAT9RcpHMDXySixF2G5bmb83uJG/ctMTCKZNIP6/Pqfl0\n" \
    "tCKgOtKiLpMFu0rIG/VVvqSuxzMpaM7FaxDe57FSiz4mCUQGkGcxuVlDSmeUA2oy\n" \
    "y4SRA0WrkxppqIjEyoBhpvfVzx+EhFjMX8QD4sXlNy5scMPbFx8JdPyIGWTEYaZv\n" \
    "DTTOgbJXy8evLj9uReHA5KkpxrEnfzME1RnCl85jSzfs/7vpzfJOu1iLnXc2b6uR\n" \
    "tdNkz+l9rl1ufs3DzjMO3rtpL/WLxuJfjHWRTlSGT/tQYvbf+orXuDDGjh3RIqdw\n" \
    "53NSBoj5w0Tvu5WfSxO/zeoO1xRjkJX0whECAwEAAaOBxTCBwjAJBgNVHRMEAjAA\n" \
    "MBEGCWCGSAGG+EIBAQQEAwIFoDAzBglghkgBhvhCAQ0EJhYkT3BlblNTTCBHZW5l\n" \
    "cmF0ZWQgQ2xpZW50IENlcnRpZmljYXRlMB0GA1UdDgQWBBSkMBHEgvjFYGOlt2Yc\n" \
    "JSKSeaW/7jAfBgNVHSMEGDAWgBQY2amEKHhQ7m4Hks9ZWGa7Y4c/YzAOBgNVHQ8B\n" \
    "Af8EBAMCBeAwHQYDVR0lBBYwFAYIKwYBBQUHAwIGCCsGAQUFBwMEMA0GCSqGSIb3\n" \
    "DQEBCwUAA4ICAQA/EViU62LDyOBx2f62lLP98sc+wv5NJ1Healoo54g7xI1ELIaV\n" \
    "IuncUVAxWL9SqII3i60ZlU3+ctIgit0UW/K8lD6nqUIsZO59udj5MlZ0ILVYRbFn\n" \
    "Uo5FhqkiewTkFE0hbxKYmcUs6ChTuTygINkwcdu6BDKroNAlOez7n8ZCzwcn1697\n" \
    "gDWhDlKAjh5aDDk4+D+Gf4E4M352nUKad9Yt4wHipIHKT6ZyErqzBLHs2rhB9cE3\n" \
    "kTNpPYbSZb9ASmXZFmLn9pSzDzlnj+6U7EsN/1JaT2PuzCVoDsjQ3vzM9MqfBUmG\n" \
    "JXC7xb9kC9MAr9fUSh9Zf9mqymXxLU6zLx/aOYBKz94H3JRvrU6pRnvoq5oYFRXC\n" \
    "dPeI4G1UL4HMJHsTTa5P3g18WvRMrtsLQtgCW31ZJHNvNOk0/B21p2P5qmt0aHTS\n" \
    "bMpBrhqItPH7hAFAkgEBjurEFlzn0ttChc6W9Oyy8uTETV9D4QQ/0zdxYQcHTm/l\n" \
    "cjqiG0OYvAyeQVrIJP7JrDDuxFAtp8wBsqOwX7W7T2uJ6XaOxH/gDQBKyq6lEry0\n" \
    "jXfCdvF2cj23LgVINAdEoaMmcGNc25JX3RB8t/ftc1g1akY2VkRQMKWmXKGNf3s5\n" \
    "SpYUgvIOgZ3xB9BLqAoFDgBdXpsCImolCLOuiP/VtPTJoYT+4cDthIDHoA==\n" \
    "-----END CERTIFICATE-----\n";

static const unsigned char TEST_PRIVATE_KEY[] = { 0x32, 0x03, 0x33, 0x34, 0x35, 0x36 };

static char * TEST_DEVICE_ID_CERT_RSA_FILE = NULL;
static char * TEST_DEVICE_ID_PK_RSA_FILE = NULL;

//#############################################################################
// Test helpers
//#############################################################################

static char* prepare_file_path(const char* base_dir, const char* file_name)
{
    size_t path_size = get_max_file_path_size();
    char *file_path = calloc(path_size, 1);
    ASSERT_IS_NOT_NULL(file_path, "Line:" TOSTRING(__LINE__));
    int status = snprintf(file_path, path_size, "%s%s%s", base_dir, SLASH, file_name);
    ASSERT_IS_TRUE(((status > 0) || (status < (int)path_size)), "Line:" TOSTRING(__LINE__));

    return file_path;
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

    TEST_DEVICE_ID_CERT_RSA_FILE = prepare_file_path(TEST_IOTEDGE_HOMEDIR, TEST_DEVICE_ID_CERT_RSA_FILE_NAME);
    TEST_DEVICE_ID_PK_RSA_FILE = prepare_file_path(TEST_IOTEDGE_HOMEDIR, TEST_DEVICE_ID_PK_RSA_FILE_NAME);

    status = write_cstring_to_file(TEST_DEVICE_ID_CERT_RSA_FILE, TEST_RSA_CERT);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    printf("Write device certificate to: [%s]\r\n", TEST_DEVICE_ID_CERT_RSA_FILE);

    status = write_buffer_to_file(TEST_DEVICE_ID_PK_RSA_FILE, TEST_PRIVATE_KEY, sizeof(TEST_PRIVATE_KEY), false);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    printf("Write device private key to: [%s]\r\n", TEST_DEVICE_ID_PK_RSA_FILE);
}

static void test_helper_teardown_homedir(void)
{
    delete_file(TEST_DEVICE_ID_CERT_RSA_FILE);
    free(TEST_DEVICE_ID_CERT_RSA_FILE);
    TEST_DEVICE_ID_CERT_RSA_FILE = NULL;

    delete_file(TEST_DEVICE_ID_PK_RSA_FILE);
    free(TEST_DEVICE_ID_PK_RSA_FILE);
    TEST_DEVICE_ID_PK_RSA_FILE = NULL;

    if ((TEST_IOTEDGE_HOMEDIR != NULL) && (TEST_IOTEDGE_HOMEDIR_GUID != NULL))
    {
        hsm_test_util_delete_dir(TEST_IOTEDGE_HOMEDIR_GUID);
        free(TEST_IOTEDGE_HOMEDIR);
        TEST_IOTEDGE_HOMEDIR = NULL;
        free(TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_IOTEDGE_HOMEDIR_GUID = NULL;
    }
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_client_x509_int)

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

    TEST_FUNCTION(hsm_client_x509_init_deinit_sanity)
    {
        //arrange

        // act
        hsm_client_x509_init(TEST_VALIDITY);

        // assert

        //cleanup
        hsm_client_x509_deinit();
    }

    TEST_FUNCTION(hsm_client_x509_get_certificate_expected_failure_always)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();
        ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));

        // act
        char* certificate = interface->hsm_client_get_cert(hsm_handle);

        // assert
        ASSERT_IS_NULL(certificate, "Line:" TOSTRING(__LINE__));

        //cleanup
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_get_private_key_expected_failure_always)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();
        ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));

        // act
        char* key = interface->hsm_client_get_key(hsm_handle);

        // assert
        ASSERT_IS_NULL(key, "Line:" TOSTRING(__LINE__));

        //cleanup
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_get_common_name_expected_failure_always)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();
        ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));

        // act
        char* name = interface->hsm_client_get_common_name(hsm_handle);

        // assert
        ASSERT_IS_NULL(name, "Line:" TOSTRING(__LINE__));

        //cleanup
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_get_certificate_info_with_missing_env_vars_fails)
    {
        //arrange
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();
        ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));

        //cleanup
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_e2e_with_provided_device_certs_succeeds)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
        int cmp_result = strcmp(TEST_RSA_CERT, certificate_info_get_certificate(result));
        ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
        size_t result_pk_size = 0;
        const void * result_pk = certificate_info_get_private_key(result, &result_pk_size);
        ASSERT_ARE_EQUAL(size_t, sizeof(TEST_PRIVATE_KEY), result_pk_size, "Line:" TOSTRING(__LINE__));
        cmp_result = memcmp(TEST_PRIVATE_KEY, result_pk, result_pk_size);
        ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(result);
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_e2e_with_invalid_device_cert_fails)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, "blah.txt");
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(result);
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_e2e_with_no_device_cert_env_var_fails)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, TEST_DEVICE_ID_PK_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(result);
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_e2e_with_invalid_device_pk_fails)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        hsm_test_util_setenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH, "blah.txt");
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(result);
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(ENV_DEVICE_ID_CERTIFICATE_PATH);
        hsm_test_util_unsetenv(ENV_DEVICE_ID_PRIVATE_KEY_PATH);
    }

    TEST_FUNCTION(hsm_client_x509_e2e_with_no_device_pk_env_var_fails)
    {
        //arrange
        hsm_test_util_setenv(ENV_DEVICE_ID_CERTIFICATE_PATH, TEST_DEVICE_ID_CERT_RSA_FILE);
        const HSM_CLIENT_X509_INTERFACE* interface = hsm_client_x509_interface();
        hsm_client_x509_init(TEST_VALIDITY);
        HSM_CLIENT_CREATE hsm_handle = interface->hsm_client_x509_create();

        // act
        CERT_INFO_HANDLE result = interface->hsm_client_get_cert_info(hsm_handle);

        // assert
        ASSERT_IS_NULL(result, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(result);
        interface->hsm_client_x509_destroy(hsm_handle);
        hsm_client_x509_deinit();
        hsm_test_util_unsetenv(TEST_DEVICE_ID_CERT_RSA_FILE);
    }

END_TEST_SUITE(edge_hsm_client_x509_int)
