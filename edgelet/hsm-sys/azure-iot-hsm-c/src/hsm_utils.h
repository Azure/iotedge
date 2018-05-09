#ifndef HSM_UTILS_H
#define HSM_UTILS_H

#include <stddef.h>
#include "azure_c_shared_utility/umock_c_prod.h"

MOCKABLE_FUNCTION(, char*, concat_files_to_cstring, char *const *, file_names, int, num_files);
MOCKABLE_FUNCTION(, char*, read_file_into_cstring, const char*, file_name, size_t*, output_buffer_size);
MOCKABLE_FUNCTION(, void*, read_file_into_buffer, const char*, file_name, size_t*, output_buffer_size);
MOCKABLE_FUNCTION(, bool, is_file_valid, const char*, file_name);
MOCKABLE_FUNCTION(, bool, is_directory_valid, const char*, dir_path);

#endif  //HSM_UTILS_H
