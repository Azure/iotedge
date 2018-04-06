// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdlib.h>
#include <string.h>
#include <stddef.h>

static void* my_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void* my_gballoc_calloc(size_t num, size_t size)
{
    return calloc(num, size);
}

static void* my_gballoc_realloc(void* ptr, size_t size)
{
    return realloc(ptr, size);
}

static void my_gballoc_free(void* ptr)
{
    free(ptr);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umock_c_negative_tests.h"
#include "umocktypes_charptr.h"

#define ENABLE_MOCKS

#include "azure_c_shared_utility/gballoc.h"

#undef ENABLE_MOCKS

#include "testrunnerswitcher.h"
#include "hsm_client_data.h"


#define TEST_STRING_64 "0123456789012345678901234567890123456789012345678901234567890123"
#define TEST_STRING_65  TEST_STRING_64 "1"

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static void on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

BEGIN_TEST_SUITE(cert_props_unittests)

        TEST_SUITE_INITIALIZE(TestClassInitialize)
        {
            TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
            g_testByTest = TEST_MUTEX_CREATE();
            ASSERT_IS_NOT_NULL(g_testByTest);

            umock_c_init(on_umock_c_error);

            REGISTER_UMOCK_ALIAS_TYPE(CERT_PROPS_HANDLE, void*);
            ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, my_gballoc_calloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, my_gballoc_realloc);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);
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
                ASSERT_FAIL("our mutex is ABANDONED. Failure in test framework");
            }

            umock_c_reset_all_calls();
        }

        TEST_FUNCTION_CLEANUP(TestMethodCleanup)
        {
            TEST_MUTEX_RELEASE(g_testByTest);
        }

        TEST_FUNCTION(create_certificate_props_success)
        {
            //arrange
            CERT_PROPS_HANDLE cert_handle;

            STRICT_EXPECTED_CALL(gballoc_calloc(1, IGNORED_NUM_ARG));

            //act
            cert_handle = create_certificate_props();

            //assert
            ASSERT_IS_NOT_NULL(cert_handle);
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

            //cleanup
            destroy_certificate_props(cert_handle);
        }

        /**
         * Test function for APIs
         *   set_validity_in_mins
         *   get_validity_in_mins
        */
        TEST_FUNCTION(create_certificate_props_validity)
        {
            //arrange
            int status;
            size_t validity = 0;
            CERT_PROPS_HANDLE props_handle = create_certificate_props();

            // invalid handle
            status = set_validity_in_mins(NULL, 10);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_validity_in_mins(NULL, &validity);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // invalid input data
            status = set_validity_in_mins(props_handle, 0);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_validity_in_mins(props_handle, NULL);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // valid input data
            status = set_validity_in_mins(props_handle, 10);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_validity_in_mins(props_handle, &validity);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(size_t, 10, validity, "Line:" TOSTRING(__LINE__));

            //cleanup
            destroy_certificate_props(props_handle);
        }

        /**
         * Test function for APIs
         *   set_common_name
         *   get_common_name
        */
        TEST_FUNCTION(certificate_props_common_name)
        {
            //arrange
            int status;
            // common name max length is 64 + 1 for null term
            char test_input_string[65] = TEST_STRING_64;
            char test_output_string[65];

            CERT_PROPS_HANDLE props_handle = create_certificate_props();

            // invalid handle
            status = set_common_name(NULL, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_common_name(NULL, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // invalid paramters and data
            status = set_common_name(props_handle, NULL);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_common_name(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_common_name(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_common_name(props_handle, NULL, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // valid input data
            status = set_common_name(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_common_name(props_handle, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

            // invalid input for get_common_name
            status = set_common_name(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_common_name(props_handle, test_output_string, 0);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_common_name(props_handle, test_output_string, 30);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            destroy_certificate_props(props_handle);
        }

        /**
         * Test function for APIs
         *   set_issuer_alias
         *   get_issuer_alias
        */
        TEST_FUNCTION(certificate_props_issuer_alias)
        {
            //arrange
            int status;
            // alias name max length is 64 + 1 for null term
            char test_input_string[65] = TEST_STRING_64;
            char test_output_string[65];

            CERT_PROPS_HANDLE props_handle = create_certificate_props();

            // invalid handle
            status = set_issuer_alias(NULL, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_issuer_alias(NULL, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // invalid paramters and data
            status = set_issuer_alias(props_handle, NULL);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_issuer_alias(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_issuer_alias(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_issuer_alias(props_handle, NULL, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // valid input data
            status = set_issuer_alias(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_issuer_alias(props_handle, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

            // invalid input for get_issuer_alias
            status = set_issuer_alias(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_issuer_alias(props_handle, test_output_string, 0);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_issuer_alias(props_handle, test_output_string, 30);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            destroy_certificate_props(props_handle);
        }

        /**
         * Test function for APIs
         *   set_alias
         *   get_alias
        */
        TEST_FUNCTION(certificate_props_alias)
        {
            //arrange
            int status;
            // alias name max length is 64 + 1 for null term
            char test_input_string[65] = TEST_STRING_64;
            char test_output_string[65];

            CERT_PROPS_HANDLE props_handle = create_certificate_props();

            // invalid handle
            status = set_alias(NULL, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_alias(NULL, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // invalid paramters and data
            status = set_alias(props_handle, NULL);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_alias(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_alias(props_handle, TEST_STRING_65);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_alias(props_handle, NULL, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // valid input data
            status = set_alias(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_alias(props_handle, test_output_string, sizeof(test_output_string));
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

            // invalid input for get_alias
            status = set_alias(props_handle, test_input_string);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_alias(props_handle, test_output_string, 0);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_alias(props_handle, test_output_string, 30);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));

            // cleanup
            destroy_certificate_props(props_handle);
        }

        /**
         * Test function for APIs
         *   set_certificate_type
         *   get_certificate_type
        */
        TEST_FUNCTION(certificate_props_certificate_type)
        {
            //arrange
            int status;
            CERTIFICATE_TYPE test_output;
            CERT_PROPS_HANDLE props_handle = create_certificate_props();

            // test default value
            status = get_certificate_type(props_handle, &test_output);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(int, CERTIFICATE_TYPE_UNKNOWN, test_output, "Line:" TOSTRING(__LINE__));

            // invalid parameters and data
            status = set_certificate_type(NULL, CERTIFICATE_TYPE_UNKNOWN);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = set_certificate_type(props_handle, CERTIFICATE_TYPE_UNKNOWN);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_certificate_type(NULL, &test_output);
            ASSERT_ARE_EQUAL(int, 1, status, "Line:" TOSTRING(__LINE__));
            status = get_certificate_type(props_handle, NULL);
            ASSERT_ARE_EQUAL(int, 1, status, "Line:" TOSTRING(__LINE__));

            // valid input data
            status = set_certificate_type(props_handle, CERTIFICATE_TYPE_CLIENT);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_certificate_type(props_handle, &test_output);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(int, CERTIFICATE_TYPE_CLIENT, test_output, "Line:" TOSTRING(__LINE__));

            status = set_certificate_type(props_handle, CERTIFICATE_TYPE_SERVER);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_certificate_type(props_handle, &test_output);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(int, CERTIFICATE_TYPE_SERVER, test_output, "Line:" TOSTRING(__LINE__));

            status = set_certificate_type(props_handle, CERTIFICATE_TYPE_CA);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            status = get_certificate_type(props_handle, &test_output);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(int, CERTIFICATE_TYPE_CA, test_output, "Line:" TOSTRING(__LINE__));

            //cleanup
            destroy_certificate_props(props_handle);
        }

END_TEST_SUITE(cert_props_unittests)
