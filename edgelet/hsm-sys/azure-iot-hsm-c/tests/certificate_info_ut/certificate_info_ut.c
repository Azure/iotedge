// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifdef __cplusplus
#include <cstdlib>
#include <cstdint>
#include <cstddef>
#include <ctime>
#else
#include <stdlib.h>
#include <stdint.h>
#include <stddef.h>
#include <time.h>
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
#include "umock_c/umock_c.h"
#include "umock_c/umocktypes_charptr.h"
#include "umock_c/umocktypes_stdint.h"
#include "umock_c/umock_c_negative_tests.h"
#include "azure_macro_utils/macro_utils.h"

#include <openssl/x509.h>
#include <openssl/pem.h>

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "umock_c/umock_c_prod.h"

MOCKABLE_FUNCTION(, int, BIO_write, BIO*, b, const void*, in, int, inl);
MOCKABLE_FUNCTION(, X509*, PEM_read_bio_X509, BIO*, bp, X509**, x, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, void, BIO_free_all, BIO*, bio);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notBefore, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notAfter, X509*, x509_cert);
//https://www.openssl.org/docs/man1.1.0/crypto/OPENSSL_VERSION_NUMBER.html
// this checks if openssl version major minor is greater than or equal to version # 1.1.0
#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, const X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, const X509*, x, int, nid, int, lastpos);
    MOCKABLE_FUNCTION(, const BIO_METHOD*, BIO_s_mem);
    MOCKABLE_FUNCTION(, BIO*, BIO_new, const BIO_METHOD*, type);
#else
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, X509*, x, int, nid, int, lastpos);
    MOCKABLE_FUNCTION(, BIO_METHOD*, BIO_s_mem);
    MOCKABLE_FUNCTION(, BIO*, BIO_new, BIO_METHOD*, type);
#endif
MOCKABLE_FUNCTION(, int, X509_NAME_get_text_by_NID, X509_NAME*, name, int, nid, char*, buf, int, len);
MOCKABLE_FUNCTION(, void, X509_free, X509*, a);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "certificate_info.h"

#ifdef __cplusplus
extern "C" {
#endif

extern time_t get_utc_time_from_asn_string(const unsigned char *time_value, size_t length);

#ifdef __cplusplus
}
#endif

//#############################################################################
// Test defines and data
//#############################################################################
static TEST_MUTEX_HANDLE g_testByTest;

MU_DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

#define MAX_FAILED_FUNCTION_LIST_SIZE 64
#define TEST_BIO (BIO*)0x1000
#define TEST_BIO_METHOD (BIO_METHOD*)0x1001
#define TEST_X509 (X509*)0x1002
#define TEST_X509_SUBJECT_NAME (X509_NAME*)0x1003
#define TEST_COMMON_NAME "TEST_CN"
#define VALID_ASN1_TIME_STRING_UTC_FORMAT   0x17
#define VALID_ASN1_TIME_STRING_UTC_LEN      13
#define INVALID_ASN1_TIME_STRING_UTC_FORMAT 0
#define INVALID_ASN1_TIME_STRING_UTC_LEN    0
#define MAX_COMMON_NAME_SIZE 65

//#define TEST_BEFORE_ASN1_STRING (unsigned char*)"BEF012345678"
static ASN1_TIME TEST_ASN1_TIME_BEFORE = {
    .length = VALID_ASN1_TIME_STRING_UTC_LEN,
    .type = VALID_ASN1_TIME_STRING_UTC_FORMAT,
    .data = (unsigned char*)"BEF012345678",
    .flags = 0
};

//#define TEST_AFTER_ASN1_STRING (unsigned char*)"AFT012345678"
static ASN1_TIME TEST_ASN1_TIME_AFTER = {
    .length = VALID_ASN1_TIME_STRING_UTC_LEN,
    .type = VALID_ASN1_TIME_STRING_UTC_FORMAT,
    .data = (unsigned char*)"AFT012345678",
    .flags = 0
};

static const int64_t RSA_CERT_VALID_FROM_TIME = 1484940333;
static const int64_t RSA_CERT_VALID_TO_TIME = 1800300333;

#define WIN_EOL_LEAF_CERT_CONTENT  "TEST_WIN_LEAF_CERT"
#define WIN_EOL_CHAIN_CERT_CONTENT "TEST_WIN_CHAIN_CERT"

#define NIX_EOL_LEAF_CERT_CONTENT  "TEST_NIX_LEAF_CERT"
#define NIX_EOL_CHAIN_CERT_CONTENT "TEST_NIX_CHAIN_CERT"

static const char* TEST_CERT_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
WIN_EOL_LEAF_CERT_CONTENT"\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_CERT_CHAIN_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
WIN_EOL_CHAIN_CERT_CONTENT"\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_CERT_FULL_CHAIN_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
WIN_EOL_LEAF_CERT_CONTENT"\r\n"
"-----END CERTIFICATE-----""\r\n"
"-----BEGIN CERTIFICATE-----""\r\n"
WIN_EOL_CHAIN_CERT_CONTENT"\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_CERT_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
NIX_EOL_LEAF_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_CERT_CHAIN_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
NIX_EOL_CHAIN_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_CERT_FULL_CHAIN_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
NIX_EOL_LEAF_CERT_CONTENT"\n"
"-----END CERTIFICATE-----""\n"
"-----BEGIN CERTIFICATE-----""\n"
NIX_EOL_CHAIN_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_CERT_NO_BEGIN_MARKER =
NIX_EOL_CHAIN_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_CERT_NO_END_MARKER =
NIX_EOL_LEAF_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_CERT_CHAIN_NO_BEGIN_MARKER =
"-----BEGIN CERTIFICATE-----""\n"
NIX_EOL_LEAF_CERT_CONTENT"\n"
"-----END CERTIFICATE-----""\n"
NIX_EOL_CHAIN_CERT_CONTENT"\n"
"-----END CERTIFICATE-----\n";

static const unsigned char TEST_PRIVATE_KEY[] = { 0x32, 0x03, 0x33, 0x34, 0x35, 0x36 };
static size_t TEST_PRIVATE_KEY_LEN = sizeof(TEST_PRIVATE_KEY)/sizeof(TEST_PRIVATE_KEY[0]);

//#############################################################################
// Mocked functions test hooks
//#############################################################################

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static BIO* test_hook_BIO_new(const BIO_METHOD* type)
#else
static BIO* test_hook_BIO_new(BIO_METHOD* type)
#endif
{
    (void)type;
    return TEST_BIO;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static const BIO_METHOD* test_hook_BIO_s_mem(void)
#else
static BIO_METHOD* test_hook_BIO_s_mem(void)
#endif
{
    return TEST_BIO_METHOD;
}

static int test_hook_BIO_write(BIO *b, const void *in, int inl)
{
    (void)b;
    (void)in;

    return inl;
}

static X509* test_hook_PEM_read_bio_X509(BIO *bp, X509 **x, pem_password_cb *cb, void *u)
{
    (void)bp;
    (void)x;
    (void)cb;
    (void)u;

    return TEST_X509;
}

static void test_hook_BIO_free_all(BIO *bio)
{
    (void)bio;
}

static ASN1_TIME* test_hook_X509_get_notAfter(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_AFTER;
}

static ASN1_TIME* test_hook_X509_get_notBefore(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_BEFORE;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static X509_NAME* test_hook_X509_get_subject_name(const X509 *a)
#else
static X509_NAME* test_hook_X509_get_subject_name(X509 *a)
#endif
{
    (void)a;
    return TEST_X509_SUBJECT_NAME;
}

static void test_hook_X509_free(X509 *a)
{
    (void)a;
}

static int test_hook_X509_NAME_get_text_by_NID(X509_NAME *name, int nid, char *buf, int len)
{
    (void)name;
    (void)buf;
    (void)len;
    int result = 0;
    const char *value;

    if ((len == 0) || (buf == NULL))
    {
        value = NULL;
        result = 0;
    }
    else
    {
        switch (nid)
        {
            case NID_commonName:
            value = TEST_COMMON_NAME;
            result = 1;
            break;

            default:
                value = NULL;
                result = 0;
        };
    }

    memset(buf, 0, len);
    if ((result == 1) && (value != NULL))
    {
        strncpy(buf, value, len - 1);
    }
    return result;
}

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", MU_ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

#ifdef CPP_UNITTEST
/*apparently CppUniTest.h does not define the below which is needed for int64_t asserts*/
template <> static std::wstring Microsoft::VisualStudio::CppUnitTestFramework::ToString < int64_t >(const int64_t& q)
{
    std::wstring result;
    std::wostringstream o;
    o << q;
    return o.str();
}
#endif

//#############################################################################
// Test helpers
//#############################################################################
struct CALLSTACK_OVERRIDE_TAG
{
    bool fail_common_name_lookup;
};
typedef struct CALLSTACK_OVERRIDE_TAG CALLSTACK_OVERRIDE;


static void test_helper_parse_cert_common_callstack
(
    const char* certificate,
    size_t certificate_size,
    bool private_key_set,
    char *failed_function_list,
    size_t failed_function_size,
    CALLSTACK_OVERRIDE* overrride
)
{
    uint64_t failed_function_bitmask = 0;
    size_t i = 0;
    size_t certificate_len = strlen(certificate);

    umock_c_reset_all_calls();

    EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(gballoc_malloc(certificate_size));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    // *************** Load and parse certificate **************
    EXPECTED_CALL(BIO_s_mem());
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(BIO_new(TEST_BIO_METHOD));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    ASSERT_IS_FALSE(certificate_len > INT_MAX, "Line:" MU_TOSTRING(__LINE__));
    STRICT_EXPECTED_CALL(BIO_write(TEST_BIO, IGNORED_PTR_ARG, (int)certificate_len));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(PEM_read_bio_X509(TEST_BIO, NULL, NULL, NULL));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Parse validity timestamps **************
    STRICT_EXPECTED_CALL(mocked_X509_get_notAfter(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(mocked_X509_get_notBefore(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Parse common name **************
    STRICT_EXPECTED_CALL(X509_get_subject_name(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(gballoc_malloc(MAX_COMMON_NAME_SIZE));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    // conditionally fail since certificates may not have a CN field
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    if (overrride && overrride->fail_common_name_lookup)
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(TEST_X509_SUBJECT_NAME, NID_commonName, IGNORED_PTR_ARG, MAX_COMMON_NAME_SIZE)).SetReturn(-1);
        failed_function_list[i++] = 1;
    }
    else
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(TEST_X509_SUBJECT_NAME, NID_commonName, IGNORED_PTR_ARG, MAX_COMMON_NAME_SIZE));
        i++;
    }

    STRICT_EXPECTED_CALL(X509_free(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Finalize certificate info object **************
    // allocator for the first certificate which includes /r/n ending
    STRICT_EXPECTED_CALL(gballoc_malloc(certificate_size));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    // allocator for the private key
    if (private_key_set)
    {
        STRICT_EXPECTED_CALL(gballoc_malloc(TEST_PRIVATE_KEY_LEN));
        ASSERT_IS_TRUE((i < failed_function_size), "Line:" MU_TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }
    // *************************************************
}

static void test_helper_parse_cert_callstack
(
    const char* certificate,
    size_t certificate_size,
    char *failed_function_list,
    size_t failed_function_size
)
{
    test_helper_parse_cert_common_callstack(certificate, certificate_size, true, failed_function_list, failed_function_size, NULL);
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(certificate_info_ut)

    TEST_SUITE_INITIALIZE(suite_init)
    {
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);
        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types());
        ASSERT_ARE_EQUAL(int, 0, umocktypes_stdint_register_types());

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_s_mem, test_hook_BIO_s_mem);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_new, test_hook_BIO_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_new, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_write, test_hook_BIO_write);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_write, 0);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_read_bio_X509, test_hook_PEM_read_bio_X509);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_read_bio_X509, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_free_all, test_hook_BIO_free_all);

        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notBefore, test_hook_X509_get_notBefore);
        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notAfter, test_hook_X509_get_notAfter);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_subject_name, test_hook_X509_get_subject_name);

        REGISTER_GLOBAL_MOCK_HOOK(X509_NAME_get_text_by_NID, test_hook_X509_NAME_get_text_by_NID);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_NAME_get_text_by_NID, -1);

        REGISTER_GLOBAL_MOCK_HOOK(X509_free, test_hook_X509_free);
    }

    TEST_SUITE_CLEANUP(suite_cleanup)
    {
        umock_c_deinit();

        TEST_MUTEX_DESTROY(g_testByTest);
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

    TEST_FUNCTION(certificate_info_create_cert_NULL_fail)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(NULL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_cert_empty_string_fail)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create("", TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_type_unknown_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_type_invalid_fails)
    {
        //arrange
        int BAD_PRIVATE_KEY_TYPE = 50;

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, (PRIVATE_KEY_TYPE)BAD_PRIVATE_KEY_TYPE);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_type_payload_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_type_reference_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_REFERENCE);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_size_non_zero_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, NULL, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_non_null_zero_length_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, 0, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_rsa_win_succeed)
    {
        //arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];

        test_helper_parse_cert_callstack(TEST_CERT_WIN_EOL, strlen(TEST_CERT_WIN_EOL) + 1, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_rsa_nix_succeed)
    {
        //arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];

        test_helper_parse_cert_callstack(TEST_CERT_NIX_EOL, strlen(TEST_CERT_NIX_EOL) + 1, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_no_private_key_succeed)
    {
        //arrange
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0 , sizeof(failed_function_list));
        test_helper_parse_cert_common_callstack(TEST_CERT_NIX_EOL, strlen(TEST_CERT_NIX_EOL) + 1, false, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE, NULL);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NIX_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_fail)
    {
        //arrange
        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0 , sizeof(failed_function_list));
        test_helper_parse_cert_common_callstack(TEST_CERT_WIN_EOL, strlen(TEST_CERT_WIN_EOL) + 1, false, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE, NULL);

        umock_c_negative_tests_snapshot();

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            if (failed_function_list[index] == 1)
            {
                char tmp_msg[64];
                sprintf(tmp_msg, "certificate_info_create failure in test %zu/%zu", index, count);

                // act
                CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

                // assert
                ASSERT_IS_NULL(cert_handle, tmp_msg);
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(certificate_info_destroy_with_private_key_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        umock_c_reset_all_calls();
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        //act
        certificate_info_destroy(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(certificate_info_destroy_without_private_key_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);
        umock_c_reset_all_calls();
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        //act
        certificate_info_destroy(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(certificate_info_destroy_handle_NULL_does_nothing)
    {
        //arrange

        //act
        certificate_info_destroy(NULL);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_certificate_win_eol_succees)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_WIN_EOL, certificate);
        int cmp = strcmp(TEST_CERT_WIN_EOL, certificate);
        ASSERT_ARE_EQUAL(int, 0, cmp);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_nix_eol_succees)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_NIX_EOL, certificate);
        int cmp = strcmp(TEST_CERT_NIX_EOL, certificate);
        ASSERT_ARE_EQUAL(int, 0, cmp);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_fail)
    {
        //arrange

        //act
        const char* certificate = certificate_info_get_certificate(NULL);

        //assert
        ASSERT_IS_NULL(certificate);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_certificate_leaf_fail)
    {
        //arrange

        //act
        const char* certificate = certificate_info_get_leaf_certificate(NULL);

        //assert
        ASSERT_IS_NULL(certificate);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_certificate_leaf_win_eol_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

         //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);

         //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_WIN_EOL, certificate);

         //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_leaf_nix_eol_success)
    {
        //arrange

        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_NIX_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_private_key_succeed)
    {
        //arrange
        size_t pk_len;
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(cert_handle, &pk_len);

        //assert
        ASSERT_IS_NOT_NULL(priv_key);
        ASSERT_ARE_EQUAL(int, 0, memcmp(priv_key, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN));
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_len);

        //cleanup
        certificate_info_destroy(cert_handle);
    }


    TEST_FUNCTION(certificate_info_get_certificate_no_chain_win_eol_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);
        const char* chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_IS_NULL(chain);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_WIN_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_no_chain_nix_eol_success)
    {
        //arrange

        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);
        const char* chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_IS_NULL(chain);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_NIX_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_with_chain_win_eol_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_FULL_CHAIN_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);
        const char* chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_IS_NOT_NULL(chain);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_WIN_EOL, certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_CHAIN_WIN_EOL, chain);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_with_chain_nix_eol_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_FULL_CHAIN_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);
        const char* chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_IS_NOT_NULL(chain);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_NIX_EOL, certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_CHAIN_NIX_EOL, chain);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_private_key_handle_NULL_fail)
    {
        //arrange
        size_t pk_len = 123;

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(NULL, &pk_len);

        //assert
        ASSERT_IS_NULL(priv_key);
        ASSERT_ARE_EQUAL(size_t, 123, pk_len);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_private_key_length_NULL_fail)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(cert_handle, NULL);

        //assert
        ASSERT_IS_NULL(priv_key);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_valid_from_handle_NULL_fail)
    {
        //arrange

        //act
        int64_t valid_from = certificate_info_get_valid_from(NULL);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, valid_from);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_valid_to_handle_NULL_fail)
    {
        //arrange

        //act
        int64_t valid_to = certificate_info_get_valid_to(NULL);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, valid_to);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_private_key_type_handle_NULL_fail)
    {
        //arrange

        //act
        (void)certificate_info_private_key_type(NULL);

        //assert

        //cleanup
    }


    TEST_FUNCTION(certificate_info_get_chain_handle_NULL_fail)
    {
        //arrange

        //act
        (void)certificate_info_get_chain(NULL);

        //assert

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_invalid_smaller_len_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 12);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_invalid_larger_len_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 14);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_success_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 13);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 1514768461, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_success_test_y2038)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"491231235959Z", 13);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 2524607999, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_common_name_NULL_param_fails)
    {
        //arrange

        // act
        const char* result = certificate_info_get_common_name(NULL);

        // assert
        ASSERT_IS_NULL(result);
    }


    TEST_FUNCTION(get_common_name_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        ASSERT_IS_NOT_NULL(cert_handle);

        // act
        const char* result = certificate_info_get_common_name(cert_handle);

        // assert
        ASSERT_IS_NOT_NULL(result);
        int cmp = strcmp(TEST_COMMON_NAME, result);
        ASSERT_ARE_EQUAL(int, 0, cmp);

        // cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(get_common_name_not_in_certificate_returns_NULL)
    {
        //arrange
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0 , sizeof(failed_function_list));
        CALLSTACK_OVERRIDE overrride = { true };
        test_helper_parse_cert_common_callstack(TEST_CERT_WIN_EOL, strlen(TEST_CERT_WIN_EOL) + 1, false, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE, &overrride);
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        ASSERT_IS_NOT_NULL(cert_handle);

        // act
        const char* result = certificate_info_get_common_name(cert_handle);

        // assert
        ASSERT_IS_NULL(result);

        // cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_fails_with_no_begin_marker)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NO_BEGIN_MARKER, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_create_fails_with_no_end_marker)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_NO_END_MARKER, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_create_fails_with_no_begin_marker_for_chain)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_CHAIN_NO_BEGIN_MARKER, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        ASSERT_IS_NOT_NULL(cert_handle);

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);
        const char* chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_IS_NULL(chain);
        ASSERT_ARE_EQUAL(char_ptr, TEST_CERT_NIX_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

END_TEST_SUITE(certificate_info_ut)
