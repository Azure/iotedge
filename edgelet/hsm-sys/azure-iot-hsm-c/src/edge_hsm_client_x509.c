// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_data.h"

static const char* const COMMON_NAME = "iothub-hsm-example";
static const char* const CERTIFICATE = "-----BEGIN CERTIFICATE-----""\n"
"MIIBbDCCARGgAwIBAgIDIj6BMAoGCCqGSM49BAMCMD4xEjAQBgNVBAoMCW1pY3Jv""\n"
"c29mdDELMAkGA1UEBhMCVVMxGzAZBgNVBAMMEmlvdGh1Yi1oc20tZXhhbXBsZTAe""\n"
"Fw0xODAzMjExNTI3MzJaFw0xODA0MjAxNTI3MzJaMD4xEjAQBgNVBAoMCW1pY3Jv""\n"
"c29mdDELMAkGA1UEBhMCVVMxGzAZBgNVBAMMEmlvdGh1Yi1oc20tZXhhbXBsZTBZ""\n"
"MBMGByqGSM49AgEGCCqGSM49AwEHA0IABPKKgaOHPnUH1iPI6+PCSoU1rc9tbXMa""\n"
"U6vhyNIsijIyE2uBkWKMAAL6SHdJNeRGj/d+zxzsqIuIPDEV+alwNfQwCgYIKoZI""\n"
"zj0EAwIDSQAwRgIhAMf0x3q2TlmLy9RixcANJC8UiK3mnoTApY8LVL1Mn5KiAiEA""\n"
"9RI4qMtjYPsvCREcR8aOoSdo9f+MNUz3sGQBDLmlRv0=""\n"
"-----END CERTIFICATE-----";
static const char* const PRIVATE_KEY = "-----BEGIN PRIVATE KEY-----""\n"
"MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg+oE7K7T/aNysJhi8""\n"
"WHjLK5CSxnq4V+G9NhMxjSgeLQihRANCAATyioGjhz51B9YjyOvjwkqFNa3PbW1z""\n"
"GlOr4cjSLIoyMhNrgZFijAAC+kh3STXkRo/3fs8c7KiLiDwxFfmpcDX0""\n"
"-----END PRIVATE KEY-----";

int hsm_client_x509_init()
{
    return 0;
}

void hsm_client_x509_deinit()
{
}

void iothub_hsm_free_buffer(void * buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

HSM_CLIENT_HANDLE iothub_x509_hsm_create()
{
    HSM_CLIENT_HANDLE result;
    result = (HSM_CLIENT_HANDLE)0x12345;
    return (HSM_CLIENT_HANDLE)result;
}

void iothub_x509_hsm_destroy(HSM_CLIENT_HANDLE handle)
{
}

char* iothub_x509_hsm_get_certificate(HSM_CLIENT_HANDLE handle)
{
    char* result;
    if (handle == NULL)
    {
        (void)printf("Invalid handle value specified");
        result = NULL;
    }
    else
    {
        size_t len = strlen(CERTIFICATE);
        if ((result = (char*)malloc(len + 1)) == NULL)
        {
            (void)printf("Failure allocating certificate\r\n");
            result = NULL;
        }
        else
        {
            strcpy(result, CERTIFICATE);
        }
    }
    return result;
}

char* iothub_x509_hsm_get_alias_key(HSM_CLIENT_HANDLE handle)
{
    char* result;
    if (handle == NULL)
    {
        (void)printf("Invalid handle value specified");
        result = NULL;
    }
    else
    {
        size_t len = strlen(PRIVATE_KEY);
        if ((result = (char*)malloc(len + 1)) == NULL)
        {
            (void)printf("Failure allocating certificate\r\n");
            result = NULL;
        }
        else
        {
            strcpy(result, PRIVATE_KEY);
        }
    }
    return result;
}

char* iothub_x509_hsm_get_common_name(HSM_CLIENT_HANDLE handle)
{
    char* result;
    if (handle == NULL)
    {
        (void)printf("Invalid handle value specified");
        result = NULL;
    }
    else
    {
        size_t len = strlen(COMMON_NAME);
        if ((result = (char*)malloc(len + 1)) == NULL)
        {
            (void)printf("Failure allocating certificate\r\n");
            result = NULL;
        }
        else
        {
            strcpy(result, COMMON_NAME);
        }
    }
    return result;
}

static const HSM_CLIENT_X509_INTERFACE x509_interface =
{
    iothub_x509_hsm_create,
    iothub_x509_hsm_destroy,
    iothub_x509_hsm_get_certificate,
    iothub_x509_hsm_get_alias_key,
    iothub_x509_hsm_get_common_name,
    iothub_hsm_free_buffer
};

const HSM_CLIENT_X509_INTERFACE* hsm_client_x509_interface()
{
    return &x509_interface;
}
