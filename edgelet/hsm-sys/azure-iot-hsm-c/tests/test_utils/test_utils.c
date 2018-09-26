//#define _POSIX_C_SOURCE 200809L
//#define _XOPEN_SOURCE 700

#include <stdlib.h>
#include <unistd.h>
#include "testrunnerswitcher.h"
#include "azure_c_shared_utility/crt_abstractions.h"

#define MAX_FILE_SIZE 256
extern char* mkdtemp(char* template);

char* create_temp_dir(const char* test_name)
{
    char *tmp_dir, *dir_template, *result;

    tmp_dir = "/tmp"; //getenv("TMP");
    ASSERT_IS_NOT_NULL(tmp_dir);
    ASSERT_ARE_NOT_EQUAL(size_t, 0, strlen(tmp_dir));
    dir_template = calloc(MAX_FILE_SIZE, 1);
    snprintf(dir_template, MAX_FILE_SIZE, "%s/hsm_test_XXXXXX", tmp_dir);
    result = mkdtemp(dir_template);
    if (result == NULL)
    {
        printf("Temp dir create failed for template %s. Errno: (%#x, %s)\r\n", dir_template, errno, strerror(errno));
    }
    ASSERT_IS_NOT_NULL(result);
    ASSERT_ARE_NOT_EQUAL(size_t, 0, strlen(result));
    free(dir_template);

    return result;
}

void delete_test_dir(char *dir_name)
{
    int status = rmdir(dir_name);
    ASSERT_ARE_EQUAL(int, 0, status);
}
