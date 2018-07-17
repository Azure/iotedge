// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdlib.h>
#include <string.h>
#include <stddef.h>

#include "testrunnerswitcher.h"
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

extern const char* const ENV_TPM_SELECT;

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_env(const char *key, const char *val)
{
#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    errno_t status = _putenv_s(key, val);
#else
    int status = setenv(key, val, 1);
#endif
    printf("Env variable %s set to %s\n", key, val);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
}

static void test_helper_unset_env(const char *key)
{
#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    errno_t status = _putenv_s(key, "");
#else
    int status = unsetenv(key);
#endif
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
}

static void test_helper_setup_homedir(void)
{
#if defined(TESTONLY_IOTEDGE_HOMEDIR)
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        errno_t status = _putenv_s("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR);
    #else
        int status = setenv("IOTEDGE_HOMEDIR", TESTONLY_IOTEDGE_HOMEDIR, 1);
    #endif
    printf("IoT Edge home dir set to %s\n", TESTONLY_IOTEDGE_HOMEDIR);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
#else
    #error "Could not find symbol TESTONLY_IOTEDGE_HOMEDIR"
#endif
}

const HSM_CLIENT_TPM_INTERFACE * init_get_if_deinit(void)
{
    int status;
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
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
        int status = test_helper_unset_env(ENV_TPM_SELECT);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        const HSM_CLIENT_TPM_INTERFACE * no_tpm =  init_get_if_deinit();
        // act
        // assert
        for(int no = 0; no < array_size; no++)
        {
            int status = test_helper_setup_env(ENV_TPM_SELECT, user_says_no[no]);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_EQUAL_WITH_MSG(const HSM_CLIENT_TPM_INTERFACE *, 
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
        int status = test_helper_unset_env(ENV_TPM_SELECT);
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        const HSM_CLIENT_TPM_INTERFACE * no_tpm =  init_get_if_deinit();
        // act
        // assert
        for(int yes = 0; yes < array_size; yes++)
        {
            int status = test_helper_setup_env(ENV_TPM_SELECT, user_says_yes[yes]);
            ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            ASSERT_ARE_NOT_EQUAL_WITH_MSG(const HSM_CLIENT_TPM_INTERFACE *, 
                                          no_tpm, init_get_if_deinit(), 
                                          "Line:" TOSTRING(__LINE__));
        }
        // cleanup
    }


END_TEST_SUITE(edge_hsm_sas_auth_int_tests)
