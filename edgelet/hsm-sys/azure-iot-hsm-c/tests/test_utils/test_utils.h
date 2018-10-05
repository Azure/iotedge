// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef HSM_TEST_UTILS_H
#define HSM_TEST_UTILS_H

#ifdef __cplusplus
extern "C" {
#endif

size_t get_max_file_path_size(void);
char* hsm_test_util_create_temp_dir(char **dir_guid);
void hsm_test_util_delete_dir(const char *dir_guid);
void hsm_test_util_setenv(const char *key, const char *value);
void hsm_test_util_unsetenv(const char *key);

#ifdef __cplusplus
}
#endif

#endif //HSM_TEST_UTILS_H
