// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "azure_c_shared_utility/gballoc.h"
#include "testrunnerswitcher.h"
#include "hsm_client_store.h"
#include "hsm_log.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

// Test data below is NIST data used for the FIPS self test
// source: https://github.com/openssl/openssl/blob/master/demos/evp/aesgcm.c

static unsigned char TEST_KEY[] = {
    0xee, 0xbc, 0x1f, 0x57, 0x48, 0x7f, 0x51, 0x92, 0x1c, 0x04, 0x65, 0x66,
    0x5f, 0x8a, 0xe6, 0xd1, 0x65, 0x8b, 0xb2, 0x6d, 0xe6, 0xf8, 0xa0, 0x69,
    0xa3, 0x52, 0x02, 0x93, 0xa5, 0x72, 0x07, 0x8f
};
static size_t TEST_KEY_SIZE = sizeof(TEST_KEY);

static unsigned char TEST_ID_1[] = {
    0x4d, 0x23, 0xc3, 0xce, 0xc3, 0x34, 0xb4, 0x9b, 0xdb, 0x37, 0x0c, 0x43,
    0x7f, 0xec, 0x78, 0xde
};
static size_t TEST_ID_1_SIZE = sizeof(TEST_ID_1);

// changed 1 byte from 0x4d to 4e
static unsigned char TEST_ID_2[] = {
    0x4e, 0x23, 0xc3, 0xce, 0xc3, 0x34, 0xb4, 0x9b, 0xdb, 0x37, 0x0c, 0x43,
    0x7f, 0xec, 0x78, 0xde
};
static size_t TEST_ID_2_SIZE = sizeof(TEST_ID_2);

static char TEST_STRING[] = {
    0xf5, 0x6e, 0x87, 0x05, 0x5b, 0xc3, 0x2d, 0x0e, 0xeb, 0x31, 0xb2, 0xea,
    0xcc, 0x2b, 0xf2, 0xa5
};
static size_t TEST_STRING_SIZE = sizeof(TEST_STRING);

static char TEST_IV[] = { 0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84 };
size_t TEST_IV_SIZE = sizeof(TEST_IV);

static char TEST_IV_LARGE[] = {
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0, 0x66, 0x84,
    0x99, 0xaa, 0x3e, 0x68, 0xed, 0x81, 0x73, 0xa0, 0xee, 0xd0,
};
size_t TEST_IV_LARGE_SIZE = sizeof(TEST_IV_LARGE);

#define TEST_TAG_SIZE 16
static const unsigned char TEST_TAG[TEST_TAG_SIZE] = {
    0x67, 0xba, 0x05, 0x10, 0x26, 0x2a, 0xe4, 0x87, 0xd7, 0x37, 0xee, 0x62,
    0x98, 0xf7, 0x7e, 0x0c
};

static const unsigned char TEST_CIPHER[] = {
    0xf7, 0x26, 0x44, 0x13, 0xa8, 0x4c, 0x0e, 0x7c, 0xd5, 0x36, 0x86, 0x7e,
    0xb9, 0xf2, 0x17, 0x36
};
size_t TEST_CIPHER_SIZE = sizeof(TEST_CIPHER);

#define ENCRYPTION_KEY_SIZE 32

#define TEST_VERSION 1
#define TEST_VERSION_SIZE 1
#define TEST_CIPHERTEXT_HEADER_SIZE (TEST_TAG_SIZE + TEST_VERSION_SIZE)

#define TEST_VERSION_OFFSET 0
#define TEST_TAG_OFFSET (TEST_VERSION_OFFSET + TEST_VERSION_SIZE)
#define TEST_CIPHERTEXT_OFFSET (TEST_TAG_OFFSET + (TEST_TAG_SIZE))

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_homedir(void)
{
#if defined(TESTONLY_IOTEDGE_HOMEDIR)
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        errno_t status = _putenv_s("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR);
    #else
        int status = setenv("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR, 1);
    #endif
    printf("IoT Edge home dir set to %s\n", TESTONLY_IOTEDGE_HOMEDIR);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
#else
    #error "Could not find symbol TESTONLY_IOTEDGE_HOMEDIR"
#endif
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_enc_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(test_enc_dec_basic_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};

        // act, assert (encrypt)
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, (TEST_STRING_SIZE + TEST_CIPHERTEXT_HEADER_SIZE), ciphertext_result.size, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char, TEST_VERSION, ciphertext_result.buffer[0], "Line:" TOSTRING(__LINE__));
        status = memcmp(ciphertext_result.buffer + TEST_TAG_OFFSET, TEST_TAG, TEST_TAG_SIZE);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = memcmp(ciphertext_result.buffer + TEST_CIPHERTEXT_OFFSET, TEST_CIPHER, TEST_CIPHER_SIZE);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act, assert (decrypt)
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        status = memcmp(plaintext_result.buffer, TEST_STRING, TEST_STRING_SIZE);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        free(plaintext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_with_an_id_and_dec_with_a_different_id_fails)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER id2 = {TEST_ID_2, TEST_ID_2_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = key_decrypt(key_handle, &id2, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, plaintext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_dec_corrupted_tag_after_enc_fails)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        // corrupt tag bits
        ciphertext_result.buffer[TEST_TAG_OFFSET] ^= 1;

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, plaintext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_dec_corrupted_data_after_enc_fails)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        // corrupt data bit
        ciphertext_result.buffer[TEST_CIPHERTEXT_OFFSET] ^= 1;

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, plaintext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_small_data_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        unsigned char data[] = {'a'};
        size_t data_size = sizeof(data);
        SIZED_BUFFER plaintext = {data, data_size};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};

        // act
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, (TEST_CIPHERTEXT_HEADER_SIZE + data_size), ciphertext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_and_dec_small_data_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        unsigned char data[] = {'a'};
        size_t data_size = sizeof(data);
        SIZED_BUFFER plaintext = {data, data_size};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, data_size, plaintext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(data, plaintext_result.buffer, plaintext_result.size);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(plaintext_result.buffer);
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_large_data_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        size_t data_size = 2048;
        unsigned char *data = malloc(data_size);
        ASSERT_IS_NOT_NULL_WITH_MSG(data, "Line:" TOSTRING(__LINE__));
        memset(data, 0xDE, data_size);
        SIZED_BUFFER plaintext = {data, data_size};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};

        // act
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, TEST_CIPHERTEXT_HEADER_SIZE + data_size, ciphertext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
        free(data);
    }

    TEST_FUNCTION(test_enc_and_dec_large_data_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        size_t data_size = 2048;
        unsigned char *data = malloc(data_size);
        ASSERT_IS_NOT_NULL_WITH_MSG(data, "Line:" TOSTRING(__LINE__));
        memset(data, 0xDE, data_size);
        SIZED_BUFFER plaintext = {data, data_size};
        SIZED_BUFFER iv = {TEST_IV, TEST_IV_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, data_size, plaintext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(data, plaintext_result.buffer, plaintext_result.size);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(plaintext_result.buffer);
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
        free(data);
    }

    TEST_FUNCTION(test_enc_and_dec_large_iv_success)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV_LARGE, TEST_IV_LARGE_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, TEST_STRING_SIZE, plaintext_result.size, "Line:" TOSTRING(__LINE__));
        status = memcmp(TEST_STRING, plaintext_result.buffer, plaintext_result.size);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(plaintext_result.buffer);
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_enc_and_dec_large_iv_corrupted_fails)
    {
        // arrange
        int status;
        KEY_HANDLE key_handle = create_encryption_key(TEST_KEY, TEST_KEY_SIZE);
        ASSERT_IS_NOT_NULL_WITH_MSG(key_handle, "Line:" TOSTRING(__LINE__));
        SIZED_BUFFER id = {TEST_ID_1, TEST_ID_1_SIZE};
        SIZED_BUFFER plaintext = {TEST_STRING, TEST_STRING_SIZE};
        SIZED_BUFFER iv = {TEST_IV_LARGE, TEST_IV_LARGE_SIZE};
        SIZED_BUFFER ciphertext_result = {NULL, 0};
        SIZED_BUFFER plaintext_result = {NULL, 0};
        status = key_encrypt(key_handle, &id, &plaintext, &iv, &ciphertext_result);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        // corrupt one bit in the iv
        iv.buffer[iv.size - 1] ^= 1;

        // act
        status = key_decrypt(key_handle, &id, &ciphertext_result, &iv, &plaintext_result);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_NULL_WITH_MSG(plaintext_result.buffer, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, 0, plaintext_result.size, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(ciphertext_result.buffer);
        key_destroy(key_handle);
    }

    TEST_FUNCTION(test_generate_encryption_key_success)
    {
        // arrange
        unsigned char *key1, *key2;
        size_t key1_size, key2_size;
        int status;

        // act 1
        status = generate_encryption_key(&key1, &key1_size);

        // assert 1
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, ENCRYPTION_KEY_SIZE, key1_size, "Line:" TOSTRING(__LINE__));

        // act 2
        status = generate_encryption_key(&key2, &key2_size);

        // assert 2
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(size_t, ENCRYPTION_KEY_SIZE, key2_size, "Line:" TOSTRING(__LINE__));
        status = memcmp(key1, key2, ENCRYPTION_KEY_SIZE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
        free(key1);
        free(key2);
    }

END_TEST_SUITE(edge_openssl_enc_tests)
