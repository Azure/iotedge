// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

//#############################################################################
// Memory allocator test hooks
//#############################################################################

static void* test_hook_gballoc_malloc(size_t size)
{
    void * result = malloc(size);
    printf("Malloc Returned %p\r\n", result);
    return result;
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
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/singlylinkedlist.h"
#include "certificate_info.h"
#include "hsm_certificate_props.h"
#include "hsm_key.h"
#include "hsm_utils.h"

MOCKABLE_FUNCTION(, CERT_INFO_HANDLE, certificate_info_create, const char*, certificate, const void*, private_key, size_t, priv_key_len, PRIVATE_KEY_TYPE, pk_type);
MOCKABLE_FUNCTION(, const char*, get_alias, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_issuer_alias, CERT_PROPS_HANDLE, handle);
//MOCKABLE_FUNCTION(, mocked_list_condition_function, const void*, item, const void*, match_context, bool*, continue_processing);
#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################

#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_constants.h"

//#############################################################################
// Test defines and data
//#############################################################################
#define TEST_STORE_NAME "test_store"

#define DEFAULT_LIST_HANDLE (SINGLYLINKEDLIST_HANDLE)0x1000
#define DEFAULT_LIST_ITEM_HANDLE (LIST_ITEM_HANDLE)0x1001
#define SAS_KEYS_LIST_HANDLE (SINGLYLINKEDLIST_HANDLE)0x1002
#define ENC_KEYS_LIST_HANDLE (SINGLYLINKEDLIST_HANDLE)0x1003
#define CERTS_LIST_HANDLE (SINGLYLINKEDLIST_HANDLE)0x1004
#define TRUSTED_CERTS_LIST_HANDLE (SINGLYLINKEDLIST_HANDLE)0x1005

#define DEFAULT_STRING_NEW_HANDLE (STRING_HANDLE)0x2000
#define DEFAULT_STRING_CONSTRUCT_HANDLE (STRING_HANDLE)0x2001
#define STORE_ID_STRING_HANDLE (STRING_HANDLE)0x2002

#define DEFAULT_BUFFER_NEW_HANDLE (BUFFER_HANDLE)0x3000

#define TEST_STORE_HANDLE (HSM_CLIENT_STORE_HANDLE)0x4000

#define TEST_SAS_KEY_NAME_1  "test_sas_name_1"
#define TEST_SAS_KEY_VALUE_1 "ABCD"
#define SAS_KEY_ID_1_BUFFER_HANDLE (BUFFER_HANDLE)0x6000
#define SAS_KEY_ID_1_STRING_HANDLE (STRING_HANDLE)0x6001
#define SAS_KEY_ID_1_LIST_ITEM_HANDLE (LIST_ITEM_HANDLE) 0x6002

#define TEST_SAS_KEY_NAME_2  "test_sas_name_2"
#define TEST_SAS_KEY_VALUE_2 "1234"
#define SAS_KEY_ID_2_BUFFER_HANDLE (BUFFER_HANDLE)0x6010
#define SAS_KEY_ID_2_STRING_HANDLE (STRING_HANDLE)0x6011
#define SAS_KEY_ID_2_LIST_ITEM_HANDLE (LIST_ITEM_HANDLE) 0x6012

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

static LIST_ITEM_HANDLE test_hook_singlylinkedlist_add(SINGLYLINKEDLIST_HANDLE list, const void* item)
{
    return DEFAULT_LIST_ITEM_HANDLE;
}

static SINGLYLINKEDLIST_HANDLE test_hook_singlylinkedlist_create()
{
    return DEFAULT_LIST_HANDLE;
}

static STRING_HANDLE test_hook_STRING_new()
{
    return DEFAULT_STRING_NEW_HANDLE;
}

static STRING_HANDLE test_hook_STRING_construct(const char *input)
{
    (void)input;
    return DEFAULT_STRING_CONSTRUCT_HANDLE;
}

static BUFFER_HANDLE test_hook_BUFFER_new()
{
    return DEFAULT_BUFFER_NEW_HANDLE;
}

static BUFFER_HANDLE test_hook_BUFFER_create(const unsigned char* data, size_t data_size)
{
    return DEFAULT_BUFFER_NEW_HANDLE;
}

//#############################################################################
// Test cases callstack helpers
//#############################################################################

void call_stack_helper_store_open(void)
{
    EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    EXPECTED_CALL(singlylinkedlist_create()).SetReturn(SAS_KEYS_LIST_HANDLE);
    EXPECTED_CALL(singlylinkedlist_create()).SetReturn(ENC_KEYS_LIST_HANDLE);
    EXPECTED_CALL(singlylinkedlist_create()).SetReturn(CERTS_LIST_HANDLE);
    EXPECTED_CALL(singlylinkedlist_create()).SetReturn(TRUSTED_CERTS_LIST_HANDLE);
    STRICT_EXPECTED_CALL(STRING_construct(TEST_STORE_NAME)).SetReturn(STORE_ID_STRING_HANDLE);
}

void call_stack_helper_key_list_destroy(SINGLYLINKEDLIST_HANDLE list, int mocked_num_items)
{
    int index;
    for (index = 0; index < mocked_num_items; index++)
    {
        LIST_ITEM_HANDLE list_item = (LIST_ITEM_HANDLE)(uintptr_t)(0x1000 + index);
        void* real_mem = test_hook_gballoc_malloc(10);
        STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(list_item);
        STRICT_EXPECTED_CALL(singlylinkedlist_item_get_value(list_item)).SetReturn(real_mem);
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        EXPECTED_CALL(BUFFER_delete(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(gballoc_free(real_mem));
        STRICT_EXPECTED_CALL(singlylinkedlist_remove(list, list_item));
    }
    STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(NULL);
    STRICT_EXPECTED_CALL(singlylinkedlist_destroy(list));
}

void call_stack_helper_trusted_cert_list_destroy(SINGLYLINKEDLIST_HANDLE list, int mocked_num_items)
{
    int index;
    for (index = 0; index < mocked_num_items; index++)
    {
        LIST_ITEM_HANDLE list_item = (LIST_ITEM_HANDLE)(uintptr_t)(0x1000 + index);
        void* real_mem = test_hook_gballoc_malloc(10);
        ASSERT_IS_NOT_NULL(real_mem);
        STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(list_item);
        STRICT_EXPECTED_CALL(singlylinkedlist_item_get_value(list_item)).SetReturn(real_mem);
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(gballoc_free(real_mem));
        STRICT_EXPECTED_CALL(singlylinkedlist_remove(list, list_item));
    }
    STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(NULL);
    STRICT_EXPECTED_CALL(singlylinkedlist_destroy(list));
}

void call_stack_helper_cert_list_destroy(SINGLYLINKEDLIST_HANDLE list, int mocked_num_items)
{
    int index;
    for (index = 0; index < mocked_num_items; index++)
    {
        LIST_ITEM_HANDLE list_item = (LIST_ITEM_HANDLE)(uintptr_t)(0x1000 + index);
        void* real_mem = test_hook_gballoc_malloc(10);
        ASSERT_IS_NOT_NULL(real_mem);
        STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(list_item);
        STRICT_EXPECTED_CALL(singlylinkedlist_item_get_value(list_item)).SetReturn(real_mem);
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        EXPECTED_CALL(STRING_delete(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(gballoc_free(real_mem));
        STRICT_EXPECTED_CALL(singlylinkedlist_remove(list, list_item));
    }
    STRICT_EXPECTED_CALL(singlylinkedlist_get_head_item(list)).SetReturn(NULL);
    STRICT_EXPECTED_CALL(singlylinkedlist_destroy(list));
}

void call_stack_helper_store_close(int mocked_num_items)
{
    STRICT_EXPECTED_CALL(STRING_delete(STORE_ID_STRING_HANDLE));
    call_stack_helper_trusted_cert_list_destroy(TRUSTED_CERTS_LIST_HANDLE, mocked_num_items);
    call_stack_helper_cert_list_destroy(CERTS_LIST_HANDLE, mocked_num_items);
    call_stack_helper_key_list_destroy(ENC_KEYS_LIST_HANDLE, mocked_num_items);
    call_stack_helper_key_list_destroy(SAS_KEYS_LIST_HANDLE, mocked_num_items);
    STRICT_EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
    STRICT_EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_store_unittests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);

        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_STORE_INTERFACE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_STORE_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_KEY_INTERFACE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(KEY_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(CERT_PROPS_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(SINGLYLINKEDLIST_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(LIST_ITEM_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(STRING_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(BUFFER_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(LIST_CONDITION_FUNCTION, void*);

        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, test_hook_gballoc_calloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, test_hook_gballoc_realloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);

        //REGISTER_GLOBAL_MOCK_HOOK(singlylinkedlist_create, test_hook_singlylinkedlist_create);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(singlylinkedlist_create, NULL);
        //REGISTER_GLOBAL_MOCK_HOOK(singlylinkedlist_add, test_hook_singlylinkedlist_add);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(singlylinkedlist_add, NULL);

        //REGISTER_GLOBAL_MOCK_HOOK(STRING_new, test_hook_STRING_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(STRING_new, NULL);

        //REGISTER_GLOBAL_MOCK_HOOK(STRING_construct, test_hook_STRING_construct);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(STRING_construct, NULL);

        //REGISTER_GLOBAL_MOCK_HOOK(BUFFER_new, test_hook_BUFFER_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_new, NULL);

        //REGISTER_GLOBAL_MOCK_HOOK(BUFFER_create, test_hook_BUFFER_create);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_create, NULL);
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

    TEST_FUNCTION(test_edge_hsm_client_store_create_invalid_params)
    {
        // arrange
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();

        // act, assert
        result = store_if->hsm_client_store_create(NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_create("");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_edge_hsm_client_store_destroy_invalid_params)
    {
        // arrange
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();

        // act, assert
        result = store_if->hsm_client_store_destroy(NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        result = store_if->hsm_client_store_destroy("");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_hsm_client_store_open_invalid_params)
    {
        // arrange
        HSM_CLIENT_STORE_HANDLE result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();

        // act, assert
        result = store_if->hsm_client_store_open(NULL);
        ASSERT_IS_NULL(result);

        result = store_if->hsm_client_store_open("");
        ASSERT_IS_NULL(result);

        // cleanup
    }

    TEST_FUNCTION(test_hsm_client_store_open_success)
    {
        // arrange
        HSM_CLIENT_STORE_HANDLE result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        umock_c_reset_all_calls();

        call_stack_helper_store_open();

        // act
        result = store_if->hsm_client_store_open(TEST_STORE_NAME);

        // assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        (void)store_if->hsm_client_store_close(result);
    }

    TEST_FUNCTION(test_hsm_client_store_multiple_open_success)
    {
        // arrange
        HSM_CLIENT_STORE_HANDLE handle_1, handle_2;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle_1 = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle_1);
        umock_c_reset_all_calls();

        // act
        handle_2 = store_if->hsm_client_store_open(TEST_STORE_NAME);

        // assert
        ASSERT_IS_NOT_NULL(handle_2);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        (void)store_if->hsm_client_store_close(handle_2);
        (void)store_if->hsm_client_store_close(handle_1);
    }

    TEST_FUNCTION(test_hsm_client_store_open_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);
        HSM_CLIENT_STORE_HANDLE result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        const char *store_name = "test_store";
        umock_c_reset_all_calls();

        call_stack_helper_store_open();

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            // act
            result = store_if->hsm_client_store_open(store_name);

            // assert
            ASSERT_IS_NULL(result);
        }

        // cleanup
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(test_hsm_client_store_close_invalid_params)
    {
        // arrange
        int result;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();

        // act, assert
        result = store_if->hsm_client_store_close(NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));


        result = store_if->hsm_client_store_close(TEST_STORE_HANDLE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_hsm_client_store_close_success)
    {
        // arrange
        int result;
        HSM_CLIENT_STORE_HANDLE handle;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle);
        umock_c_reset_all_calls();

        call_stack_helper_store_close(0);

        // act
        result = store_if->hsm_client_store_close(handle);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_hsm_client_store_close_with_mutiple_keys_certs_inserted_success)
    {
        // arrange
        int result;
        HSM_CLIENT_STORE_HANDLE handle;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle);
        umock_c_reset_all_calls();

        call_stack_helper_store_close(2);

        // act
        result = store_if->hsm_client_store_close(handle);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_hsm_client_store_multiple_open_does_not_close)
    {
        // arrange
        HSM_CLIENT_STORE_HANDLE handle_1, handle_2;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle_1 = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle_1);
        handle_2 = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle_2);
        umock_c_reset_all_calls();

        // act
        int result = store_if->hsm_client_store_close(handle_2);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        (void)store_if->hsm_client_store_close(handle_1);
    }

    TEST_FUNCTION(test_hsm_client_store_multiple_opens_last_close_deletes)
    {
        // arrange
        HSM_CLIENT_STORE_HANDLE handle_1, handle_2;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle_1 = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle_1);
        handle_2 = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle_2);
        int result = store_if->hsm_client_store_close(handle_2);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        umock_c_reset_all_calls();

        call_stack_helper_store_close(0);

        // act
        result = store_if->hsm_client_store_close(handle_1);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    TEST_FUNCTION(test_edge_hsm_client_store_insert_sas_key_invalid_params)
    {
        // arrange
        int result;
        HSM_CLIENT_STORE_HANDLE handle;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle);
        umock_c_reset_all_calls();

        // act, assert
        store_if->hsm_client_store_insert_sas_key(NULL, TEST_SAS_KEY_NAME_1, TEST_SAS_KEY_VALUE_1, strlen(TEST_SAS_KEY_VALUE_1) + 1);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // act, assert
        store_if->hsm_client_store_insert_sas_key(handle, NULL, TEST_SAS_KEY_VALUE_1, strlen(TEST_SAS_KEY_VALUE_1) + 1);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // act, assert
        store_if->hsm_client_store_insert_sas_key(handle, "", TEST_SAS_KEY_VALUE_1, strlen(TEST_SAS_KEY_VALUE_1) + 1);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // act, assert
        store_if->hsm_client_store_insert_sas_key(handle, TEST_SAS_KEY_NAME_1, NULL, strlen(TEST_SAS_KEY_VALUE_1) + 1);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // act, assert
        store_if->hsm_client_store_insert_sas_key(handle, TEST_SAS_KEY_NAME_1, TEST_SAS_KEY_VALUE_1, 0);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
        (void)store_if->hsm_client_store_close(handle);
    }

    TEST_FUNCTION(test_edge_hsm_client_store_insert_sas_key_success)
    {
        // arrange
        int result;
        HSM_CLIENT_STORE_HANDLE handle;
        const HSM_CLIENT_STORE_INTERFACE *store_if = hsm_client_store_interface();
        call_stack_helper_store_open();
        handle = store_if->hsm_client_store_open(TEST_STORE_NAME);
        ASSERT_IS_NOT_NULL(handle);
        umock_c_reset_all_calls();

        void *key_entry_1 = (void*)0x10000;
        STRICT_EXPECTED_CALL(singlylinkedlist_remove_if(SAS_KEYS_LIST_HANDLE, IGNORED_PTR_ARG, TEST_SAS_KEY_NAME_1))
            .SetReturn(0);
        EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG))
            .CaptureReturn(&key_entry_1);
        STRICT_EXPECTED_CALL(STRING_construct(TEST_SAS_KEY_NAME_1))
            .SetReturn(SAS_KEY_ID_1_STRING_HANDLE);
        STRICT_EXPECTED_CALL(BUFFER_create(TEST_SAS_KEY_VALUE_1, strlen(TEST_SAS_KEY_VALUE_1) + 1))
            .SetReturn(SAS_KEY_ID_1_BUFFER_HANDLE);
        STRICT_EXPECTED_CALL(singlylinkedlist_add(SAS_KEYS_LIST_HANDLE, IGNORED_PTR_ARG))
            .SetReturn(SAS_KEY_ID_1_LIST_ITEM_HANDLE);

        printf("Got captured value %p \r\n", key_entry_1);
        // act
        result = store_if->hsm_client_store_insert_sas_key(handle, TEST_SAS_KEY_NAME_1, TEST_SAS_KEY_VALUE_1, strlen(TEST_SAS_KEY_VALUE_1) + 1);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        (void)store_if->hsm_client_store_close(handle);
    }

    #if 0
    TEST_FUNCTION(hsm_client_sample_negative)
    {
        //arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            // act

            // assert
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }
    #endif

END_TEST_SUITE(edge_hsm_store_unittests)
