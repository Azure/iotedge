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

#include <openssl/evp.h>
#include <openssl/err.h>
#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umock_c_negative_tests.h"
#include "umocktypes_charptr.h"

//#############################################################################
// Declare and enable MOCK definitions
//#############################################################################

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"

// store mocks
MOCKABLE_FUNCTION(, void, mocked_OpenSSL_add_all_algorithms);
MOCKABLE_FUNCTION(, void, ERR_load_BIO_strings);
MOCKABLE_FUNCTION(, void, ERR_load_crypto_strings);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "edge_openssl_common.h"

//#############################################################################
// Test defines and data
//#############################################################################

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)
static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

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

static void test_hook_mocked_OpenSSL_add_all_algorithms(void)
{

}

static void test_hook_ERR_load_BIO_strings(void)
{

}

static void test_hook_ERR_load_crypto_strings(void)
{

}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_common_ut)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);
        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

        REGISTER_GLOBAL_MOCK_HOOK(mocked_OpenSSL_add_all_algorithms, test_hook_mocked_OpenSSL_add_all_algorithms);
        REGISTER_GLOBAL_MOCK_HOOK(ERR_load_BIO_strings, test_hook_ERR_load_BIO_strings);
        REGISTER_GLOBAL_MOCK_HOOK(ERR_load_crypto_strings, test_hook_ERR_load_crypto_strings);
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
     *   initialize_openssl
    */
    TEST_FUNCTION(initialize_openssl_initializes_just_once_success)
    {
        // arrange
        STRICT_EXPECTED_CALL(mocked_OpenSSL_add_all_algorithms());
        STRICT_EXPECTED_CALL(ERR_load_BIO_strings());
        STRICT_EXPECTED_CALL(ERR_load_crypto_strings());

        // act 1
        initialize_openssl();

        // assert 1
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        umock_c_reset_all_calls();

        // act 2 test that no functions are called
        initialize_openssl();

        // assert 2
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

END_TEST_SUITE(edge_openssl_common_ut)
