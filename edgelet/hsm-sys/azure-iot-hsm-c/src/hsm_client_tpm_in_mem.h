// Copyright (c) Microsoft. All rights reserved.                                                          
// Licensed under the MIT license. See LICENSE file in the project root for full license information.     

#include "hsm_client_data.h"

extern int hsm_client_tpm_store_init(void);
extern void hsm_client_tpm_store_deinit(void);
extern const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_store_interface();
