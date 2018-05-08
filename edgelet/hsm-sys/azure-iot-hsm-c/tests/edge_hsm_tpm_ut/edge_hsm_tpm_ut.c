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
#include "hsm_client_store.h"
#include "azure_c_shared_utility/gballoc.h"

MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_create, const char*, store_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_destroy, const char*, store_name);
MOCKABLE_FUNCTION(, HSM_CLIENT_STORE_HANDLE, mocked_hsm_client_store_open, const char*, store_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_close, HSM_CLIENT_STORE_HANDLE, handle);
MOCKABLE_FUNCTION(, KEY_HANDLE, mocked_hsm_client_store_open_key, HSM_CLIENT_STORE_HANDLE, handle, const char*, key_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_close_key, HSM_CLIENT_STORE_HANDLE, handle, KEY_HANDLE, key_handle);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_remove_key, HSM_CLIENT_STORE_HANDLE, handle, const char*, key_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_insert_sas_key, HSM_CLIENT_STORE_HANDLE, handle, const char*, key_name, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, const HSM_CLIENT_STORE_INTERFACE*, hsm_client_store_interface);
MOCKABLE_FUNCTION(, const HSM_CLIENT_KEY_INTERFACE*, hsm_client_key_interface);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_sign, KEY_HANDLE, key_handle, const unsigned char*, data_to_be_signed, size_t, data_len, unsigned char**, digest, size_t*, digest_size);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_derive_and_sign, KEY_HANDLE, key_handle, const unsigned char*, data_to_be_signed, size_t, data_len, const unsigned char*, identity, size_t, identity_size, unsigned char**, digest, size_t*, digest_size);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_client_data.h"

//#############################################################################
// Test defines and data
//#############################################################################

#define TEST_EDGE_STORE_NAME "edgelet"
#define TEST_HSM_STORE_HANDLE (HSM_CLIENT_STORE_HANDLE)0x1000
#define TEST_KEY_HANDLE (KEY_HANDLE)0x1001
#define TEST_HSM_CLIENT_HANDLE (HSM_CLIENT_HANDLE)0x1002
#define TEST_SAS_KEY_NAME "edgelet-identity"
#define TEST_OUTPUT_DIGEST_PTR (unsigned char*)0x5000

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;
static unsigned char TEST_EDGE_MODULE_IDENTITY[] = {'s', 'a', 'm', 'p', 'l', 'e'};

static const HSM_CLIENT_STORE_INTERFACE mocked_hsm_client_store_interface =
{
    mocked_hsm_client_store_create,
    mocked_hsm_client_store_destroy,
    mocked_hsm_client_store_open,
    mocked_hsm_client_store_close,
    mocked_hsm_client_store_open_key,
    mocked_hsm_client_store_close_key,
    mocked_hsm_client_store_remove_key,
    mocked_hsm_client_store_insert_sas_key
};

static const HSM_CLIENT_KEY_INTERFACE mocked_hsm_client_key_interface =
{
    mocked_hsm_client_key_sign,
    mocked_hsm_client_key_derive_and_sign
};

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

const HSM_CLIENT_STORE_INTERFACE* test_hook_hsm_client_store_interface(void)
{
    return &mocked_hsm_client_store_interface;
}

const HSM_CLIENT_KEY_INTERFACE* test_hook_hsm_client_key_interface(void)
{
    return &mocked_hsm_client_key_interface;
}

static int test_hook_hsm_client_store_create(const char* store_name)
{
    return 0;
}

static int test_hook_hsm_client_store_destroy(const char* store_name)
{
    return 0;
}

static HSM_CLIENT_STORE_HANDLE test_hook_hsm_client_store_open(const char* store_name)
{
    return TEST_HSM_STORE_HANDLE;
}

static int test_hook_hsm_client_store_close(HSM_CLIENT_STORE_HANDLE handle)
{
    return 0;
}

static KEY_HANDLE test_hook_hsm_client_store_open_key(HSM_CLIENT_STORE_HANDLE handle, const char* key_name)
{
    return TEST_KEY_HANDLE;
}

static int test_hook_hsm_client_store_close_key(HSM_CLIENT_STORE_HANDLE handle, KEY_HANDLE key_handle)
{
    return 0;
}

static int test_hook_hsm_client_store_remove_key(HSM_CLIENT_STORE_HANDLE handle, const char* key_name)
{
    return 0;
}

static int test_hook_hsm_client_store_insert_sas_key(HSM_CLIENT_STORE_HANDLE handle, const char* key_name, const unsigned char* key, size_t key_len)
{
    return 0;
}

static int test_hook_hsm_key_sign(KEY_HANDLE key_handle, const unsigned char* data_to_be_signed, size_t data_len, unsigned char** digest, size_t* digest_size)
{
    return 0;
}

static int test_hook_hsm_key_derive_and_sign(KEY_HANDLE key_handle, const unsigned char* data_to_be_signed, size_t data_len, const unsigned char* identity, size_t identity_size, unsigned char** digest, size_t* digest_size)
{
    return 0;
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_tpm_unittests)

        TEST_SUITE_INITIALIZE(TestClassInitialize)
        {
            TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
            g_testByTest = TEST_MUTEX_CREATE();
            ASSERT_IS_NOT_NULL(g_testByTest);

            umock_c_init(test_hook_on_umock_c_error);

            REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_STORE_INTERFACE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_STORE_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_KEY_INTERFACE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(KEY_HANDLE, void*);

            ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, test_hook_gballoc_calloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, test_hook_gballoc_realloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);

            REGISTER_GLOBAL_MOCK_HOOK(hsm_client_store_interface, test_hook_hsm_client_store_interface);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_store_interface, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(hsm_client_key_interface, test_hook_hsm_client_key_interface);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_key_interface, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_create, test_hook_hsm_client_store_create);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_create, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_destroy, test_hook_hsm_client_store_destroy);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_destroy, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_open, test_hook_hsm_client_store_open);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_open, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_close, test_hook_hsm_client_store_close);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_close, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_open_key, test_hook_hsm_client_store_open_key);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_open_key, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_close_key, test_hook_hsm_client_store_close_key);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_close_key, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_remove_key, test_hook_hsm_client_store_remove_key);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_remove_key, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_insert_sas_key, test_hook_hsm_client_store_insert_sas_key);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_insert_sas_key, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_sign, test_hook_hsm_key_sign);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_sign, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_derive_and_sign, test_hook_hsm_key_derive_and_sign);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_derive_and_sign, 1);
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

        /**
         * Test function for API
         *   hsm_client_tpm_init
        */
        TEST_FUNCTION(hsm_client_tpm_init_success)
        {
            //arrange
            int status;
            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME));

            // act
            status = hsm_client_tpm_init();

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_init
        */
        TEST_FUNCTION(hsm_client_tpm_init_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                int status;
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_tpm_init();

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_init
        */
        TEST_FUNCTION(hsm_client_tpm_init_multiple_times_fails)
        {
            //arrange
            int status;
            hsm_client_tpm_init();
            umock_c_reset_all_calls();

            // act
            status = hsm_client_tpm_init();

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_deinit
        */
        TEST_FUNCTION(hsm_client_tpm_deinit_success)
        {
            //arrange
            (void)hsm_client_tpm_init();
            umock_c_reset_all_calls();

            // act
            hsm_client_tpm_deinit();

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
        }

        /**
         * Test function for API
         *   hsm_client_tpm_init
         *   hsm_client_tpm_deinit
        */
        TEST_FUNCTION(hsm_client_tpm_init_deinit_init_success)
        {
            //arrange
            int status;
            hsm_client_tpm_init();
            hsm_client_tpm_deinit();
            umock_c_reset_all_calls();

            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME));

            // act
            status = hsm_client_tpm_init();

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_interface
        */
        TEST_FUNCTION(hsm_client_tpm_interface_success)
        {
            //arrange

            // act
            const HSM_CLIENT_TPM_INTERFACE* result = hsm_client_tpm_interface();

            // assert
            ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_tpm_create, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_tpm_destroy, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_activate_identity_key, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_get_ek, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_get_srk, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_sign_with_identity, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(result->hsm_client_derive_and_sign_with_identity, "Line:" TOSTRING(__LINE__));

            //cleanup
        }

        /**
         * Test function for API
         *   hsm_client_tpm_create
        */
        TEST_FUNCTION(edge_hsm_client_tpm_create_fails_when_tpm_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            umock_c_reset_all_calls();

            // act
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();

            // assert
            ASSERT_IS_NULL_WITH_MSG(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_tpm_create
        */
        TEST_FUNCTION(edge_hsm_client_tpm_create_success)
        {
            //arrange
            int status;
            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(gballoc_calloc(1, IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open(TEST_EDGE_STORE_NAME));

            // act
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();

            // assert
            ASSERT_IS_NOT_NULL_WITH_MSG(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_create
        */
        TEST_FUNCTION(edge_hsm_client_tpm_create_negative)
        {
            //arrange
            int status;
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(gballoc_calloc(1, IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open(TEST_EDGE_STORE_NAME));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();

                // assert
                ASSERT_IS_NULL_WITH_MSG(hsm_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_tpm_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_tpm_destroy
        */
        TEST_FUNCTION(edge_hsm_client_tpm_destroy_does_nothing_with_invalid_handle)
        {
            //arrange
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;

            // act
            hsm_client_tpm_destroy(NULL);

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_tpm_destroy
        */
        TEST_FUNCTION(edge_hsm_client_tpm_destroy_does_nothing_when_tpm_not_initialized)
        {
            //arrange
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;

            // act
            hsm_client_tpm_destroy(TEST_HSM_CLIENT_HANDLE);

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_tpm_destroy
        */
        TEST_FUNCTION(edge_hsm_client_tpm_destroy_success)
        {
            //arrange
            int status;
            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close(TEST_HSM_STORE_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_free(hsm_handle));

            // act
            hsm_client_tpm_destroy(hsm_handle);

            // assert
            ASSERT_IS_NOT_NULL_WITH_MSG(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_activate_identity_key
        */
        TEST_FUNCTION(edge_hsm_client_activate_identity_key_invalid_param_validation)
        {
            //arrange
            int status;
            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_ACTIVATE_IDENTITY_KEY hsm_client_activate_identity_key = interface->hsm_client_activate_identity_key;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};

            // act, assert
            status = hsm_client_activate_identity_key(NULL, test_input, sizeof(test_input));
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = hsm_client_activate_identity_key(hsm_handle, NULL, sizeof(test_input));
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = hsm_client_activate_identity_key(hsm_handle, test_input, 0);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_activate_identity_key
        */
        TEST_FUNCTION(edge_hsm_client_activate_identity_key_success)
        {
            //arrange
            int status;
            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_ACTIVATE_IDENTITY_KEY hsm_client_activate_identity_key = interface->hsm_client_activate_identity_key;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_insert_sas_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME, test_input, sizeof(test_input)));

            // act
            status = hsm_client_activate_identity_key(hsm_handle, test_input, sizeof(test_input));

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_activate_identity_key
        */
        TEST_FUNCTION(edge_hsm_client_activate_identity_key_negative)
        {
            //arrange
            int status;
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_ACTIVATE_IDENTITY_KEY hsm_client_activate_identity_key = interface->hsm_client_activate_identity_key;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_insert_sas_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME, test_input, sizeof(test_input)));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_activate_identity_key(hsm_handle, test_input, sizeof(test_input));

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_ek
        */
        TEST_FUNCTION(edge_hsm_client_get_ek_does_nothing_when_tpm_not_initialized)
        {
            //arrange
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_GET_ENDORSEMENT_KEY hsm_client_get_ek = interface->hsm_client_get_ek;
            int status;
            unsigned char *test_output_buffer = (unsigned char*)0x5000;
            size_t test_output_len = 10;
            umock_c_reset_all_calls();

            // act
            status = hsm_client_get_ek(TEST_HSM_CLIENT_HANDLE, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_get_ek
        */
        TEST_FUNCTION(edge_hsm_client_get_ek_success)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_GET_ENDORSEMENT_KEY hsm_client_get_ek = interface->hsm_client_get_ek;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char *test_output_buffer = (unsigned char*)0x5000;
            size_t test_output_len = 10;
            umock_c_reset_all_calls();

            // act
            status = hsm_client_get_ek(hsm_handle, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_ek
        */
        TEST_FUNCTION(edge_hsm_client_get_ek_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_GET_ENDORSEMENT_KEY hsm_client_get_ek = interface->hsm_client_get_ek;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char *test_output_buffer = (unsigned char*)0x5000;
            size_t test_output_len = 10;

            // act, assert
            status = hsm_client_get_ek(NULL, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = (unsigned char*)0x5000;
            test_output_len = 10;
            status = hsm_client_get_ek(hsm_handle, NULL, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = (unsigned char*)0x5000;
            test_output_len = 10;
            status = hsm_client_get_ek(hsm_handle, &test_output_buffer, NULL);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_ek
        */
        TEST_FUNCTION(edge_hsm_client_get_srk_does_nothing_when_tpm_not_initialized)
        {
            //arrange
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_GET_STORAGE_ROOT_KEY hsm_client_get_srk = interface->hsm_client_get_srk;
            unsigned char *test_output_buffer = (unsigned char*)0x5000;
            size_t test_output_len = 10;
            int status;
            umock_c_reset_all_calls();

            // act
            status = hsm_client_get_srk(TEST_HSM_CLIENT_HANDLE, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_get_srk
        */
        TEST_FUNCTION(edge_hsm_client_get_srk_success)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_GET_STORAGE_ROOT_KEY hsm_client_get_srk = interface->hsm_client_get_srk;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char *test_output_buffer = (unsigned char*)0x5000;
            size_t test_output_len = 10;
            umock_c_reset_all_calls();

            // act
            status = hsm_client_get_srk(hsm_handle, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_srk
        */
        TEST_FUNCTION(edge_hsm_client_get_srk_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_GET_STORAGE_ROOT_KEY hsm_client_get_srk = interface->hsm_client_get_srk;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char *test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            size_t test_output_len = 10;

            // act, assert
            status = hsm_client_get_srk(NULL, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_get_srk(hsm_handle, NULL, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_get_srk(hsm_handle, &test_output_buffer, NULL);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_sign_with_identity_does_nothing_when_tpm_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity = interface->hsm_client_sign_with_identity;
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            size_t test_output_len = 10;
            umock_c_reset_all_calls();

            // act
            status = hsm_client_sign_with_identity(TEST_HSM_CLIENT_HANDLE, test_input, sizeof(test_input), &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_sign_with_identity_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity = interface->hsm_client_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            size_t test_output_len = 10;

            // act, assert
            status = hsm_client_sign_with_identity(NULL, test_input, sizeof(test_input), &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_sign_with_identity(hsm_handle, NULL, sizeof(test_input), &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_sign_with_identity(hsm_handle, test_input, 0, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_sign_with_identity(hsm_handle, test_input, sizeof(test_input), NULL, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_sign_with_identity(hsm_handle, test_input, sizeof(test_input), &test_output_buffer, NULL);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_sign_with_identity_success)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity = interface->hsm_client_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = NULL;
            size_t test_output_len = 0;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_sign(TEST_KEY_HANDLE, test_input, sizeof(test_input), &test_output_buffer, &test_output_len));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            // act
            status = hsm_client_sign_with_identity(hsm_handle, test_input, sizeof(test_input), &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_sign_with_identity_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_SIGN_WITH_IDENTITY hsm_client_sign_with_identity = interface->hsm_client_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = NULL;
            size_t test_output_len = 0;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_sign(TEST_KEY_HANDLE, test_input, sizeof(test_input), &test_output_buffer, &test_output_len));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_sign_with_identity(hsm_handle, test_input, sizeof(test_input), &test_output_buffer, &test_output_len);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_derive_and_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_derive_and_sign_with_identity_does_nothing_when_tpm_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY hsm_client_derive_and_sign_with_identity = interface->hsm_client_derive_and_sign_with_identity;
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = NULL;
            size_t test_output_len = 0;
            size_t identity_size = sizeof(TEST_EDGE_MODULE_IDENTITY);
            umock_c_reset_all_calls();

            // act
            status = hsm_client_derive_and_sign_with_identity(TEST_HSM_CLIENT_HANDLE, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_derive_and_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_derive_and_sign_with_identity_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY hsm_client_derive_and_sign_with_identity = interface->hsm_client_derive_and_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            size_t test_output_len = 10;
            size_t identity_size = sizeof(TEST_EDGE_MODULE_IDENTITY);

            // act, assert
            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(NULL, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, NULL, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, 0, TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), NULL, identity_size, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, 0, &test_output_buffer, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, NULL, &test_output_len);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_TRUE_WITH_MSG((test_output_len == 0), "Line:" TOSTRING(__LINE__));

            test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            test_output_len = 10;
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, NULL);
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL_WITH_MSG(test_output_buffer, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_derive_and_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_derive_and_sign_with_identity_success)
        {
            //arrange
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY hsm_client_derive_and_sign_with_identity = interface->hsm_client_derive_and_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = TEST_OUTPUT_DIGEST_PTR;
            size_t test_output_len = 0;
            size_t identity_size = sizeof(TEST_EDGE_MODULE_IDENTITY);

            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_derive_and_sign(TEST_KEY_HANDLE, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            // act
            status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);

            // assert
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_derive_and_sign_with_identity
        */
        TEST_FUNCTION(edge_hsm_client_derive_and_sign_with_identity_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status = hsm_client_tpm_init();
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
            HSM_CLIENT_CREATE hsm_client_tpm_create = interface->hsm_client_tpm_create;
            HSM_CLIENT_DESTROY hsm_client_tpm_destroy = interface->hsm_client_tpm_destroy;
            HSM_CLIENT_DERIVE_AND_SIGN_WITH_IDENTITY hsm_client_derive_and_sign_with_identity = interface->hsm_client_derive_and_sign_with_identity;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_tpm_create();
            unsigned char test_input[] = {'t', 'e', 's', 't'};
            unsigned char *test_output_buffer = NULL;
            size_t test_output_len = 0;
            size_t identity_size = sizeof(TEST_EDGE_MODULE_IDENTITY);

            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, TEST_SAS_KEY_NAME));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_derive_and_sign(TEST_KEY_HANDLE, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_derive_and_sign_with_identity(hsm_handle, test_input, sizeof(test_input), TEST_EDGE_MODULE_IDENTITY, identity_size, &test_output_buffer, &test_output_len);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_tpm_destroy(hsm_handle);
            hsm_client_tpm_deinit();
            umock_c_negative_tests_deinit();
        }

END_TEST_SUITE(edge_hsm_tpm_unittests)
