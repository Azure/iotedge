// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#include "hsm_client_tpm_device.h"
#include "hsm_client_tpm_in_mem.h"

int hsm_client_tpm_init(void)
{
    int result;
    #ifdef TEST_TPM_INTERFACE_IN_MEM
        result = hsm_client_tpm_store_init();
    #else
        result = hsm_client_tpm_device_init();
    #endif

    return result;
}

void hsm_client_tpm_deinit(void)
{
    #ifdef TEST_TPM_INTERFACE_IN_MEM
        hsm_client_tpm_store_deinit();
    #else
        hsm_client_tpm_device_deinit();
    #endif
}

const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_interface(void)
{
    const HSM_CLIENT_TPM_INTERFACE* result;
    #ifdef TEST_TPM_INTERFACE_IN_MEM
        result = hsm_client_tpm_store_interface();
    #else
        result = hsm_client_tpm_device_interface();
    #endif

    return result;
}
