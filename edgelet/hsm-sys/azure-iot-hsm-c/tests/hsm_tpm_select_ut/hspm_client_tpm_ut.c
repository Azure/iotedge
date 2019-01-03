// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/sastoken.h"
#include "azure_c_shared_utility/urlencode.h"
#include "azure_c_shared_utility/hmacsha256.h"
#include "azure_c_shared_utility/base64.h"
#include "azure_c_shared_utility/agenttime.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/xlogging.h"
#include "azure_c_shared_utility/crt_abstractions.h"

#include "hsm_client_data.h"

//#############################################################################
// Test defines and data
//#############################################################################
#define TEST_DATA_TO_BE_SIGNED "The quick brown fox jumped over the lazy dog"
#define TEST_KEY_BASE64 "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA="
#define TEST_HOSTNAME  "somehost.azure-devices.net"
#define TEST_DEVICE_ID "some-device-id"
#define TEST_MODULE_ID "some-module-id"
#define TEST_GEN_ID "1"
#define PRIMARY_URI "primary"
#define SECONDARY_URI "secondary"

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;

extern const char* const ENV_TPM_SELECT;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_homedir(void)
{
    TEST_IOTEDGE_HOMEDIR = hsm_test_util_create_temp_dir(&TEST_IOTEDGE_HOMEDIR_GUID);
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR_GUID, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(TEST_IOTEDGE_HOMEDIR, "Line:" TOSTRING(__LINE__));

    printf("Temp dir created: [%s]\r\n", TEST_IOTEDGE_HOMEDIR);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);
}

static void test_helper_teardown_homedir(void)
{
    if ((TEST_IOTEDGE_HOMEDIR != NULL) && (TEST_IOTEDGE_HOMEDIR_GUID != NULL))
    {
        hsm_test_util_delete_dir(TEST_IOTEDGE_HOMEDIR_GUID);
        free(TEST_IOTEDGE_HOMEDIR);
        TEST_IOTEDGE_HOMEDIR = NULL;
        free(TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_IOTEDGE_HOMEDIR_GUID = NULL;
    }
}

const HSM_CLIENT_TPM_INTERFACE * init_get_if_deinit(void)
{
    int status;
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
    const HSM_CLIENT_TPM_INTERFACE* interface = hsm_client_tpm_interface();
    hsm_client_tpm_deinit();
    return interface;
}


//#############################################################################
// Test functions
//#############################################################################

BEGIN_TEST_SUITE(edge_hsm_sas_auth_int_tests)
    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();

        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_STORE_INTERFACE, void*);

    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        test_helper_teardown_homedir();
        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(hsm_tpm_select_no_tpm_false)
    {
        // arrange
        static const char * user_says_no[] = { "",
                                               "off", "OFF", "Off",
                                               "no", "NO", "No",
                                               "false", "FALSE", "False" };
        int array_size = sizeof(user_says_no)/sizeof(user_says_no[0]);
        int status = hsm_test_util_unsetenv(ENV_TPM_SELECT);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        const HSM_CLIENT_TPM_INTERFACE * no_tpm =  init_get_if_deinit();
        // act
        // assert
        for(int no = 0; no < array_size; no++)
        {
            int status = hsm_test_util_setenv(ENV_TPM_SELECT, user_says_no[no]);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL(const HSM_CLIENT_TPM_INTERFACE *,
                                      no_tpm, init_get_if_deinit(),
                                      "Line:" TOSTRING(__LINE__));
        }
        // cleanup
    }

    TEST_FUNCTION(hsm_tpm_select_tpm_true)
    {
        // arrange
        static const char * user_says_yes[] = { "yes", "YES", "Yes",
                                                "on", "ON", "On",
                                                "true", "TRUE", "True",
                                                "Like CMAKE, it's anything that's not assocated with false",
                                                "plugh" };
        int array_size = sizeof(user_says_yes)/sizeof(user_says_yes[0]);
        int status = hsm_test_util_unsetenv(ENV_TPM_SELECT);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
        const HSM_CLIENT_TPM_INTERFACE * no_tpm =  init_get_if_deinit();
        // act
        // assert
        for(int yes = 0; yes < array_size; yes++)
        {
            int status = hsm_test_util_setenv(ENV_TPM_SELECT, user_says_yes[yes]);
            ASSERT_ARE_EQUAL(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_NOT_EQUAL(const HSM_CLIENT_TPM_INTERFACE *,
                                          no_tpm, init_get_if_deinit(),
                                          "Line:" TOSTRING(__LINE__));
        }
        // cleanup
    }


END_TEST_SUITE(edge_hsm_sas_auth_int_tests)
