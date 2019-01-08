// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

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
#include "umock_c_negative_tests.h"
#include "umocktypes_charptr.h"

//#############################################################################
// Declare and enable MOCK definitions
//#############################################################################

#define ENABLE_MOCKS

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/hmacsha256.h"

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################

#include "hsm_client_data.h"
#include "hsm_key.h"
#include "edge_sas_perform_sign_with_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

#define TEST_BUFFER_HANDLE (BUFFER_HANDLE)0x1000
#define TEST_DERIVED_BUFFER_HANDLE (BUFFER_HANDLE)0x1001
#define TEST_DIGEST_PTR (unsigned char*)0x5000

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;
static unsigned char TEST_KEY_DATA[] = {'A', 'B', 'C', 'D'};
static unsigned char TEST_DIGEST_DATA[] = {'D', 'I', 'G', 'E', 'S', 'T'};
static unsigned char TEST_DERIVED_DIGEST_DATA[] = {'D', 'I', 'G', 'E', 'S', 'T',
                                                   'D', 'E', 'R', 'I', 'V', 'E', 'D'};

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

static unsigned char* test_hook_BUFFER_u_char(BUFFER_HANDLE handle)
{
    unsigned char* result = NULL;
    if (handle == TEST_BUFFER_HANDLE)
    {
        result = TEST_DIGEST_DATA;
    }
    else if (handle == TEST_DERIVED_BUFFER_HANDLE)
    {
        result = TEST_DERIVED_DIGEST_DATA;
    }
    ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
    return result;
}

static size_t test_hook_BUFFER_length(BUFFER_HANDLE handle)
{
    size_t result = 0;
    if (handle == TEST_BUFFER_HANDLE)
    {
        result = sizeof(TEST_DIGEST_DATA);
    }
    else if (handle == TEST_DERIVED_BUFFER_HANDLE)
    {
        result = sizeof(TEST_DERIVED_DIGEST_DATA);
    }
    ASSERT_ARE_NOT_EQUAL(size_t, 0, result, "Line:" TOSTRING(__LINE__));
    return result;
}

static HMACSHA256_RESULT test_hook_HMACSHA256_ComputeHash
(
    const unsigned char* key,
    size_t keyLen,
    const unsigned char* payload,
    size_t payloadLen,
    BUFFER_HANDLE hash
)
{
    (void)key;
    (void)keyLen;
    (void)payload;
    (void)payloadLen;
    (void)hash;
    return HMACSHA256_OK;
}

//#############################################################################
// Test helpers
//#############################################################################

static KEY_HANDLE test_helper_create_key(const unsigned char* key, size_t key_len)
{
    KEY_HANDLE key_handle = create_sas_key(key, key_len);
    ASSERT_IS_NOT_NULL(key_handle, "Line:" TOSTRING(__LINE__));
    return key_handle;
}

static void test_helper_destroy_key(KEY_HANDLE key_handle)
{
    ASSERT_IS_NOT_NULL(key_handle, "Line:" TOSTRING(__LINE__));
    const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
    key_if->hsm_client_key_destroy(key_handle);
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_key_interface_sas_key_unittests)

        TEST_SUITE_INITIALIZE(TestClassInitialize)
        {
            TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
            g_testByTest = TEST_MUTEX_CREATE();
            ASSERT_IS_NOT_NULL(g_testByTest);

            umock_c_init(test_hook_on_umock_c_error);

            REGISTER_UMOCK_ALIAS_TYPE(BUFFER_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(KEY_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(HMACSHA256_RESULT, int);

            ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, test_hook_gballoc_calloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, test_hook_gballoc_realloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);

            REGISTER_GLOBAL_MOCK_HOOK(BUFFER_length, test_hook_BUFFER_length);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_length, 0);

            REGISTER_GLOBAL_MOCK_HOOK(BUFFER_u_char, test_hook_BUFFER_u_char);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_u_char, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(HMACSHA256_ComputeHash, test_hook_HMACSHA256_ComputeHash);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(HMACSHA256_ComputeHash, HMACSHA256_ERROR);
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

        TEST_FUNCTION(hsm_client_key_interface_success)
        {
            // arrange

            // act
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();

            // assert
            ASSERT_IS_NOT_NULL(key_if, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(key_if->hsm_client_key_sign, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(key_if->hsm_client_key_derive_and_sign, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(key_if->hsm_client_key_encrypt, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(key_if->hsm_client_key_decrypt, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(key_if->hsm_client_key_destroy, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(hsm_client_key_interface_create_success)
        {
            // arrange
            EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_KEY_DATA)));

            // act
            KEY_HANDLE key_handle = create_sas_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));

            // assert
            ASSERT_IS_NOT_NULL(key_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            // cleanup
            test_helper_destroy_key(key_handle);
        }

        TEST_FUNCTION(hsm_client_key_interface_create_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_KEY_DATA)));
            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                KEY_HANDLE key_handle = create_sas_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));

                // assert
                ASSERT_IS_NULL(key_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            umock_c_negative_tests_deinit();
        }

        TEST_FUNCTION(hsm_client_key_interface_create_invalid_param)
        {
            // arrange
            KEY_HANDLE key_handle;

            // act, assert
            key_handle = create_sas_key(TEST_KEY_DATA, 0);
            ASSERT_IS_NULL(key_handle, "Line:" TOSTRING(__LINE__));
            key_handle = create_sas_key(NULL, sizeof(TEST_KEY_DATA));
            ASSERT_IS_NULL(key_handle, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(hsm_client_key_interface_destroy_invalid_param)
        {
            // arrange
            KEY_HANDLE key_handle = NULL;
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();

            // act
            key_if->hsm_client_key_destroy(key_handle);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(hsm_client_key_interface_destroy_success)
        {
            // arrange
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));
            umock_c_reset_all_calls();

            EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
            EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

            // act
            key_if->hsm_client_key_destroy(key_handle);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(hsm_client_key_sign_interface_invalid_params)
        {
            // arrange
            int status;
            unsigned char data_to_be_signed[] = "data";
            size_t data_len = sizeof(data_to_be_signed);
            unsigned char* digest = TEST_DIGEST_PTR;
            size_t digest_size = 10;
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));

            // act, assert
            status = key_if->hsm_client_key_sign(NULL, data_to_be_signed, data_len, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_sign(key_handle, NULL, data_len, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_sign(key_handle, data_to_be_signed, 0, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_sign(key_handle, data_to_be_signed, data_len, NULL, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_sign(key_handle, data_to_be_signed, data_len, &digest, NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));

            // cleanup
            test_helper_destroy_key(key_handle);
        }

        TEST_FUNCTION(hsm_client_key_sign_interface_success)
        {
            // arrange
            int status;
            unsigned char data_to_be_signed[] = "data";
            size_t data_len = sizeof(data_to_be_signed);
            unsigned char* digest = NULL;
            size_t digest_size = 0;
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));
            umock_c_reset_all_calls();

            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_BUFFER_HANDLE);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_KEY_DATA), data_to_be_signed, data_len, TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DIGEST_DATA)));
            STRICT_EXPECTED_CALL(BUFFER_delete(TEST_BUFFER_HANDLE));

            // act
            status = key_if->hsm_client_key_sign(key_handle, data_to_be_signed, data_len, &digest, &digest_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, sizeof(TEST_DIGEST_DATA), digest_size, "Line:" TOSTRING(__LINE__));
            status = memcmp(TEST_DIGEST_DATA, digest, sizeof(TEST_DIGEST_DATA));
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // cleanup
            test_hook_gballoc_free(digest);
            test_helper_destroy_key(key_handle);
        }

        TEST_FUNCTION(hsm_client_key_sign_interface_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            unsigned char data_to_be_signed[] = "data";
            size_t data_len = sizeof(data_to_be_signed);
            unsigned char* digest = NULL;
            size_t digest_size = 0;
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));
            umock_c_reset_all_calls();

            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_BUFFER_HANDLE);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_KEY_DATA), data_to_be_signed, data_len, TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DIGEST_DATA)));
            // note BUFFER_delete does not fail
            //STRICT_EXPECTED_CALL(BUFFER_delete(TEST_BUFFER_HANDLE));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = key_if->hsm_client_key_sign(key_handle, data_to_be_signed, data_len, &digest, &digest_size);

                // assert
                ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            test_helper_destroy_key(key_handle);
            umock_c_negative_tests_deinit();
        }

        TEST_FUNCTION(hsm_client_key_derive_and_sign_interface_invalid_params)
        {
            // arrange
            int status;
            unsigned char* data_to_be_signed = (unsigned char*)"data";
            size_t data_len = 0;
            unsigned char* digest = NULL;
            size_t digest_size = 0;
            unsigned char identity[] = "identity";
            size_t identity_size = sizeof(identity);
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));

            // act, assert
            status = key_if->hsm_client_key_derive_and_sign(NULL, data_to_be_signed, data_len, identity, identity_size, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, NULL, data_len, identity, identity_size, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, 0, identity, identity_size, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, NULL, identity_size, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, identity, 0, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, identity, identity_size, NULL, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, 0, digest_size, "Line:" TOSTRING(__LINE__));

            digest = TEST_DIGEST_PTR;
            digest_size = 10;
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, identity, identity_size, &digest, NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(digest, "Line:" TOSTRING(__LINE__));

            // cleanup
            test_helper_destroy_key(key_handle);
        }

        TEST_FUNCTION(hsm_client_key_derive_and_sign_interface_success)
        {
            // arrange
            int status;
            unsigned char data_to_be_signed[] = "data";
            size_t data_len = sizeof(data_to_be_signed);
            unsigned char* digest = NULL;
            size_t digest_size = 0;
            unsigned char identity[] = "identity";
            size_t identity_size = sizeof(identity);
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));
            umock_c_reset_all_calls();

            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_BUFFER_HANDLE);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_KEY_DATA), identity, identity_size, TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DIGEST_DATA)));
            STRICT_EXPECTED_CALL(BUFFER_delete(TEST_BUFFER_HANDLE));
            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_DERIVED_BUFFER_HANDLE);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_DIGEST_DATA), data_to_be_signed, data_len, TEST_DERIVED_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_DERIVED_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_DERIVED_BUFFER_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DERIVED_DIGEST_DATA)));
            STRICT_EXPECTED_CALL(BUFFER_delete(TEST_DERIVED_BUFFER_HANDLE));
            EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

            // act
            status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, identity, identity_size, &digest, &digest_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            status = memcmp(TEST_DERIVED_DIGEST_DATA, digest, sizeof(TEST_DERIVED_DIGEST_DATA));
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, sizeof(TEST_DERIVED_DIGEST_DATA), digest_size, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            // cleanup
            test_hook_gballoc_free(digest);
            test_helper_destroy_key(key_handle);
        }

        TEST_FUNCTION(hsm_client_key_derive_and_sign_interface_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            unsigned char data_to_be_signed[] = "data";
            size_t data_len = sizeof(data_to_be_signed);
            unsigned char* digest = NULL;
            size_t digest_size = 0;
            unsigned char identity[] = "identity";
            size_t identity_size = sizeof(identity);
            const HSM_CLIENT_KEY_INTERFACE* key_if = hsm_client_key_interface();
            KEY_HANDLE key_handle = test_helper_create_key(TEST_KEY_DATA, sizeof(TEST_KEY_DATA));
            uint64_t failedFunctionBitmask = 0;
            size_t i = 0;
            umock_c_reset_all_calls();

            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_BUFFER_HANDLE);
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_KEY_DATA), identity, identity_size, TEST_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DIGEST_DATA)));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_delete(TEST_BUFFER_HANDLE));
            i++;
            EXPECTED_CALL(BUFFER_new())
                .SetReturn(TEST_DERIVED_BUFFER_HANDLE);
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(HMACSHA256_ComputeHash(IGNORED_PTR_ARG, sizeof(TEST_DIGEST_DATA), data_to_be_signed, data_len, TEST_DERIVED_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_length(TEST_DERIVED_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_u_char(TEST_DERIVED_BUFFER_HANDLE));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(gballoc_malloc(sizeof(TEST_DERIVED_DIGEST_DATA)));
            failedFunctionBitmask |= ((uint64_t)1 << i++);
            STRICT_EXPECTED_CALL(BUFFER_delete(TEST_DERIVED_BUFFER_HANDLE));
            i++;
            EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
            i++;
            umock_c_negative_tests_snapshot();

            for (i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);
                if (failedFunctionBitmask & ((uint64_t)1 << i))
                {
                    // act
                    status = key_if->hsm_client_key_derive_and_sign(key_handle, data_to_be_signed, data_len, identity, identity_size, &digest, &digest_size);

                    // assert
                    ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
                }
            }

            //cleanup
            test_helper_destroy_key(key_handle);
            umock_c_negative_tests_deinit();
        }

END_TEST_SUITE(edge_hsm_key_interface_sas_key_unittests)
