// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "test_utils.h"
#include "hsm_client_data.h"

extern RECORD_RESULTS x509_validation(void);
extern RECORD_RESULTS tpm_validation(void);
extern RECORD_RESULTS crypto_validation(void);

#ifndef USE_X509_INTERFACE
RECORD_RESULTS x509_validation(void) { RETURN_EMPTY_RECORD; }
#endif

#ifndef USE_TPM_INTERFACE
RECORD_RESULTS tpm_validation(void) { RETURN_EMPTY_RECORD; }
#endif

int iothub_sdk_v002_validation(void)
{
    INIT_RECORD;

    ADD_RECORD(x509_validation());
    ADD_RECORD(tpm_validation());
    ADD_RECORD(crypto_validation());

    PRINT_RECORD;

    RETURN_FAILED_RECORD;
}
