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

static void* my_gballoc_calloc(size_t num, size_t size)
{
    return calloc(num, size);
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
#define TEST_STRING_128 TEST_STRING_64 TEST_STRING_64
#define TEST_STRING_129 TEST_STRING_128 "1"

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

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, my_gballoc_calloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, my_gballoc_realloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);

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

    TEST_FUNCTION(get_country_name_default_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        const char* result = get_country_name(cert_handle);

        //assert
        ASSERT_IS_NULL(result);

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

    TEST_FUNCTION(set_certificate_type_unknown_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_certificate_type(cert_handle, CERTIFICATE_TYPE_UNKNOWN);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_certificate_type_invalid_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_certificate_type(cert_handle, 500);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_certificate_type_ca_succeed)
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

    TEST_FUNCTION(set_certificate_type_server_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_certificate_type(cert_handle, CERTIFICATE_TYPE_SERVER);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);

        //cleanup
        cert_properties_destroy(cert_handle);
    }

    TEST_FUNCTION(set_certificate_type_client_succeed)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_certificate_type(cert_handle, CERTIFICATE_TYPE_CLIENT);

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

    TEST_FUNCTION(set_issuer_alias_alias_empty_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_issuer_alias(cert_handle, "");

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

    TEST_FUNCTION(set_alias_alias_empty_fail)
    {
        //arrange
        CERT_PROPS_HANDLE cert_handle = cert_properties_create();

        //act
        int result = set_alias(cert_handle, "");

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

        // default value
        test_output_string = get_state_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid handle
        status = set_common_name(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_common_name(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_common_name(props_handle, TEST_STRING_65);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_common_name(props_handle, "");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_common_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

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

    /**
    * Test function for APIs
    *   set_state_name
    *   get_state_name
    */
    TEST_FUNCTION(certificate_props_state_name)
    {
        //arrange
        int status;
        // state name max length is 128
        const char* test_input_string = TEST_STRING_128;
        const char* test_output_string;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // default value
        test_output_string = get_state_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid handle
        status = set_state_name(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_state_name(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_state_name(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_state_name(props_handle, TEST_STRING_129);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_state_name(props_handle, "");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_state_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_state_name(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_state_name(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   set_locality
    *   get_locality
    */
    TEST_FUNCTION(certificate_props_locality_name)
    {
        //arrange
        int status;
        // locality name max length is 128
        const char* test_input_string = TEST_STRING_128;
        const char* test_output_string;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // default value
        test_output_string = get_locality(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid handle
        status = set_locality(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_locality(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_locality(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_locality(props_handle, TEST_STRING_129);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_locality(props_handle, "");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_locality(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_locality(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_locality(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   set_organization_name
    *   get_organization_name
    */
    TEST_FUNCTION(certificate_props_organization_name)
    {
        //arrange
        int status;
        // org name max length is 64
        const char* test_input_string = TEST_STRING_64;
        const char* test_output_string;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // default value
        test_output_string = get_organization_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid handle
        status = set_organization_name(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_name(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_organization_name(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_organization_name(props_handle, TEST_STRING_65);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_organization_name(props_handle, "");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_name(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_organization_name(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_name(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   set_organization_unit
    *   get_organization_unit
    */
    TEST_FUNCTION(certificate_props_organization_unit_name)
    {
        //arrange
        int status;
        // org unit name max length is 64
        const char* test_input_string = TEST_STRING_64;
        const char* test_output_string;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // default value
        test_output_string = get_organization_unit(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid handle
        status = set_organization_unit(NULL, test_input_string);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_unit(NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // invalid paramters and data
        status = set_organization_unit(props_handle, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_organization_unit(props_handle, TEST_STRING_65);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = set_organization_unit(props_handle, "");
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_unit(props_handle);
        ASSERT_IS_NULL_WITH_MSG(test_output_string, "Line:" TOSTRING(__LINE__));

        // valid input data
        status = set_organization_unit(props_handle, test_input_string);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        test_output_string = get_organization_unit(props_handle);
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, test_input_string, test_output_string, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   get_san_entries
    */
    TEST_FUNCTION(certificate_props_get_san_entries_bad_params)
    {
        //arrange
        const char const** test_output;
        size_t num_entries = 10;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // act 1, assert
        test_output = get_san_entries(NULL, &num_entries);
        ASSERT_IS_NULL_WITH_MSG(test_output, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, num_entries, "Line:" TOSTRING(__LINE__));

        // act 2, assert
        test_output = get_san_entries(props_handle, NULL);
        ASSERT_IS_NULL_WITH_MSG(test_output, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   get_san_entries
    */
    TEST_FUNCTION(certificate_props_get_san_entries_default_has_no_entries)
    {
        //arrange
        const char const** test_output;
        size_t num_entries = 10;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // act
        test_output = get_san_entries(props_handle, &num_entries);

        // assert
        ASSERT_IS_NULL_WITH_MSG(test_output, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, num_entries, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    /**
    * Test function for APIs
    *   set_san_entries
    *   get_san_entries
    */
    TEST_FUNCTION(certificate_props_get_set_san_entries)
    {
        //arrange
        const char const** test_output;
        const char* test_input_string_1 = TEST_STRING_64;
        const char* test_input_string_2 = TEST_STRING_128;
        char const* san_list_1[] = { test_input_string_1, test_input_string_2 };
        size_t san_list_size_1 = sizeof(san_list_1) / sizeof(san_list_1[0]);
        const char* test_input_string_3 = "1234";
        char const* san_list_2[] = { test_input_string_3 };
        size_t san_list_size_2 = sizeof(san_list_2) / sizeof(san_list_2[0]);

        size_t num_entries, num_matched;
        int status;

        CERT_PROPS_HANDLE props_handle = cert_properties_create();

        // act 1, assert
        status = set_san_entries(props_handle, san_list_1, san_list_size_1);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        num_entries = 10;
        test_output = get_san_entries(props_handle, &num_entries);
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, san_list_size_1, num_entries, "Line:" TOSTRING(__LINE__));
        num_matched = 0;
        for (size_t i = 0; i < san_list_size_1; i++)
        {
            for (size_t j = 0; j < num_entries; j++)
            {
                if (strcmp(san_list_1[i], test_output[j]) == 0)
                {
                    num_matched++;
                    break;
                }
            }
        }
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, san_list_size_1, num_matched, "Line:" TOSTRING(__LINE__));

        // act 2, assert
        status = set_san_entries(props_handle, san_list_2, san_list_size_2);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        num_entries = 10;
        test_output = get_san_entries(props_handle, &num_entries);
        ASSERT_IS_NOT_NULL_WITH_MSG(test_output, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, san_list_size_2, num_entries, "Line:" TOSTRING(__LINE__));
        num_matched = 0;
        for (size_t i = 0; i < san_list_size_2; i++)
        {
            for (size_t j = 0; j < num_entries; j++)
            {
                if (strcmp(san_list_2[i], test_output[j]) == 0)
                {
                    num_matched++;
                    break;
                }
            }
        }
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, san_list_size_2, num_matched, "Line:" TOSTRING(__LINE__));

        //cleanup
        cert_properties_destroy(props_handle);
    }

    END_TEST_SUITE(hsm_certificate_props_ut)
