#include <fcntl.h>
#include <limits.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/stat.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "hsm_log.h"
#include "hsm_utils.h"

#define HSM_UTIL_SUCCESS 0
#define HSM_UTIL_ERROR 1
#define HSM_UTIL_EMPTY 2
#define HSM_UTIL_UNSUPPORTED 3

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    #include <direct.h>
    #include <intsafe.h>
    #include <windows.h>
    #if !defined S_ISDIR
        #define S_ISDIR(m) (((m) & _S_IFDIR) == _S_IFDIR)
    #endif
    #define HSM_MKDIR(dir_path) _mkdir(dir_path)
#else
    #include <unistd.h>
    #include <sys/types.h>

    #ifndef SSIZE_MAX
        #define SSIZE_MAX INT_MAX
    #endif

    // equivalent to 755
    #define HSM_MKDIR(dir_path) mkdir(dir_path, S_IRWXU | S_IRGRP | S_IXGRP | S_IROTH | S_IXOTH)
#endif

static const char* err_to_str(void)
{
    static char DEFAULT_ERROR_STRING[] = "";

    char *result = strerror(errno);
    if (result == NULL)
    {
        result = DEFAULT_ERROR_STRING;
    }

    return result;
}

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
static int create_file_handle_for_reading(const char *file_name, HANDLE *handle)
{
    int result;
    HANDLE file_handle = CreateFileA(file_name, GENERIC_READ, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file_handle == INVALID_HANDLE_VALUE)
    {
        LOG_ERROR("Could not open file for reading %s. GetLastError=%08x", file_name, GetLastError());
        result = __FAILURE__;
    }
    else
    {
        *handle = file_handle;
        result = 0;
    }

    return result;
}
#endif

static int read_file_into_buffer_impl
(
    const char *file_name,
    void  *output_buffer,
    size_t output_buffer_size,
    size_t *file_size_in_bytes
)
{

    int result;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows

    HANDLE file_handle;
    LARGE_INTEGER file_size;

    if (file_size_in_bytes != NULL)
    {
        *file_size_in_bytes = 0;
    }

    if (create_file_handle_for_reading(file_name, &file_handle) != 0)
    {
        result = HSM_UTIL_ERROR;
    }
    else
    {
        if (!GetFileSizeEx(file_handle, &file_size))
        {
            LOG_ERROR("Could not get file size for %s. GetLastError=%08x", file_name, GetLastError());
            result = HSM_UTIL_ERROR;
        }
        else if (file_size.QuadPart == 0)
        {
            LOG_ERROR("File size found to be zero for %s", file_name);
            result = HSM_UTIL_EMPTY;
        }
        else
        {
            DWORD file_size_converted = 0;

            if (ULongLongToDWord(file_size.QuadPart, &file_size_converted) != S_OK)
            {
                LOG_ERROR("File size too large, overflow detected for %s", file_name);
                result = HSM_UTIL_ERROR;
            }
            else
            {
                if (file_size_in_bytes != NULL)
                {
                    *file_size_in_bytes = file_size_converted;
                }
                if (output_buffer != NULL)
                {
                    DWORD num_bytes_read = 0;
                    DWORD num_bytes_to_read = (output_buffer_size < (size_t)file_size_converted) ?
                                               (DWORD)output_buffer_size :
                                               file_size_converted;
                    BOOL read_res = ReadFile(file_handle,
                                             output_buffer,
                                             num_bytes_to_read,
                                             &num_bytes_read,
                                             NULL);
                    if (!read_res)
                    {
                        LOG_ERROR("File read failed for file %s. GetLastError=%08x", file_name, GetLastError());
                        result = HSM_UTIL_ERROR;
                    }
                    else
                    {
                        result = HSM_UTIL_SUCCESS;
                    }
                }
                else
                {
                    result = HSM_UTIL_SUCCESS;
                }
            }
        }
        CloseHandle(file_handle);
    }

#else

    int fd;
    off_t file_size;
    struct stat stbuf;

    if (file_size_in_bytes != NULL)
    {
        *file_size_in_bytes = 0;
    }

    if ((fd = open(file_name, O_RDONLY)) == -1)
    {
        LOG_ERROR("Could not open file for reading %s. Errno %d '%s'", file_name, errno, err_to_str());
        result = HSM_UTIL_ERROR;
    }
    else
    {
        if (fstat(fd, &stbuf) != 0)
        {
            LOG_ERROR("fstat returned error for file %s. Errno %d '%s'", file_name, errno, err_to_str());
            result = HSM_UTIL_ERROR;
        }
        else if (!S_ISREG(stbuf.st_mode))
        {
            LOG_ERROR("File %s is not a regular file.", file_name);
            result = HSM_UTIL_ERROR;
        }
        else
        {
            file_size = stbuf.st_size;
            if (file_size < 0)
            {
                LOG_ERROR("File size invalid for %s", file_name);
                result = HSM_UTIL_ERROR;
            }
            else if (file_size == 0)
            {
                LOG_ERROR("File size found to be zero for %s", file_name);
                result = HSM_UTIL_EMPTY;
            }
            else
            {
                if (file_size_in_bytes != NULL)
                {
                    *file_size_in_bytes = file_size;
                }
                if (output_buffer != NULL)
                {
                    ssize_t num_bytes_read;
                    size_t num_bytes_to_read = (output_buffer_size < (size_t)file_size) ?
                                                output_buffer_size :
                                                (size_t)file_size;
                    if (num_bytes_to_read > SSIZE_MAX)
                    {
                        LOG_ERROR("Unsupported file read operation. File too large %s.", file_name);
                        result = HSM_UTIL_ERROR;
                    }
                    else
                    {
                        num_bytes_read = read(fd, output_buffer, num_bytes_to_read);
                        if (num_bytes_read == -1)
                        {
                            LOG_ERROR("File read failed for file %s. Errno %d '%s'", file_name, errno, err_to_str());
                            result = HSM_UTIL_ERROR;
                        }
                        else
                        {
                            result = HSM_UTIL_SUCCESS;
                        }
                    }
                }
                else
                {
                    result = HSM_UTIL_SUCCESS;
                }
            }
        }
        close(fd);
    }
#endif

    return result;
}

static bool is_windows()
{
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        return true;
    #else
        return false;
    #endif
}

static int write_ascii_buffer_into_file
(
    const char *file_name,
    const void *input_buffer,
    size_t input_buffer_size,
    bool make_private
)
{
    int result = HSM_UTIL_UNSUPPORTED;

    if (is_windows() || !make_private)
    {
        FILE *file_handle;
        if ((file_handle = fopen(file_name, "w")) == NULL)
        {
            LOG_ERROR("Could not open file for writing %s", file_name);
            result = HSM_UTIL_ERROR;
        }
        else
        {
            result = HSM_UTIL_SUCCESS;
            if (input_buffer_size != 0)
            {
                size_t num_bytes_written;
                num_bytes_written = fwrite(input_buffer, 1, input_buffer_size, file_handle);
                if ((num_bytes_written != input_buffer_size) || (ferror(file_handle) != 0))
                {
                    LOG_ERROR("File write failed for file %s", file_name);
                    result = HSM_UTIL_ERROR;
                }
                else
                {
                    (void)fflush(file_handle);
                }
            }
            (void)fclose(file_handle);
        }
    }
#if !(defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows)
    else
    {
        int fd = open(file_name, O_CREAT|O_WRONLY|O_TRUNC, S_IRUSR|S_IWUSR);
        if (fd == -1)
        {
            LOG_ERROR("Could not open file for writing %s", file_name);
            result = HSM_UTIL_ERROR;
        }
        else
        {
            size_t num_bytes_written;
            result = HSM_UTIL_SUCCESS;
            if (input_buffer_size != 0)
            {
                ssize_t write_status = write(fd, input_buffer, input_buffer_size);
                if ((write_status == -1) || (write_status != input_buffer_size))
                {
                    LOG_ERROR("File write failed for file %s", file_name);
                    result = HSM_UTIL_ERROR;
                }
                else if (fsync(fd) != 0)
                {
                    LOG_ERROR("File sync failed for file %s", file_name);
                    result = HSM_UTIL_ERROR;
                }
            }
            (void)close(fd);
        }
    }
#endif

    if (result != HSM_UTIL_SUCCESS)
    {
        (void)delete_file(file_name);
    }
    return result;
}

void* read_file_into_buffer(const char* file_name, size_t *output_buffer_size)
{
    void* result;
    size_t file_size_in_bytes = 0;

    if (output_buffer_size != NULL)
    {
        *output_buffer_size = 0;
    }

    if ((file_name == NULL) || (strlen(file_name) == 0))
    {
        LOG_ERROR("Invalid file name");
        result = NULL;
    }
    else if (read_file_into_buffer_impl(file_name, NULL, 0, &file_size_in_bytes) != HSM_UTIL_SUCCESS)
    {
        result = NULL;
    }
    else if ((result = malloc(file_size_in_bytes)) == NULL)
    {
        LOG_ERROR("Could not allocate memory to store the contents of the file %s", file_name);
    }
    else if (read_file_into_buffer_impl(file_name, result, file_size_in_bytes, NULL) != HSM_UTIL_SUCCESS)
    {
        LOG_ERROR("Could not read file into buffer");
        free(result);
        result = NULL;
    }
    else
    {
        if (output_buffer_size)
        {
            *output_buffer_size = file_size_in_bytes;
        }
    }

    return result;
}

char* read_file_into_cstring(const char* file_name, size_t *output_buffer_size)
{
    char* result;
    size_t file_size_in_bytes = 0;

    if (output_buffer_size != NULL)
    {
        *output_buffer_size = 0;
    }

    if ((file_name == NULL) || (strlen(file_name) == 0))
    {
        LOG_ERROR("Invalid file name");
        result = NULL;
    }
    else if (read_file_into_buffer_impl(file_name, NULL, 0, &file_size_in_bytes) != HSM_UTIL_SUCCESS)
    {
        result = NULL;
    }
    else
    {
        size_t file_size_in_bytes_check = file_size_in_bytes + 1;
        if (file_size_in_bytes_check < file_size_in_bytes) // check if roll over occurred
        {
            LOG_ERROR("Unexpected file size for file %s", file_name);
            result = NULL;
        }
        else if ((result = (char*)malloc(file_size_in_bytes + 1)) == NULL)
        {
            LOG_ERROR("Could not allocate memory to store the contents of the file %s", file_name);
        }
        else if (read_file_into_buffer_impl(file_name, result, file_size_in_bytes, NULL) != HSM_UTIL_SUCCESS)
        {
            LOG_ERROR("Could not read file into buffer: %s", file_name);
            free(result);
            result = NULL;
        }
        else
        {
            result[file_size_in_bytes] = 0;
            if (output_buffer_size != NULL)
            {
                *output_buffer_size = file_size_in_bytes + 1;
            }
        }
    }
    return result;
}

char* concat_files_to_cstring(const char **file_names, int num_files)
{
    char *result;
    if ((file_names == NULL) || (num_files <= 0))
    {
        LOG_ERROR("Invalid parameters");
        result = NULL;
    }
    else
    {
        int index;
        bool errors_found = false;
        size_t accumulated_size = 0;
        size_t accumulated_size_check;
        for (index = 0; index < num_files; index++)
        {
            size_t buffer_size;
            int status = read_file_into_buffer_impl(file_names[index], NULL, 0, &buffer_size);
            if (status == HSM_UTIL_ERROR)
            {
                result = NULL;
                errors_found = true;
                break;
            }
            else
            {
                accumulated_size_check = accumulated_size + buffer_size;
                if (accumulated_size_check < accumulated_size)
                {
                    LOG_ERROR("Concatenated file sizes too large");
                    result = NULL;
                    errors_found = true;
                    break;
                }
                accumulated_size = accumulated_size_check;
            }
        }
        if (!errors_found)
        {
            // add one more for null term
            accumulated_size_check = accumulated_size + 1;
            if (accumulated_size_check < accumulated_size)
            {
                LOG_ERROR("Concatenated file sizes too large");
                result = NULL;
            }
            else
            {
                accumulated_size = accumulated_size_check;
                if ((result = (char*)malloc(accumulated_size)) == NULL)
                {
                    LOG_ERROR("Could not allocate memory to store the concatenated files");
                }
                else
                {
                    result[0] = 0;
                    for (index = 0; index < num_files; index++)
                    {
                        char *temp_result = read_file_into_cstring(file_names[index], NULL);
                        if (temp_result != NULL)
                        {
                            // todo optimize for inplace concat
                            (void)strncat(result, temp_result, accumulated_size);
                            free(temp_result);
                        }
                    }
                }
            }
        }
    }
    return result;
}

bool is_file_valid(const char* file_name)
{
    bool result = false;
    if (file_name != NULL)
    {
        FILE *file_handle;
        if ((file_handle = fopen(file_name, "r")) != NULL)
        {
            fclose(file_handle);
            result = true;
        }
    }
    return result;
}

bool is_directory_valid(const char* dir_path)
{
    bool result = false;
    if (dir_path != NULL)
    {
        struct stat info;
        if (stat(dir_path, &info) == 0)
        {
            if (S_ISDIR(info.st_mode))
            {
                result = true;
            }
        }
    }
    return result;
}

int write_cstring_to_file(const char* file_name, const char* data)
{
    int result;

    if ((file_name == NULL) || (strlen(file_name) == 0))
    {
        LOG_ERROR("Invalid file name parameter");
        result = __FAILURE__;
    }
    else if (data == NULL)
    {
        LOG_ERROR("Invalid data parameter");
        result = __FAILURE__;
    }
    else
    {
        result = write_ascii_buffer_into_file(file_name, data, strlen(data), false);
    }

    return result;
}

int write_buffer_to_file
(
    const char *file_name,
    const unsigned char *data,
    size_t data_size,
    bool make_private
)
{
    int result;

    if ((file_name == NULL) || (strlen(file_name) == 0))
    {
        LOG_ERROR("Invalid file name parameter");
        result = __FAILURE__;
    }
    else if (data == NULL)
    {
        LOG_ERROR("Invalid data parameter");
        result = __FAILURE__;
    }
    else if (data_size == 0)
    {
        LOG_ERROR("Invalid data size parameter");
        result = __FAILURE__;
    }
    else
    {
        result = write_ascii_buffer_into_file(file_name, data, data_size, make_private);
    }

    return result;
}

int delete_file(const char* file_name)
{
    int result;

    if ((file_name == NULL) || (strlen(file_name) == 0))
    {
        LOG_ERROR("Invalid file name");
        result = __FAILURE__;
    }
    else
    {
        #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        if (DeleteFileA(file_name) == 0)
        {
            LOG_ERROR("Failed to delete file %s. GetLastError=%08x", file_name, GetLastError());
            result = __FAILURE__;
        }
        #else
        if (remove(file_name) != 0)
        {
            LOG_ERROR("Failed to delete file %s. Errno: %s.", file_name, strerror(errno));
            result = __FAILURE__;
        }
        #endif
        else
        {
            result = 0;
        }

    }

    return result;
}

int make_dir(const char* dir_path)
{
    int result = __FAILURE__;
    if (dir_path != NULL)
    {
        if (HSM_MKDIR(dir_path) != 0)
        {
            if (errno == EEXIST)
            {
                LOG_DEBUG("Directory '%s' already exists.", dir_path);
                result = 0;
            }
            else
            {
                LOG_ERROR("Directory create failed for '%s'. Errno: %s.", dir_path, strerror(errno));
                result = __FAILURE__;
            }
        }
        else
        {
            result = 0;
        }
    }
    return result;
}
