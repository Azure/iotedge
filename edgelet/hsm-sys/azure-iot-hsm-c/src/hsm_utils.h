#ifndef HSM_UTILS_H
#define HSM_UTILS_H

#include <stddef.h>
#include "azure_c_shared_utility/umock_c_prod.h"

MOCKABLE_FUNCTION(, char*, concat_files_to_cstring, const char **, file_names, int, num_files);
MOCKABLE_FUNCTION(, char*, read_file_into_cstring, const char*, file_name, size_t*, output_buffer_size);
MOCKABLE_FUNCTION(, void*, read_file_into_buffer, const char*, file_name, size_t*, output_buffer_size);
MOCKABLE_FUNCTION(, bool, is_file_valid, const char*, file_name);
MOCKABLE_FUNCTION(, bool, is_directory_valid, const char*, dir_path);
MOCKABLE_FUNCTION(, int, write_cstring_to_file, const char*, file_name, const char*, data);
MOCKABLE_FUNCTION(, int, write_buffer_to_file, const char*, file_name, const unsigned char*, data, size_t, data_size, bool, make_private);
MOCKABLE_FUNCTION(, int, delete_file, const char*, file_name);
MOCKABLE_FUNCTION(, int, make_dir, const char*, dir_path);
MOCKABLE_FUNCTION(, int, hsm_get_env, const char*, key, char**, output);

#endif  //HSM_UTILS_H
