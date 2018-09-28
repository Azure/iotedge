// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/sastoken.h"
#include "azure_c_shared_utility/urlencode.h"
#include "azure_c_shared_utility/hmacsha256.h"
#include "azure_c_shared_utility/base64.h"
#include "azure_c_shared_utility/agenttime.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/xlogging.h"
#include "azure_c_shared_utility/crt_abstractions.h"

#include "hsm_client_data.h"

//#############################################################################
// Test defines and data
//#############################################################################
#define TEST_DATA_TO_BE_SIGNED "The quick brown fox jumped over the lazy dog"
#define TEST_KEY_BASE64 "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA="
#define TEST_HOSTNAME  "somehost.azure-devices.net"
#define TEST_DEVICE_ID "some-device-id"
#define TEST_MODULE_ID "some-module-id"
#define TEST_GEN_ID "1"
#define PRIMARY_URI "primary"
#define SECONDARY_URI "secondary"

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_homedir(void)
{
    TEST_IOTEDGE_HOMEDIR = hsm_test_util_create_temp_dir(&TEST_IOTEDGE_HOMEDIR_GUID);
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_IOTEDGE_HOMEDIR_GUID, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(TEST_IOTEDGE_HOMEDIR, "Line:" TOSTRING(__LINE__));

    printf("Temp dir created: [%s]\r\n", TEST_IOTEDGE_HOMEDIR);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);
}

static void test_helper_tear_down_homedir(void)
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

static HSM_CLIENT_HANDLE tpm_provision(void)
{
    int status;
    status = hsm_client_tpm_init();
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
    HSM_CLIENT_HANDLE result = interface->hsm_client_tpm_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
    return result;
}

static void tpm_activate_key
(
    HSM_CLIENT_HANDLE hsm_handle,
    const unsigned char* key,
    size_t key_size
)
{
    const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
    int status = interface->hsm_client_activate_identity_key(hsm_handle, key, key_size);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
}

static int tpm_sign
(
    HSM_CLIENT_HANDLE hsm_handle,
    const unsigned char* derived_identity,
    size_t derived_identity_size,
    const unsigned char* data,
    size_t data_len,
    BUFFER_HANDLE hash
)
{
    const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
    unsigned char *digest;
    size_t digest_size;
    int status;
    if (derived_identity == NULL)
    {
        status = interface->hsm_client_sign_with_identity(hsm_handle, data, data_len,
                                                          &digest, &digest_size);
    }
    else
    {
        status = interface->hsm_client_derive_and_sign_with_identity(hsm_handle,
                                                                     data, data_len,
                                                                     derived_identity,
                                                                     derived_identity_size,
                                                                     &digest,
                                                                     &digest_size);
    }
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    status = BUFFER_build(hash, digest, digest_size);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    free(digest);
    return status;
}

static void tpm_deprovision(HSM_CLIENT_HANDLE hsm_handle)
{
    const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
    interface->hsm_client_tpm_destroy(hsm_handle);
    hsm_client_tpm_deinit();
}

static BUFFER_HANDLE test_helper_base64_converter(const char* input)
{
    BUFFER_HANDLE result = Base64_Decoder(input);
    ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
    size_t out_len = BUFFER_length(result);
    ASSERT_ARE_NOT_EQUAL_WITH_MSG(size_t, 0, out_len, "Line:" TOSTRING(__LINE__));
    unsigned char* out_buffer = BUFFER_u_char(result);
    ASSERT_IS_NOT_NULL_WITH_MSG(out_buffer, "Line:" TOSTRING(__LINE__));
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
    ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
    status = HMACSHA256_ComputeHash(BUFFER_u_char(key_handle), BUFFER_length(key_handle),
                                    input, input_size, result);
    ASSERT_ARE_EQUAL_WITH_MSG(int, (int)HMACSHA256_OK, status, "Line:" TOSTRING(__LINE__));
    return result;
}

static HSM_CLIENT_HANDLE test_helper_init_tpm_and_activate_key(BUFFER_HANDLE key_handle)
{
    HSM_CLIENT_HANDLE hsm_handle = tpm_provision();
    tpm_activate_key(hsm_handle, BUFFER_u_char(key_handle), BUFFER_length(key_handle));
    return hsm_handle;
}

static STRING_HANDLE tpm_construct_sas_token
(
    HSM_CLIENT_HANDLE hsm_handle,
    const unsigned char* derived_identity,
    size_t derived_identity_size,
    const char* scope,
    const char* keyname,
    size_t expiry
)
{
    STRING_HANDLE result;

    char tokenExpirationTime[32] = { 0 };

    if (size_tToString(tokenExpirationTime, sizeof(tokenExpirationTime), expiry) != 0)
    {
        LogError("Converting seconds to a string failed.  No SAS can be generated.");
        result = NULL;
    }
    else
    {
        STRING_HANDLE toBeHashed = NULL;
        BUFFER_HANDLE hash = NULL;
        if (((hash = BUFFER_new()) == NULL) ||
            ((toBeHashed = STRING_new()) == NULL) ||
            ((result = STRING_new()) == NULL))
        {
            LogError("Unable to allocate memory to prepare SAS token.");
            result = NULL;
        }
        else
        {
            if ((STRING_concat(toBeHashed, scope) != 0) ||
                (STRING_concat(toBeHashed, "\n") != 0) ||
                (STRING_concat(toBeHashed, tokenExpirationTime) != 0))
            {
                LogError("Unable to build the input to the HMAC to prepare SAS token.");
                STRING_delete(result);
                result = NULL;
            }
            else
            {
                STRING_HANDLE base64Signature = NULL;
                STRING_HANDLE urlEncodedSignature = NULL;
                size_t inLen = STRING_length(toBeHashed);
                const unsigned char* inBuf = (const unsigned char*)STRING_c_str(toBeHashed);
                if ((tpm_sign(hsm_handle, derived_identity, derived_identity_size, inBuf, inLen, hash) != 0) ||
                    ((base64Signature = Base64_Encoder(hash)) == NULL) ||
                    ((urlEncodedSignature = URL_Encode(base64Signature)) == NULL) ||
                    (STRING_copy(result, "SharedAccessSignature sr=") != 0) ||
                    (STRING_concat(result, scope) != 0) ||
                    (STRING_concat(result, "&sig=") != 0) ||
                    (STRING_concat_with_STRING(result, urlEncodedSignature) != 0) ||
                    (STRING_concat(result, "&se=") != 0) ||
                    (STRING_concat(result, tokenExpirationTime) != 0) ||
                    ((keyname != NULL) && (STRING_concat(result, "&skn=") != 0)) ||
                    ((keyname != NULL) && (STRING_concat(result, keyname) != 0)))
                {
                    LogError("Unable to build the SAS token.");
                    STRING_delete(result);
                    result = NULL;
                }
                STRING_delete(base64Signature);
                STRING_delete(urlEncodedSignature);
            }
        }
        STRING_delete(toBeHashed);
        BUFFER_delete(hash);
    }
    return result;
}

//#############################################################################
// Test functions
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_sas_auth_int_tests)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        test_helper_tear_down_homedir();
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

    // This tests the following:
    //  1) A well known identity key K can be installed in the TPM
    //  2) The HMACSHA256 digest sign request for a known payload DATA
    //     should return a digest whose value would be the same as would be
    //     expected by performing an actual HMACSHA256(K, DATA) computation.
    TEST_FUNCTION(hsm_client_key_interface_basic_sign_sanity)
    {
        // arrange
        unsigned char test_data_to_be_signed[] = TEST_DATA_TO_BE_SIGNED;
        size_t test_data_to_be_signed_size = sizeof(test_data_to_be_signed);
        char test_key[] = TEST_KEY_BASE64;
        BUFFER_HANDLE decoded_key = test_helper_base64_converter(test_key);

        // compute expected result
        BUFFER_HANDLE test_expected_digest = test_helper_compute_hmac(decoded_key,
                                                                      test_data_to_be_signed,
                                                                      test_data_to_be_signed_size);

        // act
        BUFFER_HANDLE test_output_digest = BUFFER_new();
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output_digest, "Line:" TOSTRING(__LINE__));
        HSM_CLIENT_HANDLE hsm_handle = test_helper_init_tpm_and_activate_key(decoded_key);
        tpm_sign(hsm_handle, NULL, 0, test_data_to_be_signed,
                 test_data_to_be_signed_size, test_output_digest);

        // assert
        STRING_HANDLE expected = Base64_Encoder(test_expected_digest);
        STRING_HANDLE result = Base64_Encoder(test_output_digest);
        printf("Expected: %s\r\n", STRING_c_str(expected));
        printf("Got Result: %s\r\n", STRING_c_str(result));
        ASSERT_ARE_EQUAL(int, 0, STRING_compare(expected, result));

        // cleanup
        STRING_delete(expected);
        STRING_delete(result);
        BUFFER_delete(test_output_digest);
        BUFFER_delete(test_expected_digest);
        BUFFER_delete(decoded_key);
        tpm_deprovision(hsm_handle);
    }

    // This tests the following:
    //  1) A well known identity key K can be installed in the TPM
    //  2) For a specific derived identity IDderived a HMACSHA256 digest sign request
    //     should return a digest whose value would be obtained
    //     by performing the following computations:
    //     Kderived = HMACSHA256(K, IDderived)
    //     digest   = HMACSHA256(Kderived, DATA)
    TEST_FUNCTION(hsm_client_key_interface_basic_derive_and_sign_sanity)
    {
        // arrange
        unsigned char test_data_to_be_signed[] = TEST_DATA_TO_BE_SIGNED;
        size_t test_data_to_be_signed_size = sizeof(test_data_to_be_signed);
        char primary_fqmid[] = TEST_HOSTNAME "/devices/" TEST_DEVICE_ID "/modules/" \
                               TEST_MODULE_ID "/" PRIMARY_URI "/" TEST_GEN_ID;
        char test_key[] = TEST_KEY_BASE64;
        BUFFER_HANDLE decoded_key = test_helper_base64_converter(test_key);

        // compute expected result
        BUFFER_HANDLE test_expected_primary_key_buf = test_helper_compute_hmac(decoded_key,
                                                                               (unsigned char*)primary_fqmid,
                                                                               strlen(primary_fqmid));

        BUFFER_HANDLE test_expected_digest = test_helper_compute_hmac(test_expected_primary_key_buf,
                                                                      test_data_to_be_signed,
                                                                      test_data_to_be_signed_size);

        // act
        BUFFER_HANDLE test_output_digest = BUFFER_new();
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output_digest, "Line:" TOSTRING(__LINE__));
        HSM_CLIENT_HANDLE hsm_handle = test_helper_init_tpm_and_activate_key(decoded_key);
        tpm_sign(hsm_handle, (unsigned char*)primary_fqmid, strlen(primary_fqmid),
                 test_data_to_be_signed, test_data_to_be_signed_size, test_output_digest);

        // assert
        STRING_HANDLE expected = Base64_Encoder(test_expected_digest);
        STRING_HANDLE result = Base64_Encoder(test_output_digest);
        printf("Expected digest: %s, Result digest %s\r\n",
               STRING_c_str(expected), STRING_c_str(result));
        ASSERT_ARE_EQUAL(int, 0, STRING_compare(expected, result));

        // cleanup
        STRING_delete(expected);
        STRING_delete(result);
        BUFFER_delete(test_output_digest);
        BUFFER_delete(test_expected_digest);
        BUFFER_delete(test_expected_primary_key_buf);
        BUFFER_delete(decoded_key);
        tpm_deprovision(hsm_handle);
    }

    // Test case attempts to demonstrate and validate how module primary and secondary
    // keys are to be derived when registering modules
    TEST_FUNCTION(hsm_client_key_interface_obtain_primary_and_secondary_module_keys)
    {
        // arrange
        char primary_fqmid[] = TEST_HOSTNAME "/devices/" TEST_DEVICE_ID "/modules/" \
                               TEST_MODULE_ID "/" PRIMARY_URI "/" TEST_GEN_ID;
        char secondary_fqmid[] = TEST_HOSTNAME "/devices/" TEST_DEVICE_ID "/modules/" \
                                 TEST_MODULE_ID "/" SECONDARY_URI "/" TEST_GEN_ID;
        char identity_key[] = TEST_KEY_BASE64;
        BUFFER_HANDLE decoded_key = test_helper_base64_converter(identity_key);

        // compute expected result
        BUFFER_HANDLE test_expected_primary_key_buf = test_helper_compute_hmac(decoded_key,
                                                                               (unsigned char*)primary_fqmid,
                                                                               strlen(primary_fqmid));

        BUFFER_HANDLE test_expected_secondary_key_buf = test_helper_compute_hmac(decoded_key,
                                                                                 (unsigned char*)secondary_fqmid,
                                                                                 strlen(secondary_fqmid));

        // act
        BUFFER_HANDLE test_output_primary_key_buf = BUFFER_new();
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output_primary_key_buf, "Line:" TOSTRING(__LINE__));
        BUFFER_HANDLE test_output_secondary_key_buf = BUFFER_new();
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output_secondary_key_buf, "Line:" TOSTRING(__LINE__));

        HSM_CLIENT_HANDLE hsm_handle = test_helper_init_tpm_and_activate_key(decoded_key);
        tpm_sign(hsm_handle, NULL, 0, (unsigned char*)primary_fqmid, strlen(primary_fqmid), test_output_primary_key_buf);
        tpm_sign(hsm_handle, NULL, 0, (unsigned char*)secondary_fqmid, strlen(secondary_fqmid), test_output_secondary_key_buf);

        // assert
        STRING_HANDLE expected_primary_key_str = Base64_Encoder(test_expected_primary_key_buf);
        STRING_HANDLE expected_secondary_key_str = Base64_Encoder(test_expected_secondary_key_buf);
        STRING_HANDLE result_primary_key_str = Base64_Encoder(test_output_primary_key_buf);
        STRING_HANDLE result_secondary_key_str = Base64_Encoder(test_output_secondary_key_buf);
        printf("Expected Primary Key: %s, Result Primary Key %s\r\n",
               STRING_c_str(expected_primary_key_str), STRING_c_str(result_primary_key_str));
        printf("Expected Secondary Key: %s, Result Secondary Key %s\r\n",
               STRING_c_str(expected_secondary_key_str), STRING_c_str(result_secondary_key_str));
        ASSERT_ARE_EQUAL(int, 0, STRING_compare(expected_primary_key_str, result_primary_key_str));
        ASSERT_ARE_EQUAL(int, 0, STRING_compare(expected_secondary_key_str, result_secondary_key_str));

        // cleanup
        STRING_delete(expected_primary_key_str);
        STRING_delete(expected_secondary_key_str);
        STRING_delete(result_primary_key_str);
        STRING_delete(result_secondary_key_str);
        BUFFER_delete(test_expected_primary_key_buf);
        BUFFER_delete(test_expected_secondary_key_buf);
        BUFFER_delete(test_output_primary_key_buf);
        BUFFER_delete(test_output_secondary_key_buf);
        BUFFER_delete(decoded_key);
        tpm_deprovision(hsm_handle);
    }

    // This tests the following:
    //  1) A well known shared access key (base64) can be installed in the TPM
    //  2) Build a IoT Hub device SAS token to be signed by the identity key in the TPM
    TEST_FUNCTION(hsm_client_key_interface_device_token_generation)
    {
        // arrange
        char hostname[] = TEST_HOSTNAME;
        char device_id[] = TEST_DEVICE_ID;
        char device_identity_key[] = TEST_KEY_BASE64;
        time_t currentTime = time(NULL);
        size_t expiry_time = (size_t)(currentTime + (365 * 24 * 60 * 60));

        BUFFER_HANDLE decoded_key = test_helper_base64_converter(device_identity_key);

        // act
        HSM_CLIENT_HANDLE hsm_handle = tpm_provision();
        tpm_activate_key(hsm_handle, BUFFER_u_char(decoded_key), BUFFER_length(decoded_key));
        STRING_HANDLE token = tpm_construct_sas_token(hsm_handle, NULL, 0, hostname, device_id, expiry_time);
        ASSERT_IS_NOT_NULL_WITH_MSG(token, "Line:" TOSTRING(__LINE__));
        printf("TPM Generated Token: [%s]\n", STRING_c_str(token));

        // cleanup
        STRING_delete(token);
        BUFFER_delete(decoded_key);
        tpm_deprovision(hsm_handle);
    }

END_TEST_SUITE(edge_hsm_sas_auth_int_tests)
