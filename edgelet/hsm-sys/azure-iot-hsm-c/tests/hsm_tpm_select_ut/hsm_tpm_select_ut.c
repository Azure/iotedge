// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

#include "testrunnerswitcher.h"
#include "umock_c/umock_c.h"
#include "umock_c/umock_c_negative_tests.h"
#include "umock_c/umocktypes_charptr.h"
#include "azure_macro_utils/macro_utils.h"

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"

#include "hsm_client_tpm_device.h"
#include "hsm_client_tpm_in_mem.h"

MOCKABLE_FUNCTION(, int, hsm_client_tpm_device_init);
MOCKABLE_FUNCTION(, void, hsm_client_tpm_device_deinit);
MOCKABLE_FUNCTION(, const HSM_CLIENT_TPM_INTERFACE*, hsm_client_tpm_device_interface);

MOCKABLE_FUNCTION(, int, hsm_client_tpm_store_init);
MOCKABLE_FUNCTION(, void, hsm_client_tpm_store_deinit);
MOCKABLE_FUNCTION(, const HSM_CLIENT_TPM_INTERFACE*, hsm_client_tpm_store_interface);

#undef ENABLE_MOCKS

//#############################################################################
// Interface under test
//#############################################################################
#include "hsm_client_data.h"

//#############################################################################
// Test defines and data
//#############################################################################
static TEST_MUTEX_HANDLE g_testByTest;

MU_DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

#define TEST_HSM_CLIENT_TPM_INTERFACE (HSM_CLIENT_TPM_INTERFACE*)0x1000

//#############################################################################
// Test helpers
//#############################################################################

static void setup_callstack_hsm_client_tpm_init(void)
{
    umock_c_reset_all_calls();

    #ifdef TEST_TPM_INTERFACE_IN_MEM
        EXPECTED_CALL(hsm_client_tpm_store_init());
    #else
        EXPECTED_CALL(hsm_client_tpm_device_init());
    #endif
}

static void setup_callstack_hsm_client_tpm_deinit(void)
{
    umock_c_reset_all_calls();

    #ifdef TEST_TPM_INTERFACE_IN_MEM
        EXPECTED_CALL(hsm_client_tpm_store_deinit());
    #else
        EXPECTED_CALL(hsm_client_tpm_device_deinit());
    #endif
}

static void setup_callstack_hsm_client_tpm_interface(void)
{
    umock_c_reset_all_calls();

    #ifdef TEST_TPM_INTERFACE_IN_MEM
        EXPECTED_CALL(hsm_client_tpm_store_interface());
    #else
        EXPECTED_CALL(hsm_client_tpm_device_interface());
    #endif
}

//#############################################################################
// Mocked functions test hooks
//#############################################################################

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s",
                   MU_ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

static int test_hook_hsm_client_tpm_init(void)
{
    return 0;
}

static void test_hook_hsm_client_tpm_deinit(void)
{

}

const HSM_CLIENT_TPM_INTERFACE* test_hook_hsm_client_tpm_interface(void)
{
    return TEST_HSM_CLIENT_TPM_INTERFACE;
}

//#############################################################################
// Test functions
//#############################################################################

BEGIN_TEST_SUITE(hsm_tpm_select_ut)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);

        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_TPM_INTERFACE*, void*);

        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_device_init, test_hook_hsm_client_tpm_init);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_tpm_device_init, 1);

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_device_deinit, test_hook_hsm_client_tpm_deinit);

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_device_interface, test_hook_hsm_client_tpm_interface);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_tpm_device_interface, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_store_init, test_hook_hsm_client_tpm_init);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_tpm_store_init, 1);

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_store_deinit, test_hook_hsm_client_tpm_deinit);

        REGISTER_GLOBAL_MOCK_HOOK(hsm_client_tpm_store_interface, test_hook_hsm_client_tpm_interface);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(hsm_client_tpm_store_interface, NULL);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        umock_c_deinit();
        TEST_MUTEX_DESTROY(g_testByTest);
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

    TEST_FUNCTION(hsm_client_tpm_init_success)
    {
        // arrange
        setup_callstack_hsm_client_tpm_init();

        // act
        int result = hsm_client_tpm_init();

        // assert
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        // cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_init_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        setup_callstack_hsm_client_tpm_init();

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            int status;
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            // act
            status = hsm_client_tpm_init();

            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
        }

        // cleanup
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_deinit_success)
    {
        // arrange
        int result = hsm_client_tpm_init();
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
        setup_callstack_hsm_client_tpm_deinit();

        // act
        hsm_client_tpm_deinit();

        // assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        // cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_interface_success)
    {
        //arrange
        setup_callstack_hsm_client_tpm_interface();

        //act
        const HSM_CLIENT_TPM_INTERFACE* result = hsm_client_tpm_interface();

        //assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_interface_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        setup_callstack_hsm_client_tpm_interface();

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            // act
            const HSM_CLIENT_TPM_INTERFACE* result = hsm_client_tpm_interface();

            // assert
            ASSERT_IS_NULL(result, "Line:" MU_TOSTRING(__LINE__));
        }

        // cleanup
        umock_c_negative_tests_deinit();
    }

END_TEST_SUITE(hsm_tpm_select_ut)
