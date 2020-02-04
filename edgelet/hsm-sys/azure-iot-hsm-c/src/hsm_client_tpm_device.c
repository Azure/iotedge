// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/// Derived fom azure-iot-sdk-c library's provisioning client.
/// https://github.com/Azure/azure-iot-sdk-c/blob/master/provisioning_client/src/secure_device_tpm.c

#include <stdlib.h>
#include <stdbool.h>
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/sastoken.h"
#include "azure_c_shared_utility/sha.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_utpm_c/tpm_comm.h"
#include "azure_utpm_c/tpm_codec.h"
#include "azure_utpm_c/Marshal_fp.h"     // for activation blob unmarshaling

#include "hsm_client_data.h"
#include "hsm_err.h"
#include "hsm_log.h"
#include "edge_sas_perform_sign_with_key.h"


#define EPOCH_TIME_T_VALUE          0
#define HMAC_LENGTH                 32
#define TPM_DATA_LENGTH             1024

static TPM2B_AUTH      NullAuth = { .t = {0,  {0}} };
static TSS_SESSION     NullPwSession;
static const UINT32 TPM_20_SRK_HANDLE = HR_PERSISTENT | 0x00000001;
static const UINT32 TPM_20_EK_HANDLE = HR_PERSISTENT | 0x00010001;
static const UINT32 DPS_ID_KEY_HANDLE = HR_PERSISTENT | 0x00000100;

typedef struct HSM_CLIENT_INFO_TAG
{
    TSS_DEVICE tpm_device;
    TPM2B_PUBLIC ek_pub;
    TPM2B_PUBLIC srk_pub;

    TPM2B_PUBLIC id_key_public;
    TPM2B_PRIVATE id_key_dup_blob;
    TPM2B_PRIVATE id_key_priv;
} HSM_CLIENT_INFO;

static TPMS_RSA_PARMS  RsaStorageParams = {
    { TPM_ALG_AES, {128}, {TPM_ALG_CFB} },              // TPMT_SYM_DEF_OBJECT  symmetric
    { TPM_ALG_NULL,  {.anySig = {ALG_ERROR_VALUE} }},   // TPMT_RSA_SCHEME      scheme
    2048,                                               // TPMI_RSA_KEY_BITS    keyBits
    0                                                   // UINT32               exponent
};

static TPM2B_PUBLIC* GetEkTemplate ()
{
    static TPM2B_PUBLIC EkTemplate = { 0,   // size will be computed during marshaling
    {
        TPM_ALG_RSA,                        // TPMI_ALG_PUBLIC      type
        TPM_ALG_SHA256,                     // TPMI_ALG_HASH        nameAlg
        { 0 },                              // TPMA_OBJECT  objectAttributes (set below)
        { .t = {.size = 32,
                .buffer = { 0x83, 0x71, 0x97, 0x67, 0x44, 0x84, 0xb3, 0xf8,
                            0x1a, 0x90, 0xcc, 0x8d, 0x46, 0xa5, 0xd7, 0x24,
                            0xfd, 0x52, 0xd7, 0x6e, 0x06, 0x52, 0x0b, 0x64,
                            0xf2, 0xa1, 0xda, 0x1b, 0x33, 0x14, 0x69, 0xaa }}
        },                                  // TPM2B_DIGEST         authPolicy
        { .rsaDetail = {{0}, {0}, 0, 0} },  // TPMU_PUBLIC_PARMS    parameters (set below)
        { .sym.b = {0} }                    // TPMU_PUBLIC_ID       unique
    } };
    EkTemplate.publicArea.objectAttributes = ToTpmaObject(
        Restricted | Decrypt | FixedTPM | FixedParent | AdminWithPolicy | SensitiveDataOrigin);
    EkTemplate.publicArea.parameters.rsaDetail = RsaStorageParams;
    return &EkTemplate;
}

static TPM2B_PUBLIC* GetSrkTemplate()
{
    static TPM2B_PUBLIC SrkTemplate = { 0,  // size will be computed during marshaling
    {
        TPM_ALG_RSA,                        // TPMI_ALG_PUBLIC      type
        TPM_ALG_SHA256,                     // TPMI_ALG_HASH        nameAlg
        { 0 },                              // TPMA_OBJECT  objectAttributes (set below)
        { .t = {0, {0}} },                  // TPM2B_DIGEST         authPolicy
        { .rsaDetail = {{0}, {0}, 0, 0} },  // TPMU_PUBLIC_PARMS    parameters (set below)
        { .sym.b = {0} }                    // TPMU_PUBLIC_ID       unique
    } };
    SrkTemplate.publicArea.objectAttributes = ToTpmaObject(
        Restricted | Decrypt | FixedTPM | FixedParent | NoDA | UserWithAuth | SensitiveDataOrigin);
    SrkTemplate.publicArea.parameters.rsaDetail = RsaStorageParams;
    return &SrkTemplate;
}

#define DPS_UNMARSHAL(Type, pValue) \
{                                                                       \
    TPM_RC rc = Type##_Unmarshal(pValue, &curr_pos, (INT32*)&act_size);         \
    if (rc != TPM_RC_SUCCESS)                                           \
    {                                                                   \
        LOG_ERROR(#Type"_Unmarshal() for " #pValue " failed");           \
    }                                                                   \
}

#define DPS_UNMARSHAL_FLAGGED(Type, pValue) \
{                                                                       \
    TPM_RC rc = Type##_Unmarshal(pValue, &curr_pos, (INT32*)&act_size, TRUE);   \
    if (rc != TPM_RC_SUCCESS)                                           \
    {                                                                   \
        LOG_ERROR(#Type"_Unmarshal() for " #pValue " failed");           \
    }                                                                   \
}

#define DPS_UNMARSHAL_ARRAY(dstPtr, arrSize) \
    DPS_UNMARSHAL(UINT32, &(arrSize));                                          \
    printf("act_size %d < actSize %d\r\n", act_size, arrSize);   \
    if (act_size < arrSize)                                                     \
    {                                                                           \
        LOG_ERROR("Unmarshaling " #dstPtr " failed: Need %d bytes, while only %d left", arrSize, act_size);  \
        result = __FAILURE__;       \
    }                                                                           \
    else                            \
    {                                   \
        dstPtr = curr_pos - sizeof(UINT16);                                         \
        *(UINT16*)dstPtr = (UINT16)arrSize;                                         \
        curr_pos += arrSize;                         \
    }

static int create_tpm_session
(
    HSM_CLIENT_INFO* sec_info,
    TSS_SESSION* tpm_session
)
{
    int result;
    TPMA_SESSION sess_attrib = { .continueSession = 1 };
    if (TSS_StartAuthSession(&sec_info->tpm_device, TPM_SE_POLICY, TPM_ALG_SHA256, sess_attrib, tpm_session) != TPM_RC_SUCCESS)
    {
        LOG_ERROR("Failure: Starting EK policy session");
        result = __FAILURE__;
    }
    else if (TSS_PolicySecret(&sec_info->tpm_device, &NullPwSession, TPM_RH_ENDORSEMENT, tpm_session, NULL, 0) != TPM_RC_SUCCESS)
    {
        LOG_ERROR("Failure: PolicySecret() for EK");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }
    return result;
}

static int insert_key_in_tpm
(
    HSM_CLIENT_INFO* sec_info,
    const unsigned char* key,
    size_t key_len
)
{
    int result;
    TSS_SESSION ek_sess;
    memset(&ek_sess, 0, sizeof(TSS_SESSION));
    if (create_tpm_session(sec_info, &ek_sess) != 0)
    {
        LOG_ERROR("Failure: Starting EK policy session");
        result = __FAILURE__;
    }
    else
    {
        TPMT_SYM_DEF_OBJECT Aes128SymDef = { TPM_ALG_AES, {128}, {TPM_ALG_CFB} };
        TPM2B_ID_OBJECT enc_key_blob;
        TPM2B_ENCRYPTED_SECRET tpm_enc_secret;
        TPM2B_PRIVATE id_key_dup_blob;
        TPM2B_ENCRYPTED_SECRET encrypt_wrap_key;
        TPM2B_PUBLIC id_key_Public;
        UINT16 enc_data_size = 0;
        TPM2B_DIGEST inner_wrap_key = { .t = {0, {0}} };
        TPM2B_PRIVATE id_key_priv;
        TPM_HANDLE load_id_key = TPM_ALG_NULL;

        uint8_t* curr_pos = (uint8_t*)key;
        uint32_t act_size = (int32_t)key_len;
        memset(&id_key_Public, 0, sizeof(TPM2B_PUBLIC));
        id_key_Public.size = 0;
        id_key_Public.publicArea.type = TPM_ALG_NULL;
        DPS_UNMARSHAL(TPM2B_ID_OBJECT, &enc_key_blob);
        DPS_UNMARSHAL(TPM2B_ENCRYPTED_SECRET, &tpm_enc_secret);
        DPS_UNMARSHAL(TPM2B_PRIVATE, &id_key_dup_blob);
        DPS_UNMARSHAL(TPM2B_ENCRYPTED_SECRET, &encrypt_wrap_key);
        DPS_UNMARSHAL_FLAGGED(TPM2B_PUBLIC, &id_key_Public);

        // The given TPM may support larger TPM2B_MAX_BUFFER than this API headers define.
        // So instead of unmarshaling data in a standalone data structure just reuse the
        // original activation buffer (after updating byte order of the UINT16 counter)
        DPS_UNMARSHAL(UINT16, &enc_data_size);

        if (TPM2_ActivateCredential(&sec_info->tpm_device, &NullPwSession, &ek_sess, TPM_20_SRK_HANDLE, TPM_20_EK_HANDLE,
            &enc_key_blob, &tpm_enc_secret, &inner_wrap_key) != TPM_RC_SUCCESS)
        {
            LOG_ERROR("Failure: TPM2_ActivateCredential");
            result = __FAILURE__;
        }
        else if (TPM2_Import(&sec_info->tpm_device, &NullPwSession, TPM_20_SRK_HANDLE, (TPM2B_DATA*)&inner_wrap_key, &id_key_Public, &id_key_dup_blob, &encrypt_wrap_key, &Aes128SymDef, &id_key_priv) != TPM_RC_SUCCESS)
        {
            LOG_ERROR("Failure: importing dps Id key");
            result = __FAILURE__;
        }
        else
        {
            TPM2B_SENSITIVE_CREATE sen_create = { 0 };
            TPM2B_PUBLIC sym_pub;
            TPM2B_PRIVATE sym_priv;

            static TPM2B_PUBLIC symTemplate = { 0,   // size will be computed during marshaling
            {
                TPM_ALG_SYMCIPHER,              // TPMI_ALG_PUBLIC      type
                TPM_ALG_SHA256,                 // TPMI_ALG_HASH        nameAlg
                { 0 },                          // TPMA_OBJECT  objectAttributes (set below)
                { .t = {0, {0}} },              // TPM2B_DIGEST         authPolicy
                { .symDetail.sym = {0} },       // TPMU_PUBLIC_PARMS    parameters (set below)
                { .sym.b = {0} }                // TPMU_PUBLIC_ID       unique
            } };
            symTemplate.publicArea.objectAttributes = ToTpmaObject(Decrypt | FixedTPM | FixedParent | UserWithAuth);
            symTemplate.publicArea.parameters.symDetail.sym.algorithm = TPM_ALG_AES;
            symTemplate.publicArea.parameters.symDetail.sym.keyBits.sym = inner_wrap_key.t.size * 8;
            symTemplate.publicArea.parameters.symDetail.sym.mode.sym = TPM_ALG_CFB;

            memcpy(sen_create.sensitive.data.t.buffer, inner_wrap_key.t.buffer, inner_wrap_key.t.size);
            sen_create.sensitive.data.t.size = inner_wrap_key.t.size;

            memset(&sym_pub, 0, sizeof(TPM2B_PUBLIC));
            memset(&sym_priv, 0, sizeof(TPM2B_PRIVATE));
            if (TSS_Create(&sec_info->tpm_device, &NullPwSession, TPM_20_SRK_HANDLE, &sen_create, &symTemplate, &sym_priv, &sym_pub) != TPM_RC_SUCCESS)
            {
                LOG_ERROR("Failed to inject symmetric key data");
                result = __FAILURE__;
            }
            else if (TPM2_Load(&sec_info->tpm_device, &NullPwSession, TPM_20_SRK_HANDLE, &id_key_priv, &id_key_Public, &load_id_key, NULL) != TPM_RC_SUCCESS)
            {
                LOG_ERROR("Failed Load Id key.");
                result = __FAILURE__;
            }
            else
            {
                // Remove old Id key
                (void)TPM2_EvictControl(&sec_info->tpm_device, &NullPwSession, TPM_RH_OWNER, DPS_ID_KEY_HANDLE, DPS_ID_KEY_HANDLE);

                if (TPM2_EvictControl(&sec_info->tpm_device, &NullPwSession, TPM_RH_OWNER, load_id_key, DPS_ID_KEY_HANDLE) != TPM_RC_SUCCESS)
                {
                    LOG_ERROR("Failed Load Id key.");
                    result = __FAILURE__;
                }
                else if (TPM2_FlushContext(&sec_info->tpm_device, load_id_key) != TPM_RC_SUCCESS)
                {
                    LOG_ERROR("Failed Load Id key.");
                    result = __FAILURE__;
                }
                else
                {
                    result = 0;
                }
            }
        }
    }
    return result;
}

static int initialize_tpm_device(HSM_CLIENT_INFO* tpm_info)
{
    int result;
    if (TSS_CreatePwAuthSession(&NullAuth, &NullPwSession) != TPM_RC_SUCCESS)
    {
        LOG_ERROR("Failure calling TSS_CreatePwAuthSession");
        result = __FAILURE__;
    }
    else if (Initialize_TPM_Codec(&tpm_info->tpm_device) != TPM_RC_SUCCESS)
    {
        LOG_ERROR("Failure initializeing TPM Codec");
        result = __FAILURE__;
    }
    else if ((TSS_CreatePersistentKey(&tpm_info->tpm_device, TPM_20_EK_HANDLE, &NullPwSession, TPM_RH_ENDORSEMENT, GetEkTemplate(), &tpm_info->ek_pub) ) == 0)
    {
        LOG_ERROR("Failure calling creating persistent key for Endorsement key");
        result = __FAILURE__;
    }
    else if (TSS_CreatePersistentKey(&tpm_info->tpm_device, TPM_20_SRK_HANDLE, &NullPwSession, TPM_RH_OWNER, GetSrkTemplate(), &tpm_info->srk_pub) == 0)
    {
        LOG_ERROR("Failure calling creating persistent key for Storage Root key");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }
    return result;
}

static HSM_CLIENT_HANDLE hsm_client_tpm_create()
{
    HSM_CLIENT_INFO* result;
    result = malloc(sizeof(HSM_CLIENT_INFO) );
    if (result == NULL)
    {
        LOG_ERROR("Failure: malloc HSM_CLIENT_INFO.");
    }
    else
    {
        memset(result, 0, sizeof(HSM_CLIENT_INFO));
        if (initialize_tpm_device(result) != 0)
        {
            LOG_ERROR("Failure initializing tpm device.");
            free(result);
            result = NULL;
        }
    }
    return (HSM_CLIENT_HANDLE)result;
}

static void hsm_client_tpm_destroy(HSM_CLIENT_HANDLE handle)
{
    if (handle != NULL)
    {
        HSM_CLIENT_INFO* hsm_client_info = (HSM_CLIENT_INFO*)handle;

        Deinit_TPM_Codec(&hsm_client_info->tpm_device);
        free(hsm_client_info);
    }
}

static int hsm_client_tpm_activate_identity_key
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* key,
    size_t key_len
)
{
    int result;
    if (handle == NULL || key == NULL || key_len == 0)
    {
        LOG_ERROR("Invalid argument specified handle: %p, key: %p, key_len: %zu", handle, key, key_len);
        result = __FAILURE__;
    }
    else
    {
        if (insert_key_in_tpm((HSM_CLIENT_INFO*)handle, key, key_len))
        {
            LOG_ERROR("Failure inserting key into tpm");
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }
    return result;
}

static int hsm_client_tpm_get_endorsement_key
(
    HSM_CLIENT_HANDLE handle,
    unsigned char** key,
    size_t* key_len
)
{
    int result;
    if (handle == NULL || key == NULL || key_len == NULL)
    {
        LOG_ERROR("Invalid handle value specified: handle: %p, result: %p, result_len: %p", handle, key, key_len);
        result = __FAILURE__;
    }
    else
    {
        HSM_CLIENT_INFO* hsm_client_info = (HSM_CLIENT_INFO*)handle;
        if (hsm_client_info->ek_pub.publicArea.unique.rsa.t.size == 0)
        {
            LOG_ERROR("Endorsement key is invalid");
            result = __FAILURE__;
        }
        else
        {
            unsigned char data_bytes[TPM_DATA_LENGTH];
            unsigned char* data_pos = data_bytes;
            uint32_t data_length = TPM2B_PUBLIC_Marshal(&hsm_client_info->ek_pub, &data_pos, NULL);
            if (data_length > TPM_DATA_LENGTH)
            {
                LOG_ERROR("EK data length larger than allocated buffer %zu", (size_t)data_length);
                result = __FAILURE__;
            }
            else if ((*key = (unsigned char*)malloc(data_length)) == NULL)
            {
                LOG_ERROR("Failure creating buffer handle");
                result = __FAILURE__;
            }
            else
            {
                memcpy(*key, data_bytes, data_length);
                *key_len = (size_t)data_length;
                result = 0;
            }
        }
    }
    return result;
}

static int hsm_client_tpm_get_storage_key
(
    HSM_CLIENT_HANDLE handle,
    unsigned char** key,
    size_t* key_len
)
{
    int result;
    if (handle == NULL || key == NULL || key_len == NULL)
    {
        LOG_ERROR("Invalid handle value specified: handle: %p, result: %p, result_len: %p", handle, key, key_len);
        result = __FAILURE__;
    }
    else
    {
        HSM_CLIENT_INFO* hsm_client_info = (HSM_CLIENT_INFO*)handle;
        if (hsm_client_info->srk_pub.publicArea.unique.rsa.t.size == 0)
        {
            LOG_ERROR("storage root key is invalid");
            result = __FAILURE__;
        }
        else
        {
            unsigned char data_bytes[TPM_DATA_LENGTH];
            unsigned char* data_pos = data_bytes;
            uint32_t data_length = TPM2B_PUBLIC_Marshal(&hsm_client_info->srk_pub, &data_pos, NULL);

            if (data_length > TPM_DATA_LENGTH)
            {
                LOG_ERROR("SRK data length larger than allocated buffer %zu", (size_t)data_length);
                result = __FAILURE__;
            }
            else if ((*key = (unsigned char*)malloc(data_length)) == NULL)
            {
                LOG_ERROR("Failure creating buffer handle");
                result = __FAILURE__;
            }
            else
            {
                memcpy(*key, data_bytes, data_length);
                *key_len = (size_t)data_length;
                result = 0;
            }
        }
    }
    return result;
}

static int hsm_client_tpm_sign_data
(
    HSM_CLIENT_HANDLE handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    int result;

    if (handle == NULL || data_to_be_signed == NULL || data_to_be_signed_size == 0 ||
                    digest == NULL || digest_size == NULL)
    {
        LOG_ERROR("Invalid handle value specified handle: %p, data: %p, data_size: %zu, digest: %p, digest_size: %p",
            handle, data_to_be_signed, data_to_be_signed_size, digest, digest_size);
        result = __FAILURE__;
    }
    else
    {
        BYTE data_signature[TPM_DATA_LENGTH];
        BYTE* data_copy = (unsigned char*)data_to_be_signed;
        HSM_CLIENT_INFO* hsm_client_info = (HSM_CLIENT_INFO*)handle;

        uint32_t sign_len = SignData(&hsm_client_info->tpm_device,
                        &NullPwSession, data_copy, (UINT32)data_to_be_signed_size,
                        data_signature, sizeof(data_signature) );
        if (sign_len == 0)
        {
            LOG_ERROR("Failure signing data from hash");
            result = __FAILURE__;
        }
        else
        {
            if ((*digest = (unsigned char*)malloc(sign_len)) == NULL)
            {
                LOG_ERROR("Failure creating buffer handle");
                result = __FAILURE__;
            }
            else
            {
                memcpy(*digest, data_signature, sign_len);
                *digest_size = (size_t)sign_len;
                result = 0;
            }
        }
    }
    return result;
}

static int hsm_client_tpm_derive_and_sign_with_identity
(
   HSM_CLIENT_HANDLE handle,
   const unsigned char* data_to_be_signed,
   size_t data_to_be_signed_size,
   const unsigned char* identity,
   size_t identity_size,
   unsigned char** digest,
   size_t* digest_size
)
{
    int result =0;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid NULL Handle");
        result = __FAILURE__;
    }
    else if (data_to_be_signed == NULL)
    {
        LOG_ERROR("data to be signed is null");
        result = __FAILURE__;
    }
    else if (data_to_be_signed_size == 0)
    {
        LOG_ERROR("no data to be signed");
        result = __FAILURE__;
    }
    else if (identity == NULL)
    {
        LOG_ERROR("identity is NULL");
        result = __FAILURE__;
    }
    else if (identity_size == 0)
    {
        LOG_ERROR("identity is empty");
        result = __FAILURE__;
    }
    else if (digest==NULL)
    {
        LOG_ERROR("digest is NULL");
        result = __FAILURE__;
    }
    else if (digest_size == NULL)
    {
        LOG_ERROR("digest_size is NULL");
        result = __FAILURE__;
    }
    else
    {
        *digest = NULL;
        *digest_size = 0;

        BYTE data_signature[TPM_DATA_LENGTH];
        BYTE* data_copy = (unsigned char*)identity;
        HSM_CLIENT_INFO* hsm_client_info = (HSM_CLIENT_INFO*)handle;

        uint32_t sign_len = SignData(&hsm_client_info->tpm_device,
                        &NullPwSession, data_copy, (UINT32)identity_size,
                        data_signature, sizeof(data_signature) );
        if (sign_len == 0)
        {
            LOG_ERROR("Failure signing derived key from hash");
            result = __FAILURE__;
        }
        else
        {
            // data_signature has the module key
            // - use software signing so we don't displace the key in TPM0
            if( perform_sign_with_key(data_signature, sign_len,
                                    data_to_be_signed, data_to_be_signed_size,
                                    digest, digest_size) != 0)
            {
                LOG_ERROR("Failure signing data from derived key hash");
                result = __FAILURE__;
            }
            else
            {
                result =0;
            }

            memset(data_signature, 0, TPM_DATA_LENGTH);
        }
    }
    return result;
}

static void hsm_client_tpm_free_buffer(void* buffer)
{
    if (buffer != NULL)
    {
        free(buffer);
    }
}

int hsm_client_tpm_device_init(void)
{
    log_init(LVL_INFO);

    return 0;
}

void hsm_client_tpm_device_deinit(void)
{
}

static const HSM_CLIENT_TPM_INTERFACE tpm_interface =
{
    hsm_client_tpm_create,
    hsm_client_tpm_destroy,
    hsm_client_tpm_activate_identity_key,
    hsm_client_tpm_get_endorsement_key,
    hsm_client_tpm_get_storage_key,
    hsm_client_tpm_sign_data,
    hsm_client_tpm_derive_and_sign_with_identity,
    hsm_client_tpm_free_buffer
};

const HSM_CLIENT_TPM_INTERFACE* hsm_client_tpm_device_interface(void)
{
    return &tpm_interface;
}

