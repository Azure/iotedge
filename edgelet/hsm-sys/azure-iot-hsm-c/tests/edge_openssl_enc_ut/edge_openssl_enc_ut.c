// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <limits.h>
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
#include <openssl/bio.h>
#include <openssl/evp.h>
#include <openssl/rand.h>

//#############################################################################
// Declare and enable MOCK definitions
//#############################################################################

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "edge_openssl_common.h"

MOCKABLE_FUNCTION(, int, RAND_bytes, unsigned char*, buf, int, num);
MOCKABLE_FUNCTION(, EVP_CIPHER_CTX*, EVP_CIPHER_CTX_new);
MOCKABLE_FUNCTION(, int, EVP_EncryptInit_ex, EVP_CIPHER_CTX*, ctx, const EVP_CIPHER*, type,
                    ENGINE*, impl, const unsigned char*, key, const unsigned char*, iv);
MOCKABLE_FUNCTION(, int, EVP_CIPHER_CTX_ctrl, EVP_CIPHER_CTX*, ctx, int, type, int, arg, void*, ptr);
MOCKABLE_FUNCTION(, int, EVP_EncryptUpdate, EVP_CIPHER_CTX*, ctx, unsigned char*, out,
                    int*, outl, const unsigned char*, in, int, inl);
MOCKABLE_FUNCTION(, int, EVP_EncryptFinal_ex, EVP_CIPHER_CTX*, ctx, unsigned char*, out, int*, outl);
MOCKABLE_FUNCTION(, void, EVP_CIPHER_CTX_free, EVP_CIPHER_CTX*, ctx);
MOCKABLE_FUNCTION(, int, EVP_DecryptInit_ex, EVP_CIPHER_CTX*, ctx, const EVP_CIPHER*, type,
                    ENGINE*, impl, const unsigned char*, key, const unsigned char*, iv);
MOCKABLE_FUNCTION(, int, EVP_DecryptUpdate, EVP_CIPHER_CTX*, ctx, unsigned char*, out,
                    int*, outl, const unsigned char*, in, int, inl);
MOCKABLE_FUNCTION(, int, EVP_DecryptFinal_ex, EVP_CIPHER_CTX*, ctx, unsigned char*, outm, int*, outl);
MOCKABLE_FUNCTION(, const EVP_CIPHER*, EVP_aes_256_gcm);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

#define TEST_EVP_CIPHER_CTX (EVP_CIPHER_CTX*)0x1000

#define ENCRYPTION_KEY_SIZE 32
static unsigned char TEST_KEY[ENCRYPTION_KEY_SIZE] = {
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
    0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
    0, 1
};
static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

#define TEST_VERSION_SIZE 1
#define TEST_TAG_SIZE 16
#define TEST_PLAINTEXT_SIZE 9

#define TEST_CIPHERTEXT_HEADER_SIZE (TEST_TAG_SIZE + TEST_VERSION_SIZE)
#define TEST_CIPHERTEXT_SIZE (TEST_CIPHERTEXT_HEADER_SIZE + TEST_PLAINTEXT_SIZE)

#define TEST_VERSION_OFFSET 0
#define TEST_TAG_OFFSET (TEST_VERSION_OFFSET + TEST_VERSION_SIZE)
#define TEST_CIPHERTEXT_OFFSET (TEST_TAG_OFFSET + TEST_TAG_SIZE)


static unsigned char TEST_PLAINTEXT[TEST_PLAINTEXT_SIZE] = {'P', 'L', 'A', 'I', 'N', 'T', 'E', 'X', 'T'};
static unsigned char TEST_CIPHERTEXT[TEST_CIPHERTEXT_SIZE] = {
    1, //must be 1 for v1 encryption scheme
    // tag bytes length must be TEST_TAG_SIZE
    '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
    '0', '1', '2', '3', '4', '5',
    // ciphertext length equals plaintext length
    'C', 'I', 'P', 'H', 'E', 'R', 'T', 'E', 'X'
};

static unsigned char TEST_IDENTITY[] = {'I', 'D', '1'};
static size_t TEST_IDENTITY_SIZE = sizeof(TEST_IDENTITY);
static unsigned char TEST_IV[] = "IV";
static size_t TEST_IV_SIZE = sizeof(TEST_IV);
static const EVP_CIPHER* TEST_EVP_CIPHER = (EVP_CIPHER*)(0x2000);

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

static void test_hook_initialize_openssl(void)
{

}

static int test_hook_RAND_bytes(unsigned char *buf, int num)
{
    int i;
    for (i = 0; i < num; i++)
    {
        buf[i] = i;
    }
    return 1;
}

static EVP_CIPHER_CTX* test_hook_EVP_CIPHER_CTX_new(void)
{
    return TEST_EVP_CIPHER_CTX;
}

static int test_hook_EVP_EncryptInit_ex
(
    EVP_CIPHER_CTX *ctx,
    const EVP_CIPHER *type,
    ENGINE *impl,
    const unsigned char *key,
    const unsigned char *iv
)
{
    return 1;
}

static int test_hook_EVP_EncryptUpdate
(
    EVP_CIPHER_CTX *ctx,
    unsigned char *out,
    int *outl,
    const unsigned char *in,
    int inl
)
{
    *outl = inl;
    return 1;
}

static int test_hook_EVP_CIPHER_CTX_ctrl
(
    EVP_CIPHER_CTX *ctx,
    int type,
    int arg,
    void *ptr
)
{
    return 1;
}

static int test_hook_EVP_EncryptFinal_ex
(
    EVP_CIPHER_CTX *ctx,
    unsigned char *out,
    int *outl
)
{
    *outl = 0;
    return 1;
}

static void test_hook_EVP_CIPHER_CTX_free(EVP_CIPHER_CTX *ctx)
{

}

static const EVP_CIPHER* test_hook_EVP_aes_256_gcm(void)
{
    return TEST_EVP_CIPHER;
}

static int test_hook_EVP_DecryptInit_ex
(
    EVP_CIPHER_CTX *ctx,
    const EVP_CIPHER *type,
    ENGINE *impl,
    const unsigned char *key,
    const unsigned char *iv
)
{
    return 1;
}

int test_hook_EVP_DecryptUpdate
(
    EVP_CIPHER_CTX *ctx,
    unsigned char *out,
    int *outl,
    const unsigned char *in,
    int inl
)
{
    *outl = inl;
    return 1;
}

int test_hook_EVP_DecryptFinal_ex
(
    EVP_CIPHER_CTX *ctx,
    unsigned char *outm,
    int *outl
)
{
    *outl = 0;
    return 1;
}

//#############################################################################
// Test helpers
//#############################################################################
static uint64_t test_stack_helper_encrypt(void)
{
    uint64_t failed_function_bitmask = 0;
    size_t i = 0;

	EXPECTED_CALL(initialize_openssl());
	i++;
    STRICT_EXPECTED_CALL(gballoc_malloc(TEST_CIPHERTEXT_SIZE));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_new());
    i++;
    STRICT_EXPECTED_CALL(EVP_aes_256_gcm());
    i++;
    STRICT_EXPECTED_CALL(EVP_EncryptInit_ex(TEST_EVP_CIPHER_CTX, TEST_EVP_CIPHER, NULL, NULL, NULL));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_ctrl(TEST_EVP_CIPHER_CTX, EVP_CTRL_GCM_SET_IVLEN, (int)TEST_IV_SIZE, NULL));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_EncryptInit_ex(TEST_EVP_CIPHER_CTX, NULL, NULL, IGNORED_PTR_ARG, TEST_IV));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_EncryptUpdate(TEST_EVP_CIPHER_CTX, NULL, IGNORED_PTR_ARG, TEST_IDENTITY, (int)TEST_IDENTITY_SIZE));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_EncryptUpdate(TEST_EVP_CIPHER_CTX, IGNORED_PTR_ARG, IGNORED_PTR_ARG, TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_EncryptFinal_ex(TEST_EVP_CIPHER_CTX, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_ctrl(TEST_EVP_CIPHER_CTX, EVP_CTRL_GCM_GET_TAG, TEST_TAG_SIZE, IGNORED_PTR_ARG));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_free(TEST_EVP_CIPHER_CTX));

    return failed_function_bitmask;
}

static uint64_t test_stack_helper_decrypt(void)
{
    uint64_t failed_function_bitmask = 0;
    size_t i = 0;

	EXPECTED_CALL(initialize_openssl());
	i++;
    STRICT_EXPECTED_CALL(gballoc_malloc(TEST_CIPHERTEXT_SIZE));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_new());
    i++;
    STRICT_EXPECTED_CALL(EVP_aes_256_gcm());
    i++;
    STRICT_EXPECTED_CALL(EVP_DecryptInit_ex(TEST_EVP_CIPHER_CTX, TEST_EVP_CIPHER, NULL, NULL, NULL));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_ctrl(TEST_EVP_CIPHER_CTX, EVP_CTRL_GCM_SET_IVLEN, (int)TEST_IV_SIZE, NULL));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_DecryptInit_ex(TEST_EVP_CIPHER_CTX, NULL, NULL, IGNORED_PTR_ARG, TEST_IV));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_DecryptUpdate(TEST_EVP_CIPHER_CTX, NULL, IGNORED_PTR_ARG, TEST_IDENTITY, (int)TEST_IDENTITY_SIZE));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_DecryptUpdate(TEST_EVP_CIPHER_CTX, IGNORED_PTR_ARG, IGNORED_PTR_ARG, TEST_CIPHERTEXT + TEST_CIPHERTEXT_OFFSET, TEST_CIPHERTEXT_SIZE - TEST_CIPHERTEXT_OFFSET));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_ctrl(TEST_EVP_CIPHER_CTX, EVP_CTRL_GCM_SET_TAG, TEST_TAG_SIZE, IGNORED_PTR_ARG));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_DecryptFinal_ex(TEST_EVP_CIPHER_CTX, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
    failed_function_bitmask |= ((uint64_t)1 << i++);
    STRICT_EXPECTED_CALL(EVP_CIPHER_CTX_free(TEST_EVP_CIPHER_CTX));

    return failed_function_bitmask;
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_encryption_unittests)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);

        REGISTER_UMOCK_ALIAS_TYPE(KEY_HANDLE, void*);
        //REGISTER_UMOCK_ALIAS_TYPE(EVP_CIPHER_CTX, void*);
        //REGISTER_UMOCK_ALIAS_TYPE(EVP_CIPHER, void*);

        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, test_hook_gballoc_calloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, test_hook_gballoc_realloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);

        REGISTER_GLOBAL_MOCK_HOOK(initialize_openssl, test_hook_initialize_openssl);

        REGISTER_GLOBAL_MOCK_HOOK(RAND_bytes, test_hook_RAND_bytes);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(RAND_bytes, -1);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_CIPHER_CTX_new, test_hook_EVP_CIPHER_CTX_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_CIPHER_CTX_new, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_EncryptInit_ex, test_hook_EVP_EncryptInit_ex);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_EncryptInit_ex, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_EncryptUpdate, test_hook_EVP_EncryptUpdate);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_EncryptUpdate, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_CIPHER_CTX_ctrl, test_hook_EVP_CIPHER_CTX_ctrl);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_CIPHER_CTX_ctrl, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_EncryptFinal_ex, test_hook_EVP_EncryptFinal_ex);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_EncryptFinal_ex, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_CIPHER_CTX_free, test_hook_EVP_CIPHER_CTX_free);
        REGISTER_GLOBAL_MOCK_HOOK(EVP_aes_256_gcm, test_hook_EVP_aes_256_gcm);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_DecryptInit_ex, test_hook_EVP_DecryptInit_ex);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_DecryptInit_ex, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_DecryptUpdate, test_hook_EVP_DecryptUpdate);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_DecryptUpdate, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_DecryptFinal_ex, test_hook_EVP_DecryptFinal_ex);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_DecryptFinal_ex, 0);
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
     *   generate_encryption_key
    */
    TEST_FUNCTION(generate_encryption_key_invalid_params)
    {
        // arrange
        int status;
        unsigned char *key;
        size_t key_size;

        // act, assert
        key_size = 10;
        status = generate_encryption_key(NULL, &key_size);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, key_size, "Line:" TOSTRING(__LINE__));

        key = (unsigned char*)0x1000;
        status = generate_encryption_key(&key, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(key, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_encryption_key
    */
    TEST_FUNCTION(generate_encryption_key_success)
    {
        // arrange
        unsigned char *key = NULL;
        size_t key_size;
        int status;

        EXPECTED_CALL(initialize_openssl());
        STRICT_EXPECTED_CALL(gballoc_malloc(ENCRYPTION_KEY_SIZE));
        STRICT_EXPECTED_CALL(RAND_bytes(IGNORED_PTR_ARG, ENCRYPTION_KEY_SIZE));

        // act
        status = generate_encryption_key(&key, &key_size);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(key, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, ENCRYPTION_KEY_SIZE, key_size, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        gballoc_free(key);
    }

    /**
     * Test function for API
     *   generate_encryption_key
    */
    TEST_FUNCTION(generate_encryption_key_negative)
    {
        //arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        EXPECTED_CALL(initialize_openssl());
        STRICT_EXPECTED_CALL(gballoc_malloc(ENCRYPTION_KEY_SIZE));
        STRICT_EXPECTED_CALL(RAND_bytes(IGNORED_PTR_ARG, ENCRYPTION_KEY_SIZE));

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            unsigned char *key = NULL;
            size_t key_size;
            int status;

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

			if (i != 0)
			{
               // act
               status = generate_encryption_key(&key, &key_size);

               // assert
               ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
               ASSERT_IS_NULL_WITH_MSG(key, "Line:" TOSTRING(__LINE__));
               ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, key_size, "Line:" TOSTRING(__LINE__));			
			}

        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   create_encryption_key
    */
    TEST_FUNCTION(create_encryption_key_invalid_params)
    {
        // arrange
        KEY_HANDLE key_handle;
        unsigned char key[ENCRYPTION_KEY_SIZE];

        // act, assert
        key_handle = create_encryption_key(NULL, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));

        key_handle = create_encryption_key(key, 0);
        ASSERT_IS_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));

        key_handle = create_encryption_key(key, ENCRYPTION_KEY_SIZE - 1);
        ASSERT_IS_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));

        key_handle = create_encryption_key(key, ENCRYPTION_KEY_SIZE + 1);
        ASSERT_IS_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   create_encryption_key
    */
    TEST_FUNCTION(create_encryption_key_success)
    {
        // arrange
        KEY_HANDLE key_handle;

        EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(gballoc_malloc(ENCRYPTION_KEY_SIZE));

        // act
        key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);

        // assert
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   create_encryption_key
    */
    TEST_FUNCTION(create_encryption_key_negative)
    {
        //arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(gballoc_malloc(ENCRYPTION_KEY_SIZE));

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            KEY_HANDLE key_handle;

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            // act
            key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);

            // assert
            ASSERT_IS_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   key_destroy
    */
    TEST_FUNCTION(key_destroy_success)
    {
        // arrange
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        umock_c_reset_all_calls();

        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(gballoc_free(key_handle));

        // act
        key_destroy(key_handle);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   key_encrypt
    */
    TEST_FUNCTION(key_encrypt_invalid_params)
    {
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER pt = {TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ct;
        unsigned char TEST_DATA[] = {1, 2, 3, 4};
        SIZED_BUFFER inv1 = {NULL, 4};
        SIZED_BUFFER inv2 = {TEST_DATA, 0};
        SIZED_BUFFER inv_pt_size = {TEST_DATA, 0};
        int status;

        // act, assert
        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, NULL, &pt, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &inv1, &pt, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &inv2, &pt, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, NULL, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &inv1, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        inv_pt_size.size = 0;
        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &inv_pt_size, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        inv_pt_size.size = INT_MAX - TEST_CIPHERTEXT_HEADER_SIZE + 1;
        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &inv_pt_size, &iv, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &pt, NULL, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &pt, &inv1, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &pt, &inv2, &ct);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));

        ct.size = 10; ct.buffer = (unsigned char*)0xA000;
        status = key_encrypt(key_handle, &id, &pt, &iv, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   key_encrypt
    */
    TEST_FUNCTION(key_encrypt_success)
    {
        // arrange
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER pt = {TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ct = {NULL, 0};
        int status;
        umock_c_reset_all_calls();

        (void)test_stack_helper_encrypt();

        // act
        status = key_encrypt(key_handle, &id, &pt, &iv, &ct);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, TEST_CIPHERTEXT_HEADER_SIZE+TEST_PLAINTEXT_SIZE, ct.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ct.buffer);
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   key_encrypt
    */
    TEST_FUNCTION(key_encrypt_negative)
    {
        //arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER pt = {TEST_PLAINTEXT, TEST_PLAINTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ct = {NULL, 0};
        umock_c_reset_all_calls();

        uint64_t failed_function_bitmask = test_stack_helper_encrypt();
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);
            if (failed_function_bitmask & ((uint64_t)1 << i))
            {
                // act
                int status = key_encrypt(key_handle, &id, &pt, &iv, &ct);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
                ASSERT_IS_NULL_WITH_MSG(ct.buffer, "Line:" TOSTRING(__LINE__));
                ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, ct.size, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        key_destroy(key_handle);
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   key_decrypt
    */
    TEST_FUNCTION(key_decrypt_invalid_params)
    {
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER ct = {TEST_CIPHERTEXT, TEST_CIPHERTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER pt;
        unsigned char TEST_DATA[] = {1, 2, 3, 4};
        SIZED_BUFFER inv1 = {NULL, 4};
        SIZED_BUFFER inv2 = {TEST_DATA, 0};
        SIZED_BUFFER inv_ct_size = {TEST_CIPHERTEXT, 0};
        SIZED_BUFFER inv_ct_version = {TEST_CIPHERTEXT, TEST_CIPHERTEXT_SIZE};
        int status;

        // act, assert
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, NULL, &ct, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &inv1, &ct, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &inv2, &ct, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, NULL, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv1, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_size.size = 0;
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_size, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_size.size = TEST_CIPHERTEXT_HEADER_SIZE - 1;
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_size, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_size.size = TEST_CIPHERTEXT_HEADER_SIZE;
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_size, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_size.size = ((size_t)INT_MAX + 1);
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_size, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_version.buffer[0] = 0;
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_version, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        inv_ct_version.buffer[0] = 2;
        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &inv_ct_version, &iv, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &ct, NULL, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &ct, &inv1, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &ct, &inv2, &pt);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));

        pt.size = 10; pt.buffer = (unsigned char*)0xA000;
        status = key_decrypt(key_handle, &id, &ct, &iv, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   key_decrypt
    */
    TEST_FUNCTION(key_decrypt_success)
    {
        // arrange
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER ct = {TEST_CIPHERTEXT, TEST_CIPHERTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER pt = {NULL, 0};
        int status;
        umock_c_reset_all_calls();

        (void)test_stack_helper_decrypt();

        // act
        status = key_decrypt(key_handle, &id, &ct, &iv, &pt);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, TEST_CIPHERTEXT_SIZE-TEST_CIPHERTEXT_HEADER_SIZE, pt.size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NOT_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
        free(pt.buffer);
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   key_decrypt
    */
    TEST_FUNCTION(key_decrypt_negative)
    {
        //arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_IDENTITY, TEST_IDENTITY_SIZE};
        SIZED_BUFFER ct = {TEST_CIPHERTEXT, TEST_CIPHERTEXT_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER pt = {NULL, 0};
        umock_c_reset_all_calls();

        uint64_t failed_function_bitmask = test_stack_helper_decrypt();
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);
            if (failed_function_bitmask & ((uint64_t)1 << i))
            {
                // act
                int status = key_decrypt(key_handle, &id, &ct, &iv, &pt);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
                ASSERT_IS_NULL_WITH_MSG(pt.buffer, "Line:" TOSTRING(__LINE__));
                ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, pt.size, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        key_destroy(key_handle);
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   key_sign
    */
    TEST_FUNCTION(key_sign_unsupported)
    {
        // arrange
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        unsigned char TBS[] = "data";
        unsigned char *output = (unsigned char*)0x1000;
        size_t output_size = 1234;

        // act
        int status = key_sign(key_handle, TBS, sizeof(TBS), &output, &output_size);

        // arrange
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(output, "Line:" TOSTRING(__LINE__));

        //cleanup
        key_destroy(key_handle);
    }

    /**
     * Test function for API
     *   key_derive_and_sign
    */
    TEST_FUNCTION(key_derive_and_sign_unsupported)
    {
        // arrange
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, ENCRYPTION_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        unsigned char TBS[] = "data";
        unsigned char *output = (unsigned char*)0x1000;
        size_t output_size = 1234;

        // act
        int status = key_derive_and_sign(key_handle, TBS, sizeof(TBS),
                                         TEST_IDENTITY, TEST_IDENTITY_SIZE,
                                         &output, &output_size);

        // arrange
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(output, "Line:" TOSTRING(__LINE__));

        //cleanup
        key_destroy(key_handle);
    }

END_TEST_SUITE(edge_openssl_encryption_unittests)
