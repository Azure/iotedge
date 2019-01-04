// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>

#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/uniqueid.h"

#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    #include <direct.h>
    #include <intsafe.h>
    #include <windows.h>

    #define SLASH "\\"
#else
    #include <sys/stat.h>
    #include <sys/types.h>
    #include <unistd.h>

    #define SLASH "/"
#endif

#define UID_SIZE            37
#define MAX_FILE_NAME_SIZE  256
#define CREATE_DIR_OK       0
#define CREATE_DIR_EXISTS   1
#define CREATE_DIR_ERROR    2
#define MAX_ATTEMPTS        10

static char* get_temp_base_dir(void)
{
    char *result = calloc(MAX_FILE_NAME_SIZE, 1);
    ASSERT_IS_NOT_NULL(result);

#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    DWORD count = GetTempPathA(MAX_FILE_NAME_SIZE, result);
    ASSERT_IS_TRUE(count < MAX_FILE_NAME_SIZE, "TestUtil Line:" TOSTRING(__LINE__));
#else
    strcpy_s(result, MAX_FILE_NAME_SIZE, "/tmp/");
#endif
    ASSERT_ARE_NOT_EQUAL(size_t, 0, strlen(result), "TestUtil Line:" TOSTRING(__LINE__));

    return result;
}

static int make_test_dir(const char* dir_path)
{
    int status, result;
#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    status = _mkdir(dir_path);
#else
    status = mkdir(dir_path, S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH);
#endif

    if (status != 0)
    {
        if (errno == EEXIST)
        {
            printf("Directory '%s' already exists.\r\n", dir_path);
            result = CREATE_DIR_EXISTS;
        }
        else
        {
            printf("Directory create failed for '%s'. Errno: %s.\r\n", dir_path, strerror(errno));
            result = CREATE_DIR_ERROR;
        }
    }
    else
    {
        result = CREATE_DIR_OK;
    }

    return result;
}

size_t get_max_file_path_size(void)
{
    return MAX_FILE_NAME_SIZE;
}

char *create_temp_dir_path(const char *dir_guid)
{
    int status;
    char *tmp_dir, *dir_path;

    tmp_dir = get_temp_base_dir();
    dir_path = calloc(MAX_FILE_NAME_SIZE, 1);
    ASSERT_IS_NOT_NULL(dir_path, "TestUtil Line:" TOSTRING(__LINE__));
    status = snprintf(dir_path, MAX_FILE_NAME_SIZE, "%shsm_test_%s", tmp_dir, dir_guid);
    ASSERT_IS_TRUE(((status > 0) || (status < MAX_FILE_NAME_SIZE)), "TestUtil Line:" TOSTRING(__LINE__));
    free(tmp_dir);

    return dir_path;
}

char* hsm_test_util_create_temp_dir(char **dir_guid)
{
    char *dir_path, *guid;
    int status, attempt = 0, dir_made = 0;

    ASSERT_IS_NOT_NULL(dir_guid, "TestUtil Line:" TOSTRING(__LINE__));
    guid = (char*)malloc(UID_SIZE);
    ASSERT_IS_NOT_NULL(guid, "TestUtil Line:" TOSTRING(__LINE__));
    do
    {
        memset(guid, 0, UID_SIZE);
        status = UniqueId_Generate(guid, UID_SIZE);
        ASSERT_ARE_EQUAL(int, UNIQUEID_OK, status, "TestUtil Line:" TOSTRING(__LINE__));
        dir_path = create_temp_dir_path(guid);
        status = make_test_dir(dir_path);
        ASSERT_ARE_NOT_EQUAL(int, CREATE_DIR_ERROR, status, "TestUtil Line:" TOSTRING(__LINE__));
        if (status == CREATE_DIR_EXISTS)
        {
            free(dir_path);
            dir_path = NULL;
        }
        else
        {
            dir_made = 1;
            break;
        }
    } while (++attempt < MAX_ATTEMPTS);

    ASSERT_ARE_EQUAL(int, 1, dir_made, "TestUtil Line:" TOSTRING(__LINE__));

    *dir_guid = guid;

    return dir_path;
}

void hsm_test_util_delete_dir(const char *dir_guid)
{
    int status;

    ASSERT_IS_NOT_NULL(dir_guid, "TestUtil Line:" TOSTRING(__LINE__));
    char *dir_path = create_temp_dir_path(dir_guid);
    printf("Deleting temp directory '%s'.\r\n", dir_path);

#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    SHFILEOPSTRUCTA shfo = {
        NULL,
        FO_DELETE,
        dir_path,
        NULL,
        FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMATION,
        FALSE,
        NULL,
        NULL };
    status = SHFileOperationA(&shfo);
#else
    const char *cmd_prefix = "rm -fr ";
    size_t cmd_size = strlen(cmd_prefix) + MAX_FILE_NAME_SIZE + 1;
    char *cmd = calloc(cmd_size, 1);
    ASSERT_IS_NOT_NULL(cmd, "TestUtil Line:" TOSTRING(__LINE__));
    status = snprintf(cmd, cmd_size, "%s%s", cmd_prefix, dir_path);
    ASSERT_IS_TRUE(((status > 0) || (status < (int)cmd_size)), "TestUtil Line:" TOSTRING(__LINE__));
    printf("Deleting directory using command '%s'.\r\n", cmd);
    status = system(cmd);
    free(cmd);
#endif
    ASSERT_ARE_EQUAL(int, 0, status, "TestUtil Line:" TOSTRING(__LINE__));
    free(dir_path);
}

void hsm_test_util_setenv(const char *key, const char *value)
{
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        errno_t status = _putenv_s(key, value);
    #else
        int status = setenv(key, value, 1);
    #endif
    ASSERT_ARE_EQUAL(int, 0, status, "TestUtil Line:" TOSTRING(__LINE__));
    const char *retrieved_value = getenv(key);
    if (retrieved_value != NULL)
    {
        int cmp = strcmp(retrieved_value, value);
        ASSERT_ARE_EQUAL(int, 0, cmp, "TestUtil Line:" TOSTRING(__LINE__));
    }
}

void hsm_test_util_unsetenv(const char *key)
{
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        STRING_HANDLE key_handle = STRING_construct(key);
        ASSERT_IS_NOT_NULL(key_handle, "TestUtil Line:" TOSTRING(__LINE__));
        int ret_val = STRING_concat(key_handle, "=");
        ASSERT_ARE_EQUAL(int, 0, ret_val, "TestUtil Line:" TOSTRING(__LINE__));
        errno_t status = _putenv(STRING_c_str(key_handle));
        STRING_delete(key_handle);
    #else
        int status = unsetenv(key);
    #endif
    ASSERT_ARE_EQUAL(int, 0, status, "TestUtil Line:" TOSTRING(__LINE__));
    const char *retrieved_value = getenv(key);
    ASSERT_IS_NULL(retrieved_value, "TestUtil Line:" TOSTRING(__LINE__));
}
