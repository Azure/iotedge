#include <stdlib.h>
#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/uniqueid.h"

#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    #include <direct.h>
    #include <intsafe.h>
    #include <windows.h>

    #define SLASH "\\"
#else
    #include <unistd.h>

    #define SLASH "/"
#endif

#define UID_SIZE            37
#define MAX_FILE_NAME_SIZE  256
#define CREATE_DIR_OK       0
#define CREATE_DIR_EXISTS   1
#define CREATE_DIR_ERROR    2
#define MAX_ATTEMPTS        10

static char* get_temp_dir(void)
{
    char *result = calloc(MAX_FILE_NAME_SIZE, 1);
    ASSERT_IS_NOT_NULL(result);

#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    DWORD count = GetTempPathA(MAX_FILE_NAME_SIZE, result);
    ASSERT_IS_TRUE(count < MAX_FILE_NAME_SIZE);
#else
    result = strcpy_s(result, MAX_FILE_NAME_SIZE, "/tmp/");
#endif
    ASSERT_ARE_NOT_EQUAL(size_t, 0, strlen(result));

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

char* create_temp_dir(const char* test_name)
{
    char *tmp_dir, *dir_path;
    int status, attempt = 0;

    tmp_dir = get_temp_dir();
    dir_path = malloc(MAX_FILE_NAME_SIZE);
    ASSERT_IS_NOT_NULL(dir_path);
    do
    {
        char guid[UID_SIZE];

        memset(guid, 0, UID_SIZE);
        status = UniqueId_Generate(guid, UID_SIZE);
        ASSERT_ARE_EQUAL(int, UNIQUEID_OK, status);
        memset(dir_path, 0, MAX_FILE_NAME_SIZE);
        snprintf(dir_path, MAX_FILE_NAME_SIZE, "%shsm_test_%s", tmp_dir, guid);
        status = make_test_dir(dir_path);
        ASSERT_ARE_NOT_EQUAL(int, CREATE_DIR_ERROR, status);
    } while ((status == CREATE_DIR_EXISTS) && (++attempt < MAX_ATTEMPTS));

    ASSERT_ARE_NOT_EQUAL(int, MAX_ATTEMPTS, attempt);
    free(tmp_dir);

    return dir_path;
}

void delete_test_dir(char *dir_name)
{
    int status;
#if (defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    SHFILEOPSTRUCT shfo = {
        NULL,
        FO_DELETE,
        dir_name,
        NULL,
        FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMATION,
        FALSE,
        NULL,
        NULL };

    status = SHFileOperation(&shfo);
#else
    status = rmdir(dir_name);
#endif
    ASSERT_ARE_EQUAL(int, 0, status);
}
