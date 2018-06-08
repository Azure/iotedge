// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "hsm_client_data.h"

static const char* const DEVICE_CA_ALIAS = "device_ca_alias";

static const char* const HSM_CLIENT_VERSION = "1.0.0";

const char* hsm_get_device_ca_alias(void)
{
    return DEVICE_CA_ALIAS;
}

const char* hsm_get_version(void)
{
    return HSM_CLIENT_VERSION;
}
