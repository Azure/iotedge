// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifdef __cplusplus
#include <cstdlib>
#include <cstdint>
#include <cstddef>
#else
#include <stdlib.h>
#include <stdint.h>
#include <stddef.h>
#endif

static void* my_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void my_gballoc_free(void* ptr)
{
    free(ptr);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umocktypes_charptr.h"
#include "umocktypes_stdint.h"
#include "umocktypes_bool.h"
#include "umock_c_negative_tests.h"
#include "azure_c_shared_utility/macro_utils.h"

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/umock_c_prod.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/sastoken.h"
#include "azure_c_shared_utility/base64.h"
#include "azure_c_shared_utility/sha.h"
#include "azure_c_shared_utility/urlencode.h"

#include "azure_utpm_c/tpm_codec.h"
#include "azure_utpm_c/Marshal_fp.h"

#include "edge_sas_perform_sign_with_key.h"

#include "azure_utpm_c/TpmTypes.h"
#undef ENABLE_MOCKS

#include "hsm_client_data.h"
#include "hsm_log.h"
#include "hsm_client_tpm_device.h"

static const char* TEST_STRING_VALUE = "Test_String_Value";
static const unsigned char TEST_IMPORT_KEY[] = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10 };

static const char* TEST_RSA_KEY = "1234567890";
static unsigned char TEST_BUFFER[128];
static unsigned char IDENTITY_BUFFER[128];

static uint16_t g_rsa_size;

#define TEST_BUFFER_SIZE     128
#define IDENTITY_BUFFER_SIZE 128
#define TEST_KEY_SIZE        10

static void my_STRING_delete(STRING_HANDLE h)
{
    my_gballoc_free((void*)h);
}

static TPM_HANDLE my_TSS_CreatePersistentKey(TSS_DEVICE* tpm_device, TPM_HANDLE request_handle, TSS_SESSION* sess, TPMI_DH_OBJECT hierarchy, TPM2B_PUBLIC* inPub, TPM2B_PUBLIC* outPub)
{
    (void)tpm_device;
    (void)request_handle;
    (void)sess;
    (void)hierarchy;
    (void)inPub;

    (*outPub).publicArea.unique.rsa.t.size = g_rsa_size;
    return (TPM_HANDLE)0x1;
}

static TPM_RC my_TSS_CreatePrimary(TSS_DEVICE *tpm, TSS_SESSION *sess, TPM_HANDLE hierarchy, TPM2B_PUBLIC *inPub, TPM_HANDLE *outHandle, TPM2B_PUBLIC *outPub)
{
    (void)tpm;
    (void)sess;
    (void)hierarchy;
    (void)inPub;
    (void)outHandle;

    (*outPub).publicArea.unique.rsa.t.size = g_rsa_size;
    return TPM_RC_SUCCESS;
}

static int my_mallocAndStrcpy_s(char** destination, const char* source)
{
    (void)source;
    size_t src_len = strlen(source);
    *destination = (char*)my_gballoc_malloc(src_len + 1);
    strcpy(*destination, source);
    return 0;
}

static int my_perform_sign_with_key( const unsigned char* key, size_t key_len,
                                     const unsigned char* data_to_be_signed, size_t data_to_be_signed_size,
                                     unsigned char** digest, size_t* digest_size)
{
    (void)key;
    (void)key_len;
    (void)data_to_be_signed;
    (void)data_to_be_signed_size;
    *digest = my_gballoc_malloc(1);
    *digest_size = 1;
    return 0;
}

/*static BUFFER_HANDLE my_Base64_Decoder(const char* source)
{
    (void)source;
    return (BUFFER_HANDLE)my_gballoc_malloc(1);
}*/

STRING_HANDLE my_Base64_Encode_Bytes(const unsigned char* source, size_t size)
{
    (void)source;
    (void)size;
    return (STRING_HANDLE)my_gballoc_malloc(1);
}

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static void on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

BEGIN_TEST_SUITE(hsm_client_tpm_ut)

    TEST_SUITE_INITIALIZE(suite_init)
    {
        int result;

        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        (void)umock_c_init(on_umock_c_error);

        result = umocktypes_charptr_register_types();
        ASSERT_ARE_EQUAL(int, 0, result);
        result = umocktypes_stdint_register_types();
        ASSERT_ARE_EQUAL(int, 0, result);
        result = umocktypes_bool_register_types();
        ASSERT_ARE_EQUAL(int, 0, result);

        REGISTER_UMOCK_ALIAS_TYPE(XDA_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(BUFFER_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPM_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(UINT, unsigned int);
        REGISTER_UMOCK_ALIAS_TYPE(UINT32, unsigned int);
        REGISTER_UMOCK_ALIAS_TYPE(BOOL, int);
        REGISTER_UMOCK_ALIAS_TYPE(TPM_PT, unsigned int);

        REGISTER_UMOCK_ALIAS_TYPE(HSM_CLIENT_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(SECURE_DEVICE_TYPE, int);
        REGISTER_UMOCK_ALIAS_TYPE(STRING_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(OBJECT_ATTR, int);
        REGISTER_UMOCK_ALIAS_TYPE(TPM_SE, int);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_DH_OBJECT, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_ALG_HASH, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPMA_SESSION, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_DH_ENTITY, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_DH_CONTEXT, void*);
        REGISTER_UMOCK_ALIAS_TYPE(INT32, int);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_RH_PROVISION, void*);
        REGISTER_UMOCK_ALIAS_TYPE(TPMI_DH_PERSISTENT, void*);

        REGISTER_GLOBAL_MOCK_RETURN(TSS_CreatePwAuthSession, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_CreatePwAuthSession, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(Initialize_TPM_Codec, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(Initialize_TPM_Codec, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_ReadPublic, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_ReadPublic, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_HOOK(TSS_CreatePrimary, my_TSS_CreatePrimary);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_CreatePrimary, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TSS_Create, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_Create, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TSS_GetTpmProperty, 1028);

        REGISTER_GLOBAL_MOCK_RETURN(TPM2B_PUBLIC_Marshal, 1);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2B_PUBLIC_Marshal, 1025)

        REGISTER_GLOBAL_MOCK_HOOK(TSS_CreatePersistentKey, my_TSS_CreatePersistentKey);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_CreatePersistentKey, 0);

        REGISTER_GLOBAL_MOCK_RETURN(TSS_StartAuthSession, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_StartAuthSession, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TSS_PolicySecret, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TSS_PolicySecret, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_ActivateCredential, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_ActivateCredential, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_Import, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_Import, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_Load, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_Load, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_EvictControl, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_EvictControl, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_FlushContext, TPM_RC_SUCCESS);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_FlushContext, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(TPM2_ReadPublic, TPM_RC_HANDLE);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(TPM2_ReadPublic, TPM_RC_FAILURE);
        REGISTER_GLOBAL_MOCK_RETURN(SignData, TEST_BUFFER_SIZE);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(SignData, 0);

        REGISTER_GLOBAL_MOCK_RETURN(STRING_c_str, TEST_STRING_VALUE);
        REGISTER_GLOBAL_MOCK_HOOK(STRING_delete, my_STRING_delete);

        REGISTER_GLOBAL_MOCK_HOOK(Base64_Encode_Bytes, my_Base64_Encode_Bytes);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(Base64_Encode_Bytes, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);
        REGISTER_GLOBAL_MOCK_HOOK(mallocAndStrcpy_s, my_mallocAndStrcpy_s);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(mallocAndStrcpy_s, __LINE__);

        REGISTER_GLOBAL_MOCK_HOOK(perform_sign_with_key, my_perform_sign_with_key);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(perform_sign_with_key, 1);

        for (size_t index = 0; index < 10; index++)
        {
            TEST_BUFFER[index] = (unsigned char)(index+1);
        }
    }

    TEST_SUITE_CLEANUP(suite_cleanup)
    {
        umock_c_deinit();

        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(method_init)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Could not acquire test serialization mutex.");
        }
        umock_c_reset_all_calls();
        g_rsa_size = TEST_KEY_SIZE;
    }

    TEST_FUNCTION_CLEANUP(method_cleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    static int should_skip_index(size_t current_index, const size_t skip_array[], size_t length)
    {
        int result = 0;
        for (size_t index = 0; index < length; index++)
        {
            if (current_index == skip_array[index])
            {
                result = __LINE__;
                break;
            }
        }
        return result;
    }

    static void setup_hsm_client_tpm_create_mock()
    {
        OBJECT_ATTR tmp = FixedTPM;
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(TSS_CreatePwAuthSession(IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(Initialize_TPM_Codec(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(ToTpmaObject(tmp))
            .IgnoreArgument_attrs();
        STRICT_EXPECTED_CALL(TSS_CreatePersistentKey(IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(ToTpmaObject(tmp))
            .IgnoreArgument_attrs();
        STRICT_EXPECTED_CALL(TSS_CreatePersistentKey(IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
    }

    static void setup_hsm_client_tpm_activate_key_mock()
    {
        OBJECT_ATTR tmp = FixedTPM;
        TPM_SE tmp_se = 0;
        TPMA_SESSION tmp_session = { 0 };
        TPMI_DH_OBJECT tmp_dh = { 0 };
        TPMI_RH_PROVISION tmp_prov = { 0 };
        TPMI_DH_CONTEXT tmp_dh_ctx = { 0 };
        TPMI_DH_PERSISTENT tmp_dh_per = { 0 };

        STRICT_EXPECTED_CALL(TSS_StartAuthSession(IGNORED_PTR_ARG, tmp_se, IGNORED_NUM_ARG, tmp_session, IGNORED_PTR_ARG))
            .IgnoreArgument_sessAttrs()
            .IgnoreArgument_sessionType();
        STRICT_EXPECTED_CALL(TSS_PolicySecret(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG)); // 4

        STRICT_EXPECTED_CALL(TPM2B_ID_OBJECT_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));     // 2
        STRICT_EXPECTED_CALL(TPM2B_ENCRYPTED_SECRET_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(TPM2B_PRIVATE_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(TPM2B_ENCRYPTED_SECRET_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(TPM2B_PUBLIC_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, TRUE));
        STRICT_EXPECTED_CALL(UINT16_Unmarshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));       // 7

        STRICT_EXPECTED_CALL(TPM2_ActivateCredential(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(TPM2_Import(IGNORED_PTR_ARG, IGNORED_PTR_ARG, tmp_dh, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG))
            .IgnoreArgument_parentHandle();

        STRICT_EXPECTED_CALL(ToTpmaObject(tmp)).IgnoreArgument_attrs(); // 10
        STRICT_EXPECTED_CALL(TSS_Create(IGNORED_PTR_ARG, IGNORED_PTR_ARG, tmp_dh, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG))
            .IgnoreAllArguments();

        STRICT_EXPECTED_CALL(TPM2_Load(IGNORED_PTR_ARG, IGNORED_PTR_ARG, tmp_dh, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG))
            .IgnoreArgument_parentHandle();     // 12
        STRICT_EXPECTED_CALL(TPM2_EvictControl(IGNORED_PTR_ARG, IGNORED_PTR_ARG, tmp_prov, tmp_dh, tmp_dh_per))
            .IgnoreArgument_auth()
            .IgnoreArgument_objectHandle()
            .IgnoreArgument_persistentHandle();
        STRICT_EXPECTED_CALL(TPM2_EvictControl(IGNORED_PTR_ARG, IGNORED_PTR_ARG, tmp_prov, tmp_dh, tmp_dh_per))
            .IgnoreArgument_auth()
            .IgnoreArgument_objectHandle()
            .IgnoreArgument_persistentHandle();
        STRICT_EXPECTED_CALL(TPM2_FlushContext(IGNORED_PTR_ARG, tmp_dh_ctx)) // 15
            .IgnoreArgument_flushHandle();
    }

    static void setup_hsm_client_tpm_get_storage_key_mocks()
    {
        STRICT_EXPECTED_CALL(TPM2B_PUBLIC_Marshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, NULL));
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    }

    static void setup_hsm_client_tpm_sign_data_mocks()
    {
        STRICT_EXPECTED_CALL(SignData(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    }

    static void setup_hsm_client_tpm_get_endorsement_key_mocks()
    {
        STRICT_EXPECTED_CALL(TPM2B_PUBLIC_Marshal(IGNORED_PTR_ARG, IGNORED_PTR_ARG, NULL));
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    }

    TEST_FUNCTION(hsm_client_tpm_create_succeed)
    {
        //arrange
        setup_hsm_client_tpm_create_mock();

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();

        //assert
        ASSERT_IS_NOT_NULL(sec_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_create_fail)
    {
        //arrange
        setup_hsm_client_tpm_create_mock();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        umock_c_negative_tests_snapshot();

        size_t calls_cannot_fail[] = { 3, 5 };

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            if (should_skip_index(index, calls_cannot_fail, sizeof(calls_cannot_fail) / sizeof(calls_cannot_fail[0])) != 0)
            {
                continue;
            }

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "secure_device_riot_create failure in test %zu/%zu", index, count);

            //act
            HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();

            //assert
            ASSERT_IS_NULL(sec_handle, tmp_msg);
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_destroy_succeed)
    {
        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        STRICT_EXPECTED_CALL(Deinit_TPM_Codec(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        //act
        tpm_if->hsm_client_tpm_destroy(sec_handle);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_destroy_handle_NULL_succeed)
    {
        //arrange
        umock_c_reset_all_calls();

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        tpm_if->hsm_client_tpm_destroy(NULL);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_activate_key_handle_NULL_fail)
    {
        //arrange

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        int import_res = tpm_if->hsm_client_activate_identity_key(NULL, TEST_IMPORT_KEY, TEST_KEY_SIZE);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, import_res);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_activate_key_key_NULL_fail)
    {
        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int import_res = tpm_if->hsm_client_activate_identity_key(sec_handle, NULL, TEST_KEY_SIZE);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, import_res);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_activate_key_fail)
    {
        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        setup_hsm_client_tpm_activate_key_mock();

        umock_c_negative_tests_snapshot();

        size_t calls_cannot_fail[] = { 2, 3, 4, 5, 6, 7, 10, 13 };

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            if (should_skip_index(index, calls_cannot_fail, sizeof(calls_cannot_fail) / sizeof(calls_cannot_fail[0])) != 0)
            {
                continue;
            }

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "hsm_client_tpm_activate_key failure in test %zu/%zu", index, count);

            //act
            int import_res = tpm_if->hsm_client_activate_identity_key(sec_handle, TEST_IMPORT_KEY, TEST_KEY_SIZE);

            //assert
            ASSERT_ARE_NOT_EQUAL(int, 0, import_res, tmp_msg);
        }
        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_activate_key_succeed)
    {
        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        setup_hsm_client_tpm_activate_key_mock();

        //act
        int import_res = tpm_if->hsm_client_activate_identity_key(sec_handle, TEST_IMPORT_KEY, TEST_KEY_SIZE);

        //assert
        ASSERT_ARE_EQUAL(int, 0, import_res);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_get_endorsement_key_handle_NULL_succeed)
    {
        unsigned char* key;
        size_t key_len;

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        int result = tpm_if->hsm_client_get_ek(NULL, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_get_endorsement_key_size_0_fail)
    {
        g_rsa_size = 0;
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_get_ek(NULL, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_get_endorsement_key_fail)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        setup_hsm_client_tpm_get_endorsement_key_mocks();

        umock_c_negative_tests_snapshot();

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "hsm_client_tpm_get_endorsement_key failure in test %zu/%zu", index, count);

            int result = tpm_if->hsm_client_get_ek(sec_handle, &key, &key_len);

            //assert
            ASSERT_ARE_NOT_EQUAL(int, 0, result, tmp_msg);
        }
        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_get_endorsement_key_succeed)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        setup_hsm_client_tpm_get_endorsement_key_mocks();

        //act
        int result = tpm_if->hsm_client_get_ek(sec_handle, &key, &key_len);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        my_gballoc_free(key);
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_get_storage_key_handle_NULL_fail)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        int result = tpm_if->hsm_client_get_srk(NULL, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_get_storage_key_size_0_fail)
    {
        unsigned char* key;
        size_t key_len;
        g_rsa_size = 0;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_get_srk(NULL, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_get_storage_key_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        setup_hsm_client_tpm_get_storage_key_mocks();

        umock_c_negative_tests_snapshot();

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "hsm_client_tpm_get_storage_key failure in test %zu/%zu", index, count);

            int result = tpm_if->hsm_client_get_srk(sec_handle, &key, &key_len);

            //assert
            ASSERT_ARE_NOT_EQUAL(int, 0, result, tmp_msg);
        }

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_get_storage_key_succeed)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        setup_hsm_client_tpm_get_storage_key_mocks();

        //act
        int result = tpm_if->hsm_client_get_srk(sec_handle, &key, &key_len);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        my_gballoc_free(key);
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_handle_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        int result = tpm_if->hsm_client_sign_with_identity(NULL, TEST_BUFFER, TEST_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_data_NULL_fail)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_sign_with_identity(sec_handle, NULL, TEST_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_size_0_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_sign_with_identity(sec_handle, TEST_BUFFER, 0, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_digest_null_fail)
    {
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE, NULL, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_digest_size_null_fail)
    {
        unsigned char* key;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE, &key, NULL);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        setup_hsm_client_tpm_sign_data_mocks();

        umock_c_negative_tests_snapshot();

        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "hsm_client_tpm_sign_data failure in test %zu/%zu", index, count);

            //act
            int result = tpm_if->hsm_client_sign_with_identity(NULL, TEST_BUFFER, TEST_BUFFER_SIZE, &key, &key_len);

            //assert
            ASSERT_ARE_NOT_EQUAL(int, 0, result, tmp_msg);
        }

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_sign_data_succeed)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        setup_hsm_client_tpm_sign_data_mocks();

        //act
        int result = tpm_if->hsm_client_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        my_gballoc_free(key);
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_handle_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(NULL, TEST_BUFFER, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_data_null_fail)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, NULL, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_data_size_0_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, 0,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_identity_null_fail)
    {
        //arrange
        unsigned char* key;
        size_t key_len;

        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE,
                NULL, IDENTITY_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_identity_size_0_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, 0, &key, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_digest_null_fail)
    {
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, NULL, &key_len);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_sign_derive_and_sign_digest_size_null_fail)
    {
        unsigned char* key;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, NULL);

        //assert
        ASSERT_ARE_NOT_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_fail)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        setup_hsm_client_tpm_sign_data_mocks();

        umock_c_negative_tests_snapshot();

        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "hsm_client_derive_and_sign_with_identity failure in test %zu/%zu", index, count);

            //act
            int result = tpm_if->hsm_client_derive_and_sign_with_identity(NULL, TEST_BUFFER, TEST_BUFFER_SIZE,
                    IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, &key_len);

            //assert
            ASSERT_ARE_NOT_EQUAL(int, 0, result, tmp_msg);
        }

        //cleanup
        tpm_if->hsm_client_tpm_destroy(sec_handle);
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(hsm_client_tpm_derive_and_sign_succeed)
    {
        unsigned char* key;
        size_t key_len;

        //arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        HSM_CLIENT_HANDLE sec_handle = tpm_if->hsm_client_tpm_create();
        umock_c_reset_all_calls();

        STRICT_EXPECTED_CALL(SignData(IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(perform_sign_with_key(IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_NUM_ARG, IGNORED_PTR_ARG, IGNORED_PTR_ARG));

        //act
        int result = tpm_if->hsm_client_derive_and_sign_with_identity(sec_handle, TEST_BUFFER, TEST_BUFFER_SIZE,
                IDENTITY_BUFFER, IDENTITY_BUFFER_SIZE, &key, &key_len);

        //assert
        ASSERT_ARE_EQUAL(int, 0, result);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        my_gballoc_free(key);
        tpm_if->hsm_client_tpm_destroy(sec_handle);
    }

    TEST_FUNCTION(hsm_client_tpm_free_buffer_null_does_nothing)
    {
        // arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();

        // act
        tpm_if->hsm_client_free_buffer(NULL);

        // assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());
        // cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_free_buffer_frees_something)
    {
        // arrange
        const HSM_CLIENT_TPM_INTERFACE* tpm_if = hsm_client_tpm_device_interface();
        void * buffer = my_gballoc_malloc(1);
        umock_c_reset_all_calls();

        STRICT_EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        // act
        tpm_if->hsm_client_free_buffer(buffer);

        // assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());
        // cleanup
    }

    TEST_FUNCTION(hsm_client_tpm_interface_succeed)
    {
        //arrange
        umock_c_reset_all_calls();

        //act
        const HSM_CLIENT_TPM_INTERFACE* tpm_iface = hsm_client_tpm_device_interface();

        //assert
        ASSERT_IS_NOT_NULL(tpm_iface);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_tpm_create);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_tpm_destroy);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_get_ek);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_get_srk);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_activate_identity_key);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_sign_with_identity);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_derive_and_sign_with_identity);
        ASSERT_IS_NOT_NULL(tpm_iface->hsm_client_free_buffer);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    END_TEST_SUITE(hsm_client_tpm_ut)
