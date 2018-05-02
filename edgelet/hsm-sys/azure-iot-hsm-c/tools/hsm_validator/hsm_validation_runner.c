// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>

#include "v0_0_1/hsm_v0_0_1_validation.h"
#include "v0_0_2/hsm_v0_0_2_validation.h"

typedef int (*HSM_CLIENT_VALIDATE_ENTRY_POINT)(void);
typedef struct VALIDATE_INFO
{
    const char* name;
    HSM_CLIENT_VALIDATE_ENTRY_POINT entrypoint;
} VALIDATE_INFO;

// List of validation functions that shall be called in order
const VALIDATE_INFO validation_list[] = 
{
    { "HSM SDK validation v001", iothub_sdk_v001_validation },
    { "HSM SDK validation v002", iothub_sdk_v002_validation }
};


int main(void)
{
    int failed_count = 0;

    size_t index;
    size_t list_len = sizeof(validation_list) / sizeof(validation_list[0]);

    for (index = 0; index < list_len; index++)
    {
        (void)printf("\n%s\n", validation_list[index].name);
        failed_count += validation_list[index].entrypoint();
    }

    (void)printf("\nHSM validation %s\n", (failed_count == 0 ? "passed" : "encountered failures"));
    return failed_count;
}
