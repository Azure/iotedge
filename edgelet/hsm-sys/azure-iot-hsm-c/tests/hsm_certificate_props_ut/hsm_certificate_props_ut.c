// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifdef __cplusplus
#include <cstdlib>
#include <cstdint>
#include <cstddef>
#else
#include <stdlib.h>
#include <stdint.h>
#include <stddef.h>
#endif

static void* my_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void my_gballoc_free(void* ptr)
{
    free(ptr);
}

static void* my_gballoc_realloc(void* ptr, size_t size)
{
    return realloc(ptr, size);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umocktypes_charptr.h"
#include "umocktypes_stdint.h"
#include "umocktypes_bool.h"
#include "umock_c_negative_tests.h"
#include "azure_c_shared_utility/macro_utils.h"

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/umock_c_prod.h"
#undef ENABLE_MOCKS

#include "hsm_certificate_props.h"

#ifdef __cplusplus
extern "C"
{
#endif
#ifdef __cplusplus
}
#endif

static const uint64_t TEST_VALIDITY_MIN = 30;
static const char* TEST_COMMON_NAME = "test_common_name";
static const char* TEST_COUNTRY_NAME = "UA";
static const char* TEST_ISSUER_ALIAS_VALUE = "test_issuer_alias";
static const char* TEST_ALIAS_VALUE = "test_alias";

#define TEST_STRING_64 "0123456789012345678901234567890123456789012345678901234567890123"
#define TEST_STRING_65  TEST_STRING_64 "1"

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static void on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

BEGIN_TEST_SUITE(hsm_certificate_props_ut)

    TEST_SUITE_INITIALIZE(suite_init)
    {
        //int result;
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        (void)umock_c_init(on_umock_c_error);
        (void)umocktypes_bool_register_types();
        (void)umocktypes_stdint_register_types();
    }

    TEST_SUITE_CLEANUP(suite_cleanup)
    {
        umock_c_deinit();

        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(method_init)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Could not acquire test serialization mutex.");
        }
        umock_c_reset_all_calls();
    }

    TEST_FUNCTION_CLEANUP(method_cleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(cert_properties_create_succeed)
    {
        //arrange

        //act
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(cert_properties_destroy_handle_NULL_succeed)
    {
        //arrange

        //act
        cert_properties_destroy(NULL);

        //assert

        //cleanup
    }

    TEST_FUNCTION(cert_properties_destroy_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        cert_properties_destroy(cert_handle);

        //assert

        //cleanup
    }

    TEST_FUNCTION(set_validity_seconds_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_validity_seconds(NULL, TEST_VALIDITY_MIN);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_validity_seconds_validity_zero_fail)
    {
        //arrange
        uint64_t validity_min = 0;
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_validity_seconds(cert_handle, validity_min);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_validity_seconds_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_validity_seconds(cert_handle, TEST_VALIDITY_MIN);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_validity_seconds_handle_NULL_fail)
    {
        //arrange

        //act
        uint64_t validity_min = get_validity_seconds(NULL);

        //assert
        ASSERT_ARE_EQUAL(uint64_t, 0, validity_min);

        //cleanup
    }

    TEST_FUNCTION(get_validity_seconds_default_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        uint64_t validity_min = get_validity_seconds(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(uint64_t, 0, validity_min);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_validity_seconds_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_validity_seconds(cert_handle, TEST_VALIDITY_MIN);

        //act
        uint64_t validity_min = get_validity_seconds(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(uint64_t, TEST_VALIDITY_MIN, validity_min);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_common_name_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_common_name(NULL, TEST_COMMON_NAME);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_common_name_common_name_NULL_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_common_name(cert_handle, NULL);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_common_name_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_common_name(cert_handle, TEST_COMMON_NAME);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_common_name_handle_NULL_fail)
    {
        //arrange

        //act
        const char* result = get_common_name(NULL);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
    }

    TEST_FUNCTION(get_common_name_default_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        const char* result = get_common_name(cert_handle);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_common_name_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_common_name(cert_handle, TEST_COMMON_NAME);

        //act
        const char* result = get_common_name(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL(char_ptr, TEST_COMMON_NAME, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_country_name_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_country_name(NULL, TEST_COUNTRY_NAME);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_country_name_too_long_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_country_name(cert_handle, TEST_COMMON_NAME);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_country_name_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_country_name(cert_handle, TEST_COUNTRY_NAME);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_country_name_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_country_name(cert_handle, TEST_COUNTRY_NAME);

        //act
        const char* result = get_country_name(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL(char_ptr, TEST_COUNTRY_NAME, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_certificate_type_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_certificate_type(NULL, CERTIFICATE_TYPE_CA);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_certificate_type_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_certificate_type(cert_handle, CERTIFICATE_TYPE_CA);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_certificate_type_handle_NULL_fail)
    {
        //arrange

        //act
        CERTIFICATE_TYPE result = get_certificate_type(NULL);

        //assert
        ASSERT_ARE_EQUAL(int, CERTIFICATE_TYPE_UNKNOWN, result);

        //cleanup
    }

    TEST_FUNCTION(get_certificate_type_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_certificate_type(cert_handle, CERTIFICATE_TYPE_CA);

        //act
        CERTIFICATE_TYPE result = get_certificate_type(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int, CERTIFICATE_TYPE_CA, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_issuer_alias_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_issuer_alias(NULL, TEST_ISSUER_ALIAS_VALUE);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_issuer_alias_alias_NULL_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_issuer_alias(cert_handle, NULL);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_issuer_alias_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_issuer_alias(cert_handle, TEST_ISSUER_ALIAS_VALUE);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_issuer_alias_handle_NULL_fail)
    {
        //arrange

        //act
        const char* result = get_issuer_alias(NULL);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
    }

    TEST_FUNCTION(get_issuer_alias_default_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        const char* result = get_issuer_alias(cert_handle);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_issuer_alias_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_issuer_alias(cert_handle, TEST_ISSUER_ALIAS_VALUE);

        //act
        const char* result = get_issuer_alias(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL(char_ptr, TEST_ISSUER_ALIAS_VALUE, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_alias_handle_NULL_fail)
    {
        //arrange

        //act
        int result = set_alias(NULL, TEST_ALIAS_VALUE);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
    }

    TEST_FUNCTION(set_alias_alias_NULL_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_alias(cert_handle, NULL);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_alias_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_alias(cert_handle, TEST_ALIAS_VALUE);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_alias_handle_NULL_fail)
    {
        //arrange

        //act
        const char* result = get_alias(NULL);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
    }

    TEST_FUNCTION(get_alias_default_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        const char* result = get_alias(cert_handle);

        //assert
        ASSERT_IS_NULL(result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(get_alias_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();
        (void)set_alias(cert_handle, TEST_ALIAS_VALUE);

        //act
        const char* result = get_alias(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(result);
        ASSERT_ARE_EQUAL(char_ptr, TEST_ALIAS_VALUE, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    /**
    * Test function for APIs
    *   set_validity_seconds
    *   get_validity_seconds
    */
    TEST_FUNCTION(cert_properties_create_validity)
    {
        //arrange
        int status;
        uint64_t validity = 0;
        const uint64_t test_validity_value = 10;
        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // invalid handle
        status = set_validity_seconds(NULL, test_validity_value);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 1, status, "Line:" TOSTRING(__LINE__));
        validity = get_validity_seconds(NULL);
        ASSERT_ARE_EQUAL_WITH_MSG(uint64_t, 0, validity, "Line:" TOSTRING(__LINE__));

        // invalid input data
        status = set_validity_seconds(props_handle, 0);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        validity = get_validity_seconds(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(uint64_t, 0, validity, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_validity_seconds(props_handle, test_validity_value);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        validity = get_validity_seconds(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(uint64_t, test_validity_value, validity, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
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
        const char* test_input_string = TEST_STRING_64;
        const char* test_output_string;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // invalid handle
        status = set_common_name(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_common_name(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_common_name(props_handle, TEST_STRING_65);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_common_name(props_handle, TEST_STRING_65);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(props_handle);
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_common_name(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid input for get_common_name
        status = set_common_name(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

#if 0
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

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

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
        cert_properties_destroy(props_handle);
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

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

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
        cert_properties_destroy(props_handle);
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
        CERT_PROPS_HANDLE props_handle = cert_properties_create();

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
        cert_properties_destroy(props_handle);
    }
#endif

    END_TEST_SUITE(hsm_certificate_props_ut)
