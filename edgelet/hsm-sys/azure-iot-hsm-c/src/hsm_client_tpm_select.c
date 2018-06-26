// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include <stdlib.h>
#include <ctype.h>
#include <stdbool.h>
#include "hsm_utils.h"
#include "hsm_log.h"
#include "hsm_client_tpm_device.h"
#include "hsm_client_tpm_in_mem.h"

extern const char* const ENV_TPM_SELECT;

static int strcmp_i(const char* lhs, const char* rhs)
{
    char lc, rc;
    int cmp = 0;
    do
    {
        lc = *lhs++;
        rc = *rhs++;
        if ((tolower(lc) - tolower(rc)) != 0)
        {
            cmp = 1;
        }
    } while (lc != 0 && rc != 0);

    return cmp;
}

// IF ENV_TPM_SELECT is set and not empty, "NO", "OFF" or "FALSE", then user wants to use the
// TPM device for TPM functionality.
static int use_tpm_device(bool *use_tpm)
{
    static const char * user_says_no[] = { "", "off", "no", "false" };
    int array_size = sizeof(user_says_no)/sizeof(user_says_no[0]);
    int result;
    char * env_use_tpm;

    *use_tpm = false;
    if (hsm_get_env(ENV_TPM_SELECT, &env_use_tpm) != 0)
    {
        LOG_ERROR("Could not lookup env variable %s", ENV_TPM_SELECT);
        result = __FAILURE__;
    }
    else
    {
        if (env_use_tpm != NULL)
        {
            *use_tpm = true;
            for(int no = 0; no < array_size; no++)
            {
                if (strcmp_i(env_use_tpm, user_says_no[no]) == 0)
                {
                    *use_tpm = false;
                    break;
                }
            }
            free(env_use_tpm);
        }
        else
        {
            *use_tpm = false;
        }
        result = 0;
    }

    return result;
}

static bool g_use_tpm_device = false;

int hsm_client_tpm_init(void)
{
    int result;
    bool use_tpm_flag = false;

    if (use_tpm_device(&use_tpm_flag) != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        if (use_tpm_flag)
        {
            result = hsm_client_tpm_device_init();
            if (result == 0)
            {
                g_use_tpm_device = true;
            }
        }
        else
        {
            result = hsm_client_tpm_store_init();
        }
    }

    return result;
}

void hsm_client_tpm_deinit(void)
{
    if (g_use_tpm_device)
    {
        hsm_client_tpm_device_deinit();
    }
    else
    {
        hsm_client_tpm_store_deinit();
    }
}

const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_interface(void)
{
    const HSM_CLIENT_TPM_INTERFACE* result;
    if (g_use_tpm_device)
    {
        result = hsm_client_tpm_device_interface();
    }
    else
    {
        result = hsm_client_tpm_store_interface();
    }
    return result;
}
