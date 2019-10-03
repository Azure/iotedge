// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>

#include "test_utils.h"
#include "hsm_client_data.h"

static int validate_hsm_init_library(void)
{
    int result = 0;
#ifdef USE_X509_INTERFACE
    if (hsm_client_x509_init(1000) != 0)
    {
        (void)printf("Failure calling hsm_client_x509_init\r\n");
        result = __LINE__;
    }
#endif
#ifdef USE_TPM_INTERFACE
    if (hsm_client_tpm_init() != 0)
    {
        (void)printf("Failure calling hsm_client_tpm_init\r\n");
        result = __LINE__;
    }
#endif
    return result;
}

static void validate_hsm_deinit_library(void)
{
#ifdef USE_X509_INTERFACE
    hsm_client_x509_deinit();
#endif
#ifdef USE_TPM_INTERFACE
    hsm_client_tpm_deinit();
#endif
}

static int validate_hsm_x509_interface(void)
{
    int result;
    const HSM_CLIENT_X509_INTERFACE* x509_interface = hsm_client_x509_interface();
    if (x509_interface == NULL)
    {
        (void)printf("NULL value encountered calling hsm_client_x509_interface\r\n");
        result = __LINE__;
    }
    else
    {
        HSM_CLIENT_HANDLE hsm_handle;
        char* certificate = NULL;
        char* key = NULL;
        char* common_name = NULL;
        if (x509_interface->hsm_client_x509_create == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_x509_create\r\n");
            result = __LINE__;
        }
        else if ((hsm_handle = x509_interface->hsm_client_x509_create() ) == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_x509_interface\r\n");
            result = __LINE__;
        }
        else if (x509_interface->hsm_client_get_cert == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_get_cert\r\n");
            result = __LINE__;
        }
        else if ((certificate = x509_interface->hsm_client_get_cert(hsm_handle)) == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_get_cert\r\n");
            result = __LINE__;
        }
        else if (x509_interface->hsm_client_get_key == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_get_key\r\n");
            result = __LINE__;
        }
        else if ((key = x509_interface->hsm_client_get_key(hsm_handle)) == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_get_key\r\n");
            result = __LINE__;
        }
        else if (x509_interface->hsm_client_get_common_name == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_get_common_name\r\n");
            result = __LINE__;
        }
        else if ((common_name = x509_interface->hsm_client_get_common_name(hsm_handle)) == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_get_common_name\r\n");
            result = __LINE__;
        }
        else if (x509_interface->hsm_client_x509_destroy == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_x509_destroy\r\n");
            result = __LINE__;
        }
        else
        {
            x509_interface->hsm_client_x509_destroy(hsm_handle);
            result = 0;
        }
        free(certificate);
        free(key);
        free(common_name);
    }
    return result;
}

static int validate_hsm_tpm_interface(void)
{
    int result;
    const HSM_CLIENT_TPM_INTERFACE* tpm_interface = hsm_client_tpm_interface();
    if (tpm_interface == NULL)
    {
        (void)printf("NULL value encountered calling hsm_client_x509_interface\r\n");
        result = __LINE__;
    }
    else
    {
        HSM_CLIENT_HANDLE hsm_handle;
        unsigned char* ek = NULL;
        size_t ek_length;
        unsigned char* srk = NULL;
        size_t srk_length;
        unsigned char activate_identity[] = { 0x10 };
        size_t identity_length = sizeof(activate_identity) / sizeof(activate_identity[0]);
        const unsigned char identity_data[] = { 0x68, 0x73, 0x6d, 0x20, 0x76, 0x61, 0x6c, 0x69, 0x64, 0x61, 0x74, 0x6f, 0x72 };
        size_t data_length = sizeof(identity_data) / sizeof(identity_data[0]);
        unsigned char* sign_result = NULL;
        size_t sign_data;


        if (tpm_interface->hsm_client_tpm_create == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_tpm_create\r\n");
            result = __LINE__;
        }
        else if ((hsm_handle = tpm_interface->hsm_client_tpm_create()) == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_tpm_create\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_get_ek == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_get_ek\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_get_ek(hsm_handle, &ek, &ek_length) != 0)
        {
            (void)printf("nonzero value encountered calling hsm_client_get_ek\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_get_srk == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_get_srk\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_get_srk(hsm_handle, &srk, &srk_length) != 0)
        {
            (void)printf("nonzero value encountered calling hsm_client_get_srk\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_activate_identity_key == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_activate_identity_key\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_activate_identity_key(hsm_handle, activate_identity, identity_length) != 0)
        {
            (void)printf("nonzero value encountered calling hsm_client_activate_identity_key\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_sign_with_identity == NULL)
        {
            (void)printf("NULL interface pointer encountered on hsm_client_sign_with_identity\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_sign_with_identity(hsm_handle, identity_data, data_length, &sign_result, &sign_data) != 0)
        {
            (void)printf("nonzero value encountered calling hsm_client_sign_with_identity\r\n");
            result = __LINE__;
        }
        else if (tpm_interface->hsm_client_tpm_destroy == NULL)
        {
            (void)printf("NULL value encountered calling hsm_client_tpm_destroy\r\n");
            result = __LINE__;
        }
        else
        {
            tpm_interface->hsm_client_tpm_destroy(hsm_handle);
            result = 0;
        }
        free(ek);
        free(srk);
        free(sign_result);
    }
    return result;
}

int iothub_sdk_v001_validation(void)
{
    INIT_RECORD;

    RECORD(validate_hsm_init_library());

#ifdef USE_X509_INTERFACE
    RECORD(validate_hsm_x509_interface());
#endif // USE_X509_INTERFACE

#ifdef USE_TPM_INTERFACE
    RECORD(validate_hsm_tpm_interface());
#endif // USE_TPM_INTERFACE

    validate_hsm_deinit_library();

    PRINT_RECORD;

    RETURN_FAILED_RECORD;
}
