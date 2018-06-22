// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_data.h"
#include "hsm_log.h"

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
    LOG_ERROR("API unsupported");
    return (HSM_CLIENT_HANDLE)NULL;
}

void iothub_x509_hsm_destroy(HSM_CLIENT_HANDLE handle)
{
    (void)handle;
    LOG_ERROR("API unsupported");
}

char* iothub_x509_hsm_get_certificate(HSM_CLIENT_HANDLE handle)
{
    (void)handle;
    LOG_ERROR("API unsupported");
    return NULL;
}

char* iothub_x509_hsm_get_alias_key(HSM_CLIENT_HANDLE handle)
{
    (void)handle;
    LOG_ERROR("API unsupported");
    return NULL;
}

char* iothub_x509_hsm_get_common_name(HSM_CLIENT_HANDLE handle)
{
    (void)handle;
    LOG_ERROR("API unsupported");
    return NULL;
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
