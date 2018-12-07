// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "umocktypes.h"
#include "umocktypes_charptr.h"
#include "azure_c_shared_utility/gballoc.h"
#include "hsm_log.h"


//#############################################################################
// Interface(s) under test
//#############################################################################

#include "hsm_utils.h"

//#############################################################################
// Test defines and data
//#############################################################################

#define TEST_FILE_BAD_NAME "test_does_not_exist.txt"
static char *TEST_FILE_BAD = NULL;

#define TEST_FILE_ALPHA_NAME "test_alpha.txt"
static char *TEST_FILE_ALPHA = NULL;

#define TEST_FILE_ALPHA_NEWLINE_NAME "test_alpha_newline.txt"
static char *TEST_FILE_ALPHA_NEWLINE = NULL;

#define TEST_FILE_NUMERIC_NAME "test_numeric.txt"
static char *TEST_FILE_NUMERIC = NULL;

#define TEST_FILE_NUMERIC_NEWLINE_NAME "test_numeric_newline.txt"
static char *TEST_FILE_NUMERIC_NEWLINE = NULL;

#define TEST_FILE_EMPTY_NAME "test_empty.txt"
static char *TEST_FILE_EMPTY = NULL;

#define TEST_WRITE_FILE_NAME "test_write_data.txt"
static char *TEST_WRITE_FILE = NULL;

#define TEST_WRITE_FILE_FOR_DELETE_NAME "test_write_data_del.txt"
static char *TEST_WRITE_FILE_FOR_DELETE = NULL;

static char ALPHA[] = "ABCD";
static char ALPHA_NEWLINE[] = "AB\nCD\n";
static unsigned char NUMERIC[] = {'1', '2', '3', '4'};
static unsigned char NUMERIC_NEWLINE[] = {'1', '2', '\n', '4', '5', '\n'};

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static char* TEST_TEMP_DIR = NULL;
static char* TEST_TEMP_DIR_GUID = NULL;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_testdir(void)
{
    TEST_TEMP_DIR = hsm_test_util_create_temp_dir(&TEST_TEMP_DIR_GUID);
    ASSERT_IS_NOT_NULL(TEST_TEMP_DIR_GUID, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(TEST_TEMP_DIR, "Line:" TOSTRING(__LINE__));
    printf("Temp dir created: [%s]\r\n", TEST_TEMP_DIR);
}

static void test_helper_teardown_testdir(void)
{
    if ((TEST_TEMP_DIR != NULL) && (TEST_TEMP_DIR_GUID != NULL))
    {
        hsm_test_util_delete_dir(TEST_TEMP_DIR_GUID);
        free(TEST_TEMP_DIR);
        TEST_TEMP_DIR = NULL;
        free(TEST_TEMP_DIR_GUID);
        TEST_TEMP_DIR_GUID = NULL;
    }
}

static int test_helper_write_data_to_file
(
    const char* file_name,
    const unsigned char* input_data,
    size_t input_data_size
)
{
    FILE *file_handle;
    int result;
    if ((file_handle = fopen(file_name, "wb")) == NULL)
    {
        LOG_ERROR("Could not open file for write %s", file_name);
        result = __LINE__;
    }
    else
    {
        result = 0;
        if (input_data != NULL)
        {
            size_t num_bytes_written = fwrite(input_data, 1, input_data_size, file_handle);
            if (num_bytes_written != input_data_size)
            {
                LOG_ERROR("File write failed for file %s", file_name);
                result = __FAILURE__;
            }
        }
    }

    if (file_handle != NULL)
    {
        fclose(file_handle);
    }

    return result;
}

static void delete_file_if_exists(const char* file_name)
{
    (void)remove(file_name);
}

static char* prepare_file_path(const char* base_dir, const char* file_name)
{
    size_t path_size = get_max_file_path_size();
    char *file_path = calloc(path_size, 1);
    ASSERT_IS_NOT_NULL(file_path, "Line:" TOSTRING(__LINE__));
    int status = snprintf(file_path, path_size, "%s%s", base_dir, file_name);
    ASSERT_IS_TRUE(((status > 0) || (status < (int)path_size)), "Line:" TOSTRING(__LINE__));

    return file_path;
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_util_int_tests)

        TEST_SUITE_INITIALIZE(TestClassInitialize)
        {
            TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
            g_testByTest = TEST_MUTEX_CREATE();
            ASSERT_IS_NOT_NULL(g_testByTest);

            test_helper_setup_testdir();

            TEST_FILE_ALPHA = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_ALPHA_NAME);
            ASSERT_ARE_EQUAL(int, 0, test_helper_write_data_to_file(TEST_FILE_ALPHA, (unsigned char*)ALPHA, strlen(ALPHA)));

            TEST_FILE_ALPHA_NEWLINE = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_ALPHA_NEWLINE_NAME);
            ASSERT_ARE_EQUAL(int, 0, test_helper_write_data_to_file(TEST_FILE_ALPHA_NEWLINE, (unsigned char*)ALPHA_NEWLINE, strlen(ALPHA_NEWLINE)));

            TEST_FILE_NUMERIC = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_NUMERIC_NAME);
            ASSERT_ARE_EQUAL(int, 0, test_helper_write_data_to_file(TEST_FILE_NUMERIC, (unsigned char*)NUMERIC, sizeof(NUMERIC)));

            TEST_FILE_NUMERIC_NEWLINE = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_NUMERIC_NEWLINE_NAME);
            ASSERT_ARE_EQUAL(int, 0, test_helper_write_data_to_file(TEST_FILE_NUMERIC_NEWLINE, (unsigned char*)NUMERIC_NEWLINE, sizeof(NUMERIC_NEWLINE)));

            TEST_FILE_EMPTY = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_EMPTY_NAME);
            ASSERT_ARE_EQUAL(int, 0, test_helper_write_data_to_file(TEST_FILE_EMPTY, NULL, 0));

            TEST_FILE_BAD = prepare_file_path(TEST_TEMP_DIR, TEST_FILE_BAD_NAME);
            TEST_WRITE_FILE = prepare_file_path(TEST_TEMP_DIR, TEST_WRITE_FILE_NAME);
            TEST_WRITE_FILE_FOR_DELETE = prepare_file_path(TEST_TEMP_DIR, TEST_WRITE_FILE_FOR_DELETE_NAME);
        }

        TEST_SUITE_CLEANUP(TestClassCleanup)
        {
            delete_file_if_exists(TEST_FILE_ALPHA);
            delete_file_if_exists(TEST_FILE_NUMERIC);
            delete_file_if_exists(TEST_FILE_ALPHA_NEWLINE);
            delete_file_if_exists(TEST_FILE_NUMERIC_NEWLINE);
            delete_file_if_exists(TEST_FILE_EMPTY);
            free(TEST_FILE_ALPHA); TEST_FILE_ALPHA = NULL;
            free(TEST_FILE_ALPHA_NEWLINE); TEST_FILE_ALPHA_NEWLINE = NULL;
            free(TEST_FILE_NUMERIC); TEST_FILE_NUMERIC = NULL;
            free(TEST_FILE_NUMERIC_NEWLINE); TEST_FILE_NUMERIC_NEWLINE = NULL;
            free(TEST_FILE_EMPTY); TEST_FILE_EMPTY = NULL;
            free(TEST_FILE_BAD); TEST_FILE_BAD = NULL;
            free(TEST_WRITE_FILE); TEST_WRITE_FILE = NULL;
            free(TEST_WRITE_FILE_FOR_DELETE); TEST_WRITE_FILE_FOR_DELETE = NULL;
            test_helper_teardown_testdir();
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

        TEST_FUNCTION(read_file_into_cstring_smoke)
        {
            // arrange
            char *expected_string = ALPHA;
            size_t expected_string_size = sizeof(ALPHA);

            // act
            size_t output_size = 0;
            char *output_string = read_file_into_cstring(TEST_FILE_ALPHA, &output_size);

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(read_file_into_cstring_with_newline_smoke)
        {
            // arrange
            char *expected_string = ALPHA_NEWLINE;
            size_t expected_string_size = sizeof(ALPHA_NEWLINE);

            // act
            size_t output_size = 0;
            char *output_string = read_file_into_cstring(TEST_FILE_ALPHA_NEWLINE, &output_size);

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(read_file_into_cstring_non_existant_file_returns_null)
        {
            // arrange
            size_t output_size = 100;

            // act
            char *output_string = read_file_into_cstring(TEST_FILE_BAD, &output_size);

            // assert
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(read_file_into_cstring_empty_file_returns_null)
        {
            // arrange
            size_t output_size = 100;

            // act
            char *output_string = read_file_into_cstring(TEST_FILE_EMPTY, &output_size);

            // assert
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(read_file_into_cstring_invalid_params_returns_null)
        {
            // arrange
            size_t output_size;
            unsigned char *output_string;

            // act, assert
            output_size = 100;
            output_string = (unsigned char *)read_file_into_cstring(NULL, &output_size);
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // act, assert
            output_size = 100;
            output_string = (unsigned char *)read_file_into_cstring("", &output_size);
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(read_file_into_cbuffer_smoke)
        {
            // arrange
            unsigned char *expected_buffer = NUMERIC;
            size_t expected_buffer_size = sizeof(NUMERIC);

            // act
            size_t output_size = 0;
            unsigned char *output_buffer = read_file_into_buffer(TEST_FILE_NUMERIC, &output_size);

            // assert
            ASSERT_IS_NOT_NULL(output_buffer);
            int cmp_result = memcmp(expected_buffer, output_buffer, expected_buffer_size);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_buffer_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_buffer);
        }

        TEST_FUNCTION(read_file_into_cbuffer_newline_smoke)
        {
            // arrange
            unsigned char *expected_buffer = NUMERIC_NEWLINE;
            size_t expected_buffer_size = sizeof(NUMERIC_NEWLINE);

            // act
            size_t output_size = 0;
            unsigned char *output_buffer = read_file_into_buffer(TEST_FILE_NUMERIC_NEWLINE, &output_size);

            // assert
            ASSERT_IS_NOT_NULL(output_buffer);
            int cmp_result = memcmp(expected_buffer, output_buffer, expected_buffer_size);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_buffer_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_buffer);
        }

        TEST_FUNCTION(read_file_into_cbuffer_invalid_params_returns_null)
        {
            // arrange
            size_t output_size;
            unsigned char *output_buffer;

            // act, assert
            output_size = 100;
            output_buffer = read_file_into_buffer(NULL, &output_size);
            ASSERT_IS_NULL(output_buffer);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // act, assert
            output_size = 100;
            output_buffer = read_file_into_buffer("", &output_size);
            ASSERT_IS_NULL(output_buffer);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(read_file_into_cbuffer_non_existant_file_returns_null)
        {
            // arrange
            size_t output_size = 100;

            // act
            unsigned char *output_buffer = read_file_into_buffer(TEST_FILE_BAD, &output_size);

            // assert
            ASSERT_IS_NULL(output_buffer);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(read_file_into_cbuffer_empty_file_returns_null)
        {
            // arrange
            size_t output_size = 100;

            // act
            unsigned char *output_buffer = read_file_into_buffer(TEST_FILE_EMPTY, &output_size);

            // assert
            ASSERT_IS_NULL(output_buffer);
            ASSERT_ARE_EQUAL(size_t, 0, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(concat_files_to_cstring_invalid_params)
        {
            // arrange
            char *output_string;
            const char *files[] = {
                TEST_FILE_ALPHA,
                TEST_FILE_NUMERIC
            };

            // act, assert
            output_string = concat_files_to_cstring(NULL, 10);
            ASSERT_IS_NULL(output_string);

            output_string = concat_files_to_cstring(files, 0);
            ASSERT_IS_NULL(output_string);

            // cleanup
        }

        TEST_FUNCTION(concat_files_to_cstring_smoke)
        {
            // arrange
            char *expected_string = "ABCD1234";
            size_t expected_string_size = 9;
            const char *files[] = {
                TEST_FILE_ALPHA,
                TEST_FILE_NUMERIC
            };

            // act
            char *output_string = concat_files_to_cstring(files, sizeof(files)/sizeof(files[0]));
            size_t output_size = strlen(output_string) + 1;

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(concat_files_to_cstring_newline_smoke)
        {
            // arrange
            char *expected_string = "AB\nCD\n12\n45\n";
            size_t expected_string_size = 13;
            const char *files[] = {
                TEST_FILE_ALPHA_NEWLINE,
                TEST_FILE_NUMERIC_NEWLINE
            };

            // act
            char *output_string = concat_files_to_cstring(files, sizeof(files)/sizeof(files[0]));
            size_t output_size = strlen(output_string) + 1;

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(concat_files_to_cstring_with_empty_file_smoke)
        {
            // arrange
            char *expected_string = "ABCD1234";
            size_t expected_string_size = 9;
            const char *files[] = {
                TEST_FILE_ALPHA,
                TEST_FILE_EMPTY,
                TEST_FILE_NUMERIC
            };

            // act
            char *output_string = concat_files_to_cstring(files, sizeof(files)/sizeof(files[0]));
            size_t output_size = strlen(output_string) + 1;

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(concat_files_to_cstring_with_all_empty_file_smoke)
        {
            // arrange
            char *expected_string = "";
            size_t expected_string_size = 1;
            const char *files[] = {
                TEST_FILE_EMPTY,
                TEST_FILE_EMPTY,
                TEST_FILE_EMPTY,
            };

            // act
            char *output_string = concat_files_to_cstring(files, sizeof(files)/sizeof(files[0]));
            size_t output_size = strlen(output_string) + 1;

            // assert
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(concat_files_to_cstring_with_bad_file_returns_null)
        {
            // arrange
            const char *files[] = {
                TEST_FILE_ALPHA,
                TEST_FILE_BAD,
                TEST_FILE_NUMERIC
            };

            // act
            char *output_string = concat_files_to_cstring(files, sizeof(files)/sizeof(files[0]));

            // assert
            ASSERT_IS_NULL(output_string);

            // cleanup
        }

        TEST_FUNCTION(test_is_directory_valid_returns_false_with_bad_dirs)
        {
            // arrange
            bool result;

            // act, assert
            result = is_directory_valid(NULL);
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            result = is_directory_valid("");
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            result = is_directory_valid("some_bad_dir");
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_is_directory_valid_returns_true_with_valid_dirs)
        {
            // arrange
            bool result;
            // act, assert
            result = is_directory_valid(".");
            ASSERT_IS_TRUE(result, "Line:" TOSTRING(__LINE__));

            result = is_directory_valid("..");
            ASSERT_IS_TRUE(result, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_is_file_valid_returns_false_with_bad_files)
        {
            // arrange
            bool result;

            // act, assert
            result = is_file_valid(NULL);
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            result = is_file_valid("");
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            result = is_file_valid(TEST_FILE_BAD);
            ASSERT_IS_FALSE(result, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_is_file_valid_returns_true_with_valid_files)
        {
            // arrange
            bool result;

            // act, assert
            result = is_file_valid(TEST_FILE_ALPHA);
            ASSERT_IS_TRUE(result, "Line:" TOSTRING(__LINE__));

            result = is_file_valid(TEST_FILE_NUMERIC);
            ASSERT_IS_TRUE(result, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_write_cstring_to_file_smoke)
        {
            // arrange
            const char *expected_string = "ZZXXYYZZ";
            size_t expected_string_size = 9;
            const char *input_string = "ZZXXYYZZ";
            (void)delete_file(TEST_WRITE_FILE);

            // act
            int output = write_cstring_to_file(TEST_WRITE_FILE, input_string);
            size_t output_size = 0;
            char *output_string = read_file_into_cstring(TEST_WRITE_FILE, &output_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(output_string);
            int cmp_result = strcmp(expected_string, output_string);
            ASSERT_ARE_EQUAL(int, 0, cmp_result, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output_string);
        }

        TEST_FUNCTION(test_write_cstring_to_file_invalid_params)
        {
            // arrange
            int output;
            (void)delete_file(TEST_WRITE_FILE);

            // act, assert
            output = write_cstring_to_file(NULL, "abcd");
            ASSERT_ARE_NOT_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));

            output = write_cstring_to_file("", "abcd");
            ASSERT_ARE_NOT_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));

            output = write_cstring_to_file(TEST_WRITE_FILE, NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_write_cstring_to_file_empty_file_returns_null_when_read)
        {
            // arrange
            const char *expected_string = NULL;
            size_t expected_string_size = 0;
            const char *input_string = "";
            (void)delete_file(TEST_WRITE_FILE);

            // act
            int output = write_cstring_to_file(TEST_WRITE_FILE, input_string);
            size_t output_size = 10;
            char *output_string = read_file_into_cstring(TEST_WRITE_FILE, &output_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_delete_file_smoke)
        {
            // arrange
            size_t expected_string_size = 0;
            const char *input_string = "abcd";

            int status = write_cstring_to_file(TEST_WRITE_FILE_FOR_DELETE, input_string);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));

            // act
            int output = delete_file(TEST_WRITE_FILE_FOR_DELETE);
            ASSERT_ARE_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));
            size_t output_size = 10;
            char *output_string = read_file_into_cstring(TEST_WRITE_FILE_FOR_DELETE, &output_size);

            // assert
            ASSERT_ARE_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(output_string);
            ASSERT_ARE_EQUAL(size_t, expected_string_size, output_size, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_delete_file_invalid_params)
        {
            // arrange
            int output;

            // act, assert
            output = delete_file(NULL);
            ASSERT_ARE_NOT_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));

            output = delete_file("");
            ASSERT_ARE_NOT_EQUAL(int, 0, output, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

        TEST_FUNCTION(test_hsm_env_input)
        {
            // arrange
            int status;
            char *output = NULL;

            // act
            status = hsm_get_env(NULL,&output);
            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            // act
            status = hsm_get_env("TEST_ENV_1",NULL);
            // assert
            ASSERT_ARE_NOT_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            // cleanup
        }

        TEST_FUNCTION(test_hsm_env_get_smoke)
        {
            // arrange
            int status;
            char *input_data = "1234";
            hsm_test_util_setenv("TEST_ENV_1", input_data);
            char *output = NULL;

            // act
            status = hsm_get_env("TEST_ENV_1", &output);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(char_ptr, input_data, output, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(size_t, strlen(input_data), strlen(output), "Line:" TOSTRING(__LINE__));

            // cleanup
            free(output);
            output = NULL;

            // arrange
            hsm_test_util_unsetenv("TEST_ENV_1");

            // act
            status = hsm_get_env("TEST_ENV_1", &output);

            // assert
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NULL(output, "Line:" TOSTRING(__LINE__));

            // cleanup
        }

END_TEST_SUITE(edge_hsm_util_int_tests)
