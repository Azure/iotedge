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
#include "umock_c/umock_c.h"
#include "umock_c/umocktypes_stdint.h"
#include "umock_c/umock_c_negative_tests.h"
#include "umock_c/umocktypes_charptr.h"

//#############################################################################
// Declare and enable MOCK definitions
//#############################################################################

#define ENABLE_MOCKS
#include "hsm_client_store.h"
#include "azure_c_shared_utility/gballoc.h"

// store mocks
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_create, const char*, store_name, uint64_t, auto_generated_ca_lifetime);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_destroy, const char*, store_name);
MOCKABLE_FUNCTION(, HSM_CLIENT_STORE_HANDLE, mocked_hsm_client_store_open, const char*, store_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_close, HSM_CLIENT_STORE_HANDLE, handle);

// store key mocks
MOCKABLE_FUNCTION(, KEY_HANDLE, mocked_hsm_client_store_open_key, HSM_CLIENT_STORE_HANDLE, handle, HSM_KEY_T, key_type, const char*, key_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_close_key, HSM_CLIENT_STORE_HANDLE, handle, KEY_HANDLE, key_handle);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_remove_key, HSM_CLIENT_STORE_HANDLE, handle, HSM_KEY_T, key_type, const char*, key_name);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_insert_sas_key, HSM_CLIENT_STORE_HANDLE, handle, const char*, key_name, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_insert_encryption_key, HSM_CLIENT_STORE_HANDLE, handle, const char*, key_name);

// store pki mocks
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_create_pki_cert, HSM_CLIENT_STORE_HANDLE, handle, CERT_PROPS_HANDLE, cert_props_handle);
MOCKABLE_FUNCTION(, CERT_INFO_HANDLE, mocked_hsm_client_store_get_pki_cert, HSM_CLIENT_STORE_HANDLE, handle, const char*, alias);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_remove_pki_cert, HSM_CLIENT_STORE_HANDLE, handle, const char*, alias);

// store trusted pki mocks
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_insert_pki_trusted_cert, HSM_CLIENT_STORE_HANDLE, handle, const char*, alias, const char*, file_name);
MOCKABLE_FUNCTION(, CERT_INFO_HANDLE, mocked_hsm_client_store_get_pki_trusted_certs, HSM_CLIENT_STORE_HANDLE, handle);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_store_remove_pki_trusted_cert, HSM_CLIENT_STORE_HANDLE, handle, const char*, alias);

// key interface mocks
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_sign, KEY_HANDLE, key_handle, const unsigned char*, data_to_be_signed, size_t, data_len, unsigned char**, digest, size_t*, digest_size);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_derive_and_sign, KEY_HANDLE, key_handle, const unsigned char*, data_to_be_signed, size_t, data_len, const unsigned char*, identity, size_t, identity_size, unsigned char**, digest, size_t*, digest_size);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_encrypt, KEY_HANDLE, key_handle, const SIZED_BUFFER*, identity, const SIZED_BUFFER*, plaintext, const SIZED_BUFFER*, iv, SIZED_BUFFER*, ciphertext);
MOCKABLE_FUNCTION(, int, mocked_hsm_client_key_decrypt, KEY_HANDLE, key_handle, const SIZED_BUFFER*, identity, const SIZED_BUFFER*, ciphertext, const SIZED_BUFFER*, iv, SIZED_BUFFER*, plaintext);
MOCKABLE_FUNCTION(, void, mocked_hsm_client_key_destroy, KEY_HANDLE, key_handle);

// interface mocks
MOCKABLE_FUNCTION(, const HSM_CLIENT_STORE_INTERFACE*, hsm_client_store_interface);
MOCKABLE_FUNCTION(, const HSM_CLIENT_KEY_INTERFACE*, hsm_client_key_interface);

MOCKABLE_FUNCTION(, CERT_INFO_HANDLE, certificate_info_create, const char*, certificate, const void*, private_key, size_t, priv_key_len, PRIVATE_KEY_TYPE, pk_type);
MOCKABLE_FUNCTION(, const char*, get_alias, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_issuer_alias, CERT_PROPS_HANDLE, handle);

MOCKABLE_FUNCTION(, int, generate_rand_buffer, unsigned char*, buffer, size_t, num_bytes);

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
#define TEST_CERT_INFO_HANDLE (CERT_INFO_HANDLE)0x1003
#define TEST_TRUST_BUNDLE_CERT_INFO_HANDLE (CERT_INFO_HANDLE)0x1004
#define TEST_CERT_PROPS_HANDLE (CERT_PROPS_HANDLE)0x1005

MU_DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static const char* TEST_ALIAS_STRING = "test_alias";
static const char* TEST_ISSUER_ALIAS_STRING = "test_issuer_alias";

// 90 days.
static const uint64_t TEST_CA_VALIDITY =  90 * 24 * 3600;

static const unsigned char TEST_TBS[] = { 't', 'e', 's', 't' };
static const size_t TEST_TBS_SIZE = sizeof(TEST_TBS);

static unsigned char TEST_DIGEST_BUFFER[] = { 'b', 'u', 'f', 'f', 'e', 'r' };
static size_t TEST_DIGEST_BUFFER_SIZE = sizeof(TEST_DIGEST_BUFFER);

static const HSM_CLIENT_STORE_INTERFACE mocked_hsm_client_store_interface =
{
    mocked_hsm_client_store_create,
    mocked_hsm_client_store_destroy,
    mocked_hsm_client_store_open,
    mocked_hsm_client_store_close,
    mocked_hsm_client_store_open_key,
    mocked_hsm_client_store_close_key,
    mocked_hsm_client_store_remove_key,
    mocked_hsm_client_store_insert_sas_key,
    mocked_hsm_client_store_insert_encryption_key,
    mocked_hsm_client_store_create_pki_cert,
    mocked_hsm_client_store_get_pki_cert,
    mocked_hsm_client_store_remove_pki_cert,
    mocked_hsm_client_store_insert_pki_trusted_cert,
    mocked_hsm_client_store_get_pki_trusted_certs,
    mocked_hsm_client_store_remove_pki_trusted_cert
};

static const HSM_CLIENT_KEY_INTERFACE mocked_hsm_client_key_interface =
{
    mocked_hsm_client_key_sign,
    mocked_hsm_client_key_derive_and_sign,
    mocked_hsm_client_key_encrypt,
    mocked_hsm_client_key_decrypt,
    mocked_hsm_client_key_destroy
};

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

const HSM_CLIENT_STORE_INTERFACE* test_hook_hsm_client_store_interface(void)
{
    return &mocked_hsm_client_store_interface;
}

const HSM_CLIENT_KEY_INTERFACE* test_hook_hsm_client_key_interface(void)
{
    return &mocked_hsm_client_key_interface;
}

static int test_hook_hsm_client_store_create(const char* store_name, uint64_t auto_generated_ca_lifetime)
{
    (void)auto_generated_ca_lifetime;
    (void)store_name;
    return 0;
}

static int test_hook_hsm_client_store_destroy(const char* store_name)
{
    (void)store_name;
    return 0;
}

static HSM_CLIENT_STORE_HANDLE test_hook_hsm_client_store_open(const char* store_name)
{
    (void)store_name;
    return TEST_HSM_STORE_HANDLE;
}

static int test_hook_hsm_client_store_close(HSM_CLIENT_STORE_HANDLE handle)
{
    (void)handle;
    return 0;
}

static KEY_HANDLE test_hook_hsm_client_store_open_key(HSM_CLIENT_STORE_HANDLE handle,
                                                      HSM_KEY_T key_type,
                                                      const char* key_name)
{
    (void)handle;
    (void)key_type;
    (void)key_name;
    return TEST_KEY_HANDLE;
}

static int test_hook_hsm_client_store_close_key(HSM_CLIENT_STORE_HANDLE handle,
                                                KEY_HANDLE key_handle)
{
    (void)handle;
    (void)key_handle;

    return 0;
}

static int test_hook_hsm_client_store_remove_key(HSM_CLIENT_STORE_HANDLE handle,
                                                 HSM_KEY_T key_type,
                                                 const char* key_name)
{
    (void)handle;
    (void)key_type;
    (void)key_name;
    return 0;
}

static int test_hook_hsm_client_store_insert_sas_key(HSM_CLIENT_STORE_HANDLE handle,
                                                     const char* key_name,
                                                     const unsigned char* key,
                                                     size_t key_len)
{
    (void)handle;
    (void)key_name;
    (void)key;
    (void)key_len;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static int test_hook_hsm_client_store_insert_encryption_key(HSM_CLIENT_STORE_HANDLE handle,
                                                            const char* key_name)
{
    (void)handle;
    (void)key_name;
    return 0;
}

static int test_hook_hsm_client_store_create_pki_cert(HSM_CLIENT_STORE_HANDLE handle,
                                                      CERT_PROPS_HANDLE cert_props_handle)
{
    (void)handle;
    (void)cert_props_handle;
    return 0;
}

static CERT_INFO_HANDLE test_hook_hsm_client_store_get_pki_cert(HSM_CLIENT_STORE_HANDLE handle,
                                                                const char* alias)
{
    (void)handle;
    (void)alias;
    return TEST_CERT_INFO_HANDLE;
}

static int test_hook_hsm_client_store_remove_pki_cert(HSM_CLIENT_STORE_HANDLE handle,
                                                      const char* alias)
{
    (void)handle;
    (void)alias;
    return 0;
}

static int test_hook_hsm_client_store_insert_pki_trusted_cert(HSM_CLIENT_STORE_HANDLE handle,
                                                              const char* alias,
                                                              const char* file_name)
{
    (void)handle;
    (void)alias;
    (void)file_name;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static CERT_INFO_HANDLE test_hook_hsm_client_store_get_pki_trusted_certs
(
    HSM_CLIENT_STORE_HANDLE handle
)
{
    (void)handle;
    return TEST_TRUST_BUNDLE_CERT_INFO_HANDLE;
}

static int test_hook_hsm_client_store_remove_pki_trusted_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
)
{
    (void)handle;
    (void)alias;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static int test_hook_hsm_client_key_sign(KEY_HANDLE key_handle,
                                         const unsigned char* data_to_be_signed,
                                         size_t data_len,
                                         unsigned char** digest,
                                         size_t* digest_size)
{
    (void)key_handle;
    (void)data_to_be_signed;
    (void)data_len;
    *digest = TEST_DIGEST_BUFFER;
    *digest_size = TEST_DIGEST_BUFFER_SIZE;

    return 0;
}

static int test_hook_hsm_client_key_derive_and_sign(KEY_HANDLE key_handle,
                                                    const unsigned char* data_to_be_signed,
                                                    size_t data_len,
                                                    const unsigned char* identity,
                                                    size_t identity_size,
                                                    unsigned char** digest,
                                                    size_t* digest_size)
{
    (void)key_handle;
    (void)data_to_be_signed;
    (void)data_len;
    (void)identity;
    (void)identity_size;
    (void)digest;
    (void)digest_size;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static int test_hook_hsm_client_key_encrypt(KEY_HANDLE key_handle,
                                            const SIZED_BUFFER *identity,
                                            const SIZED_BUFFER *plaintext,
                                            const SIZED_BUFFER *initialization_vector,
                                            SIZED_BUFFER *ciphertext)
{
    (void)key_handle;
    (void)identity;
    (void)plaintext;
    (void)initialization_vector;
    (void)ciphertext;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static int test_hook_hsm_client_key_decrypt(KEY_HANDLE key_handle,
                                            const SIZED_BUFFER *identity,
                                            const SIZED_BUFFER *ciphertext,
                                            const SIZED_BUFFER *initialization_vector,
                                            SIZED_BUFFER *plaintext)
{
    (void)key_handle;
    (void)identity;
    (void)ciphertext;
    (void)initialization_vector;
    (void)plaintext;
    ASSERT_FAIL("API not expected to be called");
    return __LINE__;
}

static void test_hook_hsm_client_key_destroy(KEY_HANDLE key_handle)
{
    (void)key_handle;
    ASSERT_FAIL("API not expected to be called");
}

static const char* test_hook_get_alias(CERT_PROPS_HANDLE handle)
{
    (void)handle;
    return TEST_ALIAS_STRING;
}

static const char* test_hook_get_issuer_alias(CERT_PROPS_HANDLE handle)
{
    (void)handle;
    return TEST_ISSUER_ALIAS_STRING;
}

static CERT_INFO_HANDLE test_hook_certificate_info_create
(
    const char* certificate,
    const void* private_key,
    size_t priv_key_len,
    PRIVATE_KEY_TYPE pk_type
)
{
    (void)certificate;
    (void)private_key;
    (void)priv_key_len;
    (void)pk_type;
    return TEST_CERT_INFO_HANDLE;
}

static int test_hook_generate_rand_buffer(unsigned char *buffer, size_t num_bytes)
{
    (void)buffer;
    (void)num_bytes;

    return 0;
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_crypto_unittests)

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
            REGISTER_UMOCK_ALIAS_TYPE(TEST_CERT_INFO_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(CERT_PROPS_HANDLE, void*);
            REGISTER_UMOCK_ALIAS_TYPE(PRIVATE_KEY_TYPE, int);
            REGISTER_UMOCK_ALIAS_TYPE(HSM_KEY_T, int);

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

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_insert_encryption_key, test_hook_hsm_client_store_insert_encryption_key);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_insert_encryption_key, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_create_pki_cert, test_hook_hsm_client_store_create_pki_cert);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_create_pki_cert, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_get_pki_cert, test_hook_hsm_client_store_get_pki_cert);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_get_pki_cert, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_remove_pki_cert, test_hook_hsm_client_store_remove_pki_cert);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_remove_pki_cert, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_insert_pki_trusted_cert, test_hook_hsm_client_store_insert_pki_trusted_cert);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_insert_pki_trusted_cert, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_get_pki_trusted_certs, test_hook_hsm_client_store_get_pki_trusted_certs);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_get_pki_trusted_certs, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_store_remove_pki_trusted_cert, test_hook_hsm_client_store_remove_pki_trusted_cert);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_store_remove_pki_trusted_cert, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_sign, test_hook_hsm_client_key_sign);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_sign, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_derive_and_sign, test_hook_hsm_client_key_derive_and_sign);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_derive_and_sign, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_encrypt, test_hook_hsm_client_key_encrypt);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_encrypt, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_decrypt, test_hook_hsm_client_key_decrypt);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_hsm_client_key_decrypt, 1);

            REGISTER_GLOBAL_MOCK_HOOK(mocked_hsm_client_key_destroy, test_hook_hsm_client_key_destroy);

            REGISTER_GLOBAL_MOCK_HOOK(certificate_info_create, test_hook_certificate_info_create);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(certificate_info_create, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(get_alias, test_hook_get_alias);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_alias, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(get_issuer_alias, test_hook_get_issuer_alias);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_issuer_alias, NULL);

            REGISTER_GLOBAL_MOCK_HOOK(generate_rand_buffer, test_hook_generate_rand_buffer);
            REGISTER_GLOBAL_MOCK_FAIL_RETURN(generate_rand_buffer, 1);

            (void)umocktypes_stdint_register_types();
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
         *   hsm_client_crypto_init
        */
        TEST_FUNCTION(hsm_client_crypto_init_success)
        {
            //arrange
            int status;
            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME, TEST_CA_VALIDITY));

            // act
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_init
        */
        TEST_FUNCTION(hsm_client_crypto_multi_init_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            umock_c_reset_all_calls();

            // act
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_init
        */
        TEST_FUNCTION(hsm_client_crypto_init_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME, TEST_CA_VALIDITY));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                int status;
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_crypto_init(TEST_CA_VALIDITY);

                // assert
                ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_deinit
        */
        TEST_FUNCTION(hsm_client_crypto_deinit_success)
        {
            //arrange
            (void)hsm_client_crypto_init(TEST_CA_VALIDITY);
            umock_c_reset_all_calls();
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_destroy(TEST_EDGE_STORE_NAME));

            // act
            hsm_client_crypto_deinit();

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
        }

        /**
         * Test function for API
         *   hsm_client_crypto_init
         *   hsm_client_crypto_deinit
        */
        TEST_FUNCTION(hsm_client_crypto_init_deinit_init_success)
        {
            //arrange
            int status;
            hsm_client_crypto_init(TEST_CA_VALIDITY);
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            EXPECTED_CALL(hsm_client_store_interface());
            EXPECTED_CALL(hsm_client_key_interface());
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create(TEST_EDGE_STORE_NAME, TEST_CA_VALIDITY));

            // act
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_interface
        */
        TEST_FUNCTION(hsm_client_crypto_interface_success)
        {
            //arrange

            // act
            const HSM_CLIENT_CRYPTO_INTERFACE* result = hsm_client_crypto_interface();

            // assert
            ASSERT_IS_NOT_NULL(result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_crypto_create, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_crypto_destroy, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_get_random_bytes, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_create_master_encryption_key, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_destroy_master_encryption_key, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_create_certificate, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_destroy_certificate, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_encrypt_data, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_decrypt_data, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_get_trust_bundle, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(result->hsm_client_free_buffer, "Line:" TOSTRING(__LINE__));

            //cleanup
        }

        /**
         * Test function for API
         *   hsm_client_crypto_create
        */
        TEST_FUNCTION(edge_hsm_client_crypto_create_fails_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();

            // assert
            ASSERT_IS_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_crypto_create
        */
        TEST_FUNCTION(edge_hsm_client_crypto_create_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(gballoc_calloc(1, IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open(TEST_EDGE_STORE_NAME));

            // act
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();

            // assert
            ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_create
        */
        TEST_FUNCTION(edge_hsm_client_crypto_create_negative)
        {
            //arrange
            int status;
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);

            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(gballoc_calloc(1, IGNORED_NUM_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open(TEST_EDGE_STORE_NAME));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();

                // assert
                ASSERT_IS_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_destroy
        */
        TEST_FUNCTION(edge_hsm_client_crypto_destroy_does_nothing_with_invalid_handle)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            hsm_client_crypto_destroy(NULL);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_crypto_destroy
        */
        TEST_FUNCTION(edge_hsm_client_crypto_destroy_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            hsm_client_crypto_destroy(TEST_HSM_CLIENT_HANDLE);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_crypto_destroy
        */
        TEST_FUNCTION(edge_hsm_client_crypto_destroy_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close(TEST_HSM_STORE_HANDLE));
            STRICT_EXPECTED_CALL(gballoc_free(hsm_handle));

            // act
            hsm_client_crypto_destroy(hsm_handle);

            // assert
            ASSERT_IS_NOT_NULL(hsm_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_random_bytes
        */
        TEST_FUNCTION(edge_hsm_client_get_random_bytes_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_GET_RANDOM_BYTES hsm_client_get_random_bytes = interface->hsm_client_get_random_bytes;
            unsigned char test_input[] = {'r', 'a', 'n' , 'd'};
            unsigned char test_output[] = {'r', 'a', 'n' , 'd'};
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            status = hsm_client_get_random_bytes(TEST_HSM_CLIENT_HANDLE, test_output, sizeof(test_output));

            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            for (int idx = 0; idx < (int)sizeof(test_output); idx++)
            {
                ASSERT_ARE_EQUAL(char, test_input[idx], test_output[idx], "Line:" TOSTRING(__LINE__));
            }
        }

        /**
         * Test function for API
         *   hsm_client_get_random_bytes
        */
        TEST_FUNCTION(edge_hsm_client_get_random_bytes_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_GET_RANDOM_BYTES hsm_client_get_random_bytes = interface->hsm_client_get_random_bytes;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            unsigned char test_input[] = {'r', 'a', 'n' , 'd'};
            unsigned char test_output[] = {'r', 'a', 'n' , 'd'};

            // act, assert
            status = hsm_client_get_random_bytes(NULL, test_output, sizeof(test_output));
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            for (int idx = 0; idx < (int)sizeof(test_output); idx++)
            {
                ASSERT_ARE_EQUAL(char, test_input[idx], test_output[idx], "Line:" TOSTRING(__LINE__));
            }

            status = hsm_client_get_random_bytes(hsm_handle, NULL, sizeof(test_output));
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            status = hsm_client_get_random_bytes(hsm_handle, test_output, 0);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_random_bytes
        */
        TEST_FUNCTION(edge_hsm_client_get_random_bytes_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            unsigned char test_output[] = {'r', 'a', 'n' , 'd'};
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(generate_rand_buffer(test_output, sizeof(test_output)));

            // act
            status = interface->hsm_client_get_random_bytes(hsm_handle, test_output, sizeof(test_output));

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_random_bytes
        */
        TEST_FUNCTION(edge_hsm_client_get_random_bytes_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            unsigned char test_output[] = {'r', 'a', 'n' , 'd'};
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(generate_rand_buffer(test_output, sizeof(test_output)));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = interface->hsm_client_get_random_bytes(hsm_handle, test_output, sizeof(test_output));

                // assert
                ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_create_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_create_master_encryption_key_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY hsm_client_create_master_encryption_key;
            hsm_client_create_master_encryption_key = interface->hsm_client_create_master_encryption_key;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            status = hsm_client_create_master_encryption_key(TEST_HSM_CLIENT_HANDLE);

            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_create_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_create_master_encryption_key_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY hsm_client_create_master_encryption_key;
            hsm_client_create_master_encryption_key = interface->hsm_client_create_master_encryption_key;

            // act, assert
            status = hsm_client_create_master_encryption_key(NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_create_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_create_master_encryption_key_success)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            HSM_CLIENT_CREATE_MASTER_ENCRYPTION_KEY hsm_client_create_master_encryption_key;
            hsm_client_create_master_encryption_key = interface->hsm_client_create_master_encryption_key;

            // act, assert
            status = hsm_client_create_master_encryption_key(hsm_handle);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_destroy_master_encryption_key_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            int status;
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY hsm_client_destroy_master_encryption_key;
            hsm_client_destroy_master_encryption_key = interface->hsm_client_destroy_master_encryption_key;
            hsm_client_crypto_deinit();

            umock_c_reset_all_calls();

            // act
            status = hsm_client_destroy_master_encryption_key(TEST_HSM_CLIENT_HANDLE);

            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_destroy_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_destroy_master_encryption_key_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY hsm_client_destroy_master_encryption_key;
            hsm_client_destroy_master_encryption_key = interface->hsm_client_destroy_master_encryption_key;

            // act, assert
            status = hsm_client_destroy_master_encryption_key(NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_master_encryption_key
        */
        TEST_FUNCTION(edge_hsm_client_destroy_master_encryption_key_success)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            HSM_CLIENT_DESTROY_MASTER_ENCRYPTION_KEY hsm_client_destroy_master_encryption_key;
            hsm_client_destroy_master_encryption_key = interface->hsm_client_destroy_master_encryption_key;

            // act, assert
            status = hsm_client_destroy_master_encryption_key(hsm_handle);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_create_certificate
        */
        TEST_FUNCTION(edge_hsm_client_create_certificate_cert_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate = interface->hsm_client_create_certificate;
            CERT_INFO_HANDLE cert_info_handle;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            cert_info_handle = hsm_client_create_certificate(TEST_HSM_CLIENT_HANDLE, TEST_CERT_PROPS_HANDLE);

            // assert
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_create_certificate
        */
        TEST_FUNCTION(edge_hsm_client_create_certificate_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate = interface->hsm_client_create_certificate;
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            // act, assert
            cert_info_handle = hsm_client_create_certificate(NULL, TEST_CERT_PROPS_HANDLE);
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));

            // act, assert
            cert_info_handle = hsm_client_create_certificate(TEST_HSM_CLIENT_HANDLE, NULL);
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_create_certificate
        */
        TEST_FUNCTION(edge_hsm_client_create_certificate_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate = interface->hsm_client_create_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(get_alias(TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(get_issuer_alias(TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create_pki_cert(IGNORED_PTR_ARG, TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            // act
            cert_info_handle = hsm_client_create_certificate(hsm_handle, TEST_CERT_PROPS_HANDLE);

            // assert
            ASSERT_ARE_EQUAL(void_ptr, TEST_CERT_INFO_HANDLE, cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_create_certificate
        */
        TEST_FUNCTION(edge_hsm_client_create_certificate_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CREATE_CERTIFICATE hsm_client_create_certificate = interface->hsm_client_create_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(get_alias(TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(get_issuer_alias(TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_create_pki_cert(IGNORED_PTR_ARG, TEST_CERT_PROPS_HANDLE));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                cert_info_handle = hsm_client_create_certificate(hsm_handle, TEST_CERT_PROPS_HANDLE);

                // assert
                ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_trust_bundle
        */
        TEST_FUNCTION(edge_hsm_client_get_trust_bundle_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_GET_TRUST_BUNDLE hsm_client_get_trust_bundle = interface->hsm_client_get_trust_bundle;
            CERT_INFO_HANDLE cert_info_handle;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            cert_info_handle = hsm_client_get_trust_bundle(TEST_HSM_CLIENT_HANDLE);

            // assert
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_get_trust_bundle
        */
        TEST_FUNCTION(edge_hsm_client_get_trust_bundle_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_GET_TRUST_BUNDLE hsm_client_get_trust_bundle = interface->hsm_client_get_trust_bundle;
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            // act, assert
            cert_info_handle = hsm_client_get_trust_bundle(NULL);
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_trust_bundle
        */
        TEST_FUNCTION(edge_hsm_client_get_trust_bundle_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_GET_TRUST_BUNDLE hsm_client_get_trust_bundle = interface->hsm_client_get_trust_bundle;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_trusted_certs(IGNORED_PTR_ARG));

            // act
            cert_info_handle = hsm_client_get_trust_bundle(hsm_handle);

            // assert
            ASSERT_ARE_EQUAL(void_ptr, TEST_TRUST_BUNDLE_CERT_INFO_HANDLE, cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_get_trust_bundle
        */
        TEST_FUNCTION(edge_hsm_client_get_trust_bundle_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_GET_TRUST_BUNDLE hsm_client_get_trust_bundle = interface->hsm_client_get_trust_bundle;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_trusted_certs(IGNORED_PTR_ARG));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                cert_info_handle = hsm_client_get_trust_bundle(hsm_handle);

                // assert
                ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_certificate
        */
        TEST_FUNCTION(edge_hsm_client_destroy_certificate_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate = interface->hsm_client_destroy_certificate;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            hsm_client_destroy_certificate(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_destroy_certificate
        */
        TEST_FUNCTION(edge_hsm_client_destroy_certificate_invalid_param_1_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate = interface->hsm_client_destroy_certificate;
            umock_c_reset_all_calls();

            // act, assert
            hsm_client_destroy_certificate(TEST_HSM_CLIENT_HANDLE, NULL);
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_certificate
        */
        TEST_FUNCTION(edge_hsm_client_destroy_certificate_invalid_param_2_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate = interface->hsm_client_destroy_certificate;
            umock_c_reset_all_calls();

            // act, assert
            hsm_client_destroy_certificate(NULL, TEST_ALIAS_STRING);
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_certificate
        */
        TEST_FUNCTION(edge_hsm_client_destroy_certificate_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate = interface->hsm_client_destroy_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_remove_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            // act
            hsm_client_destroy_certificate(hsm_handle, TEST_ALIAS_STRING);

            // assert
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_destroy_certificate
        */
        TEST_FUNCTION(edge_hsm_client_destroy_certificate_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_DESTROY_CERTIFICATE hsm_client_destroy_certificate = interface->hsm_client_destroy_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_remove_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                hsm_client_destroy_certificate(hsm_handle, TEST_ALIAS_STRING);

                // assert
                ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_get_certificate
        */
        TEST_FUNCTION(edge_hsm_client_get_certificate_cert_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CRYPTO_GET_CERTIFICATE hsm_client_crypto_get_certificate = interface->hsm_client_crypto_get_certificate;
            CERT_INFO_HANDLE cert_info_handle;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            cert_info_handle = hsm_client_crypto_get_certificate(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING);

            // assert
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_crypto_get_certificate
        */
        TEST_FUNCTION(edge_hsm_client_crypto_get_certificate_invalid_param_validation)
        {
            //arrange
            int status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CRYPTO_GET_CERTIFICATE hsm_client_crypto_get_certificate = interface->hsm_client_crypto_get_certificate;
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            // act, assert
            cert_info_handle = hsm_client_crypto_get_certificate(NULL, TEST_ALIAS_STRING);
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));

            // act, assert
            cert_info_handle = hsm_client_crypto_get_certificate(TEST_HSM_CLIENT_HANDLE, NULL);
            ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_get_certificate
        */
        TEST_FUNCTION(edge_hsm_client_crypto_get_certificate_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CRYPTO_GET_CERTIFICATE hsm_client_crypto_get_certificate = interface->hsm_client_crypto_get_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            // act
            cert_info_handle = hsm_client_crypto_get_certificate(hsm_handle, TEST_ALIAS_STRING);

            // assert
            ASSERT_ARE_EQUAL(void_ptr, TEST_CERT_INFO_HANDLE, cert_info_handle, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_get_certificate
        */
        TEST_FUNCTION(edge_hsm_client_crypto_get_certificate_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CRYPTO_GET_CERTIFICATE hsm_client_crypto_get_certificate = interface->hsm_client_crypto_get_certificate;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            CERT_INFO_HANDLE cert_info_handle;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_get_pki_cert(IGNORED_PTR_ARG, TEST_ALIAS_STRING));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                cert_info_handle = hsm_client_crypto_get_certificate(hsm_handle, TEST_ALIAS_STRING);

                // assert
                ASSERT_IS_NULL(cert_info_handle, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_sign_with_private_key
        */
        TEST_FUNCTION(edge_hsm_client_crypto_sign_with_private_key_does_nothing_when_crypto_not_initialized)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY hsm_client_crypto_sign_with_private_key = interface->hsm_client_crypto_sign_with_private_key;
            unsigned char *digest;
            size_t digest_size;
            int status;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, &digest, &digest_size);

            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));
        }

        /**
         * Test function for API
         *   hsm_client_crypto_sign_with_private_key
        */
        TEST_FUNCTION(edge_hsm_client_crypto_sign_with_private_key_invalid_param_validation)
        {
            //arrange
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY hsm_client_crypto_sign_with_private_key = interface->hsm_client_crypto_sign_with_private_key;
            unsigned char *digest;
            size_t digest_size;
            int status;
            hsm_client_crypto_deinit();
            umock_c_reset_all_calls();

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(NULL, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, NULL, TEST_TBS, TEST_TBS_SIZE, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING, NULL, TEST_TBS_SIZE, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING, TEST_TBS, 0, &digest, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, NULL, &digest_size);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act, assert
            status = hsm_client_crypto_sign_with_private_key(TEST_HSM_CLIENT_HANDLE, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, &digest, NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_sign_with_private_key
        */
        TEST_FUNCTION(edge_hsm_client_crypto_sign_with_private_key_success)
        {
            //arrange
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY hsm_client_crypto_sign_with_private_key = interface->hsm_client_crypto_sign_with_private_key;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            unsigned char *digest = NULL;
            size_t digest_size = 0;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, HSM_KEY_ASYMMETRIC_PRIVATE_KEY, TEST_ALIAS_STRING));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_sign(TEST_KEY_HANDLE, TEST_TBS, TEST_TBS_SIZE, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            // act
            status = hsm_client_crypto_sign_with_private_key(hsm_handle, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, &digest, &digest_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(void_ptr, TEST_DIGEST_BUFFER, digest, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, TEST_DIGEST_BUFFER_SIZE, digest_size, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
        }

        /**
         * Test function for API
         *   hsm_client_crypto_sign_with_private_key
        */
        TEST_FUNCTION(edge_hsm_client_crypto_sign_with_private_key_negative)
        {
            //arrange
            int test_result = umock_c_negative_tests_init();
            ASSERT_ARE_EQUAL(int, 0, test_result);
            int status;
            status = hsm_client_crypto_init(TEST_CA_VALIDITY);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            const HSM_CLIENT_CRYPTO_INTERFACE* interface = hsm_client_crypto_interface();
            HSM_CLIENT_CREATE hsm_client_crypto_create = interface->hsm_client_crypto_create;
            HSM_CLIENT_DESTROY hsm_client_crypto_destroy = interface->hsm_client_crypto_destroy;
            HSM_CLIENT_CRYPTO_SIGN_WITH_PRIVATE_KEY hsm_client_crypto_sign_with_private_key = interface->hsm_client_crypto_sign_with_private_key;
            HSM_CLIENT_HANDLE hsm_handle = hsm_client_crypto_create();
            unsigned char *digest = NULL;
            size_t digest_size = 0;
            umock_c_reset_all_calls();

            STRICT_EXPECTED_CALL(mocked_hsm_client_store_open_key(TEST_HSM_STORE_HANDLE, HSM_KEY_ASYMMETRIC_PRIVATE_KEY, TEST_ALIAS_STRING));
            STRICT_EXPECTED_CALL(mocked_hsm_client_key_sign(TEST_KEY_HANDLE, TEST_TBS, TEST_TBS_SIZE, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
            STRICT_EXPECTED_CALL(mocked_hsm_client_store_close_key(TEST_HSM_STORE_HANDLE, TEST_KEY_HANDLE));

            umock_c_negative_tests_snapshot();

            for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
            {
                umock_c_negative_tests_reset();
                umock_c_negative_tests_fail_call(i);

                // act
                status = hsm_client_crypto_sign_with_private_key(hsm_handle, TEST_ALIAS_STRING, TEST_TBS, TEST_TBS_SIZE, &digest, &digest_size);

                // assert
                ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            }

            //cleanup
            hsm_client_crypto_destroy(hsm_handle);
            hsm_client_crypto_deinit();
            umock_c_negative_tests_deinit();
        }

END_TEST_SUITE(edge_hsm_crypto_unittests)
