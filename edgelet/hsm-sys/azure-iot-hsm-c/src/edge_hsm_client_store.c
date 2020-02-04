#include <limits.h>
#include <stdlib.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/azure_base64.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/singlylinkedlist.h"
#include "azure_c_shared_utility/sha.h"

#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_constants.h"
#include "hsm_err.h"
#include "hsm_key.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//##############################################################################
// Data types
//##############################################################################
#define OWNER_CA_PATHLEN  3
#define DEVICE_CA_PATHLEN (OWNER_CA_PATHLEN - 1)

#define LOAD_SUCCESS 0
#define LOAD_ERR_NOT_FOUND 1
#define LOAD_ERR_VERIFICATION_FAILED 2
#define LOAD_ERR_FAILED 3

// local normalized file storage defines
#define NUM_NORMALIZED_ALIAS_CHARS  32

struct STORE_ENTRY_KEY_TAG
{
    STRING_HANDLE id;
    BUFFER_HANDLE key;
};
typedef struct STORE_ENTRY_KEY_TAG STORE_ENTRY_KEY;

struct STORE_ENTRY_PKI_CERT_TAG
{
    STRING_HANDLE id;
    STRING_HANDLE issuer_id;
    STRING_HANDLE cert_file;
    STRING_HANDLE private_key_file;
};
typedef struct STORE_ENTRY_PKI_CERT_TAG STORE_ENTRY_PKI_CERT;

struct STORE_ENTRY_PKI_TRUSTED_CERT_TAG
{
    STRING_HANDLE id;
    STRING_HANDLE cert_file;
};
typedef struct STORE_ENTRY_PKI_TRUSTED_CERT_TAG STORE_ENTRY_PKI_TRUSTED_CERT;

struct CRYPTO_STORE_ENTRY_TAG
{
    SINGLYLINKEDLIST_HANDLE sas_keys;
    SINGLYLINKEDLIST_HANDLE sym_enc_keys;
    SINGLYLINKEDLIST_HANDLE pki_certs;
    SINGLYLINKEDLIST_HANDLE pki_trusted_certs;
};
typedef struct CRYPTO_STORE_ENTRY_TAG CRYPTO_STORE_ENTRY;

struct CRYPTO_STORE_TAG
{
    STRING_HANDLE id;
    CRYPTO_STORE_ENTRY* store_entry;
    int ref_count;
};
typedef struct CRYPTO_STORE_TAG CRYPTO_STORE;

typedef enum HSM_STATE_TAG_T
{
    HSM_STATE_UNPROVISIONED = 0,
    HSM_STATE_PROVISIONED,
    HSM_STATE_PROVISIONING_ERROR
} HSM_STATE_T;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    static const char *SLASH = "\\";
#else
    static const char *SLASH = "/";
#endif

static const char *CERTS_DIR        = "certs";
static const char *CERT_KEYS_DIR    = "cert_keys";
static const char *ENC_KEYS_DIR     = "enc_keys";
static const char *CERT_FILE_EXT    = ".cert.pem";
static const char *PK_FILE_EXT      = ".key.pem";
static const char *ENC_KEY_FILE_EXT = ".enc.key";

static HSM_STATE_T g_hsm_state = HSM_STATE_UNPROVISIONED;

static CRYPTO_STORE* g_crypto_store = NULL;
static int g_store_ref_count = 0;

//##############################################################################
// Forward declarations
//##############################################################################
static int edge_hsm_client_store_create_pki_cert_internal
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle,
    int ca_path_len
);

static int edge_hsm_client_store_insert_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias,
    const char *issuer_alias,
    const char *cert_file_path,
    const char *key_file_path
);

static int edge_hsm_client_store_insert_pki_trusted_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* alias,
    const char* cert_file_name
);

static int verify_certificate_helper
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias,
    const char *issuer_alias,
    const char *cert_file_path,
    const char *key_file_path,
    bool *verification_status
);

static const char* get_base_dir(void);
//##############################################################################
// STORE_ENTRY_KEY helpers
//##############################################################################
static bool find_key_cb(LIST_ITEM_HANDLE list_item, const void *match_context)
{
    bool result;

    STORE_ENTRY_KEY *key = (STORE_ENTRY_KEY*)singlylinkedlist_item_get_value(list_item);
    if (strcmp(STRING_c_str(key->id), (const char*)match_context) == 0)
    {
        result = true;
    }
    else
    {
        result = false;
    }

    return result;
}


static STORE_ENTRY_KEY* get_key(const CRYPTO_STORE *store, HSM_KEY_T key_type, const char *key_name)
{
    STORE_ENTRY_KEY *result = NULL;
    SINGLYLINKEDLIST_HANDLE key_list = (key_type == HSM_KEY_SAS) ? store->store_entry->sas_keys :
                                                                   store->store_entry->sym_enc_keys;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(key_list, find_key_cb, key_name);
    if (list_item != NULL)
    {
        result = (STORE_ENTRY_KEY*)singlylinkedlist_item_get_value(list_item);
    }

    return result;
}

static bool key_exists(const CRYPTO_STORE *store, HSM_KEY_T key_type, const char *key_name)
{
    STORE_ENTRY_KEY *entry = get_key(store, key_type, key_name);
    return (entry != NULL) ? true : false;
}

static STORE_ENTRY_KEY* create_key_entry
(
    const char *key_name,
    const unsigned char* key,
    size_t key_size
)
{
    STORE_ENTRY_KEY *result;

    if ((result = malloc(sizeof(STORE_ENTRY_KEY))) == NULL)
    {
        LOG_ERROR("Could not allocate memory to store the key %s", key_name);
    }
    else if ((result->id = STRING_construct(key_name)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for key %s", key_name);
        free(result);
        result = NULL;
    }
    else if ((result->key = BUFFER_create(key, key_size)) == NULL)
    {
        LOG_ERROR("Could not allocate buffer for key %s", key_name);
        STRING_delete(result->id);
        free(result);
        result = NULL;
    }

    return result;
}

static void destroy_key(STORE_ENTRY_KEY *key)
{
    STRING_delete(key->id);
    BUFFER_delete(key->key);
    free(key);
}

static void destroy_keys(SINGLYLINKEDLIST_HANDLE keys)
{
    LIST_ITEM_HANDLE list_item;
    while ((list_item = singlylinkedlist_get_head_item(keys)) != NULL)
    {
        STORE_ENTRY_KEY *key_entry = (STORE_ENTRY_KEY*)singlylinkedlist_item_get_value(list_item);
        destroy_key(key_entry);
        singlylinkedlist_remove(keys, list_item);
    }
}

static bool remove_key_entry_cb
(
    const void *item,
    const void *match_context,
    bool *continue_processing
)
{
    bool result;
    STORE_ENTRY_KEY* key = (STORE_ENTRY_KEY*)item;

    if (strcmp(STRING_c_str(key->id), (const char*)match_context) == 0)
    {
        destroy_key(key);
        *continue_processing = false;
        result = true;
    }
    else
    {
        *continue_processing = true;
        result = false;
    }

    return result;
}

static int put_key
(
    CRYPTO_STORE *store,
    HSM_KEY_T key_type,
    const char *key_name,
    const unsigned char* key,
    size_t key_size
)
{
    int result;
    STORE_ENTRY_KEY *key_entry;
    SINGLYLINKEDLIST_HANDLE key_list = (key_type == HSM_KEY_SAS) ? store->store_entry->sas_keys :
                                                                   store->store_entry->sym_enc_keys;
    (void)singlylinkedlist_remove_if(key_list, remove_key_entry_cb, key_name);
    if ((key_entry = create_key_entry(key_name, key, key_size)) == NULL)
    {
        LOG_ERROR("Could not allocate memory to store key %s", key_name);
        result = __FAILURE__;
    }
    else if (singlylinkedlist_add(key_list, key_entry) == NULL)
    {
        LOG_ERROR("Could not insert key in the key store");
        destroy_key(key_entry);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int remove_key
(
    CRYPTO_STORE *store,
    HSM_KEY_T key_type,
    const char *key_name
)
{
    int result;
    SINGLYLINKEDLIST_HANDLE key_list = (key_type == HSM_KEY_SAS) ? store->store_entry->sas_keys :
                                                                   store->store_entry->sym_enc_keys;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(key_list, find_key_cb, key_name);
    if (list_item == NULL)
    {
        LOG_DEBUG("Key not found %s", key_name);
        result = __FAILURE__;
    }
    else
    {
        STORE_ENTRY_KEY *key_entry = (STORE_ENTRY_KEY*)singlylinkedlist_item_get_value(list_item);
        destroy_key(key_entry);
        singlylinkedlist_remove(key_list, list_item);
        result = 0;
    }

    return result;
}

//##############################################################################
// STORE_ENTRY_PKI_CERT helpers
//##############################################################################
static bool find_pki_cert_cb(LIST_ITEM_HANDLE list_item, const void *match_context)
{
    bool result;
    STORE_ENTRY_PKI_CERT *cert = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
    if (strcmp(STRING_c_str(cert->id), (const char*)match_context) == 0)
    {
        result = true;
    }
    else
    {
        result = false;
    }

    return result;
}

static STORE_ENTRY_PKI_CERT* get_pki_cert
(
    const CRYPTO_STORE *store,
    const char *cert_alias
)
{
    STORE_ENTRY_PKI_CERT *result = NULL;
    SINGLYLINKEDLIST_HANDLE certs_list = store->store_entry->pki_certs;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(certs_list,
                                                       find_pki_cert_cb,
                                                       cert_alias);
    if (list_item != NULL)
    {
        result = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
    }
    return result;
}

static int make_new_dir_relative_to_dir(const char *relative_dir, const char *new_dir_name)
{
    int result;

    STRING_HANDLE dir_path = STRING_construct(relative_dir);
    if (dir_path == NULL)
    {
        LOG_ERROR("Could not construct handle to relative dir %s", relative_dir);
        result = __FAILURE__;
    }
    else
    {
        if ((STRING_concat(dir_path, SLASH) != 0) ||
            (STRING_concat(dir_path, new_dir_name) != 0))
        {
            LOG_ERROR("Could not construct handle to relative dir %s", relative_dir);
            result = __FAILURE__;
        }
        else if (make_dir(STRING_c_str(dir_path)) != 0)
        {
            LOG_ERROR("Could not create dir %s relative to %s", new_dir_name, relative_dir);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }

        STRING_delete(dir_path);
    }

    return result;
}

static const char* obtain_default_platform_base_dir(void)
{
    const char *result;
    static STRING_HANDLE PLATFORM_BASE_PATH = NULL;

    if (PLATFORM_BASE_PATH == NULL)
    {
        #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
            STRING_HANDLE path;
            char *env_base_path = NULL;

            if (hsm_get_env(DEFAULT_EDGE_BASE_DIR_ENV_WIN, &env_base_path) != 0)
            {
                LOG_ERROR("Error obtaining Windows env variable %s", DEFAULT_EDGE_HOME_DIR_WIN);
                result = NULL;
            }
            else if (env_base_path == NULL)
            {
                LOG_ERROR("Windows env variable %s is not set", DEFAULT_EDGE_HOME_DIR_WIN);
                result = NULL;
            }
            else if (!is_directory_valid(env_base_path))
            {
                LOG_ERROR("Dir set in environment variable %s is not valid", env_base_path);
                result = NULL;
            }
            else if ((path = STRING_construct(env_base_path)) == NULL)
            {
                LOG_ERROR("Could not create string handle for default base path");
                result = NULL;
            }
            else
            {
                if ((STRING_concat(path, SLASH) != 0) ||
                    (STRING_concat(path, DEFAULT_EDGE_HOME_DIR_WIN) != 0))
                {
                    LOG_ERROR("Could not build path to IoT Edge home dir");
                    STRING_delete(path);
                    result = NULL;
                }
                else
                {
                    result = STRING_c_str(path);
                    if (make_dir(result) != 0)
                    {
                        LOG_ERROR("Could not create home dir %s", result);
                        STRING_delete(path);
                        result = NULL;
                    }
                    else
                    {
                        PLATFORM_BASE_PATH = path;
                    }
                }
            }
            FREEIF(env_base_path);
        #else
            if (make_dir(DEFAULT_EDGE_HOME_DIR_UNIX) != 0)
            {
                LOG_ERROR("Could not create home dir %s", DEFAULT_EDGE_HOME_DIR_UNIX);
                result = NULL;
            }
            else if ((PLATFORM_BASE_PATH = STRING_construct(DEFAULT_EDGE_HOME_DIR_UNIX)) == NULL)
            {
                LOG_ERROR("Could not create string handle for default base path");
                result = NULL;
            }
            else
            {
                result = DEFAULT_EDGE_HOME_DIR_UNIX;
            }
        #endif
    }
    else
    {
        // platform base dir already initialized
        result = STRING_c_str(PLATFORM_BASE_PATH);
    }

    return result;
}

static const char* get_base_dir(void)
{
    static STRING_HANDLE base_dir_path = NULL;

    const char *result = NULL;
    if (base_dir_path == NULL)
    {
        int status = 0;
        if ((base_dir_path = STRING_new()) == NULL)
        {
            LOG_ERROR("Could not allocate memory to hold hsm base dir");
            status = __FAILURE__;
        }
        else
        {
            char* env_base_path = NULL;
            if (hsm_get_env(ENV_EDGE_HOME_DIR, &env_base_path) != 0)
            {
                LOG_ERROR("Could not lookup home dir env variable %s", ENV_EDGE_HOME_DIR);
                status = __FAILURE__;
            }
            else if ((env_base_path != NULL) && (strlen(env_base_path) != 0))
            {
                if (!is_directory_valid(env_base_path))
                {
                    LOG_ERROR("Directory path in env variable %s is invalid. Found %s",
                              ENV_EDGE_HOME_DIR, env_base_path);
                    status = __FAILURE__;
                }
                else
                {
                    status = STRING_concat(base_dir_path, env_base_path);
                }
            }
            else
            {
                const char* default_dir = obtain_default_platform_base_dir();
                if (default_dir == NULL)
                {
                    LOG_ERROR("IOTEDGED platform specific default base directory is invalid");
                    status = __FAILURE__;
                }
                else if (STRING_concat(base_dir_path, default_dir) != 0)
                {
                    LOG_ERROR("Could not construct path to HSM dir");
                    status = __FAILURE__;
                }
            }
            FREEIF(env_base_path);
            if (status == 0)
            {
                if ((STRING_concat(base_dir_path, SLASH) != 0) ||
                    (STRING_concat(base_dir_path, HSM_CRYPTO_DIR) != 0))
                {
                    LOG_ERROR("Could not construct path to HSM dir");
                    status = __FAILURE__;
                }
                else
                {
                    result = STRING_c_str(base_dir_path);
                    if (make_dir(result) != 0)
                    {
                        LOG_ERROR("Could not make HSM dir %s", result);
                        status = __FAILURE__;
                        result = NULL;
                    }
                    else
                    {
                        // make the certs and keys dirs
                        if (make_new_dir_relative_to_dir(result, CERTS_DIR) != 0)
                        {
                            LOG_ERROR("Could not make HSM certs dir under %s", result);
                            status = __FAILURE__;
                            result = NULL;
                        }
                        else if (make_new_dir_relative_to_dir(result, CERT_KEYS_DIR) != 0)
                        {
                            LOG_ERROR("Could not make HSM cert keys dir under %s", result);
                            status = __FAILURE__;
                            result = NULL;
                        }
                        else if (make_new_dir_relative_to_dir(result, ENC_KEYS_DIR) != 0)
                        {
                            LOG_ERROR("Could not make HSM encryption keys dir under %s", result);
                            status = __FAILURE__;
                            result = NULL;
                        }
                    }
                }
            }
        }
        if ((status != 0) && (base_dir_path != NULL))
        {
            STRING_delete(base_dir_path);
            base_dir_path = NULL;
        }
    }
    else
    {
        result = STRING_c_str(base_dir_path);
    }

    return result;
}

STRING_HANDLE compute_b64_sha_digest_string
(
    const unsigned char* ip_buffer,
    size_t ip_buffer_size
)
{
    STRING_HANDLE result;
    USHAContext ctx;
    unsigned char *digest = (unsigned char*)malloc(USHAMaxHashSize);

    if (digest == NULL)
    {
        LOG_ERROR("Could not allocate memory to hold SHA digest");
        result = NULL;
    }
    else if (ip_buffer_size > UINT_MAX)
    {
        LOG_ERROR("Input buffer size too large %zu", ip_buffer_size);
        result = NULL;
    }
    else
    {
        int status;

        memset(digest, 0, USHAMaxHashSize);
        status = USHAReset(&ctx, SHA256) ||
                 USHAInput(&ctx, ip_buffer, (unsigned int)ip_buffer_size) ||
                 USHAResult(&ctx, digest);
        if (status != shaSuccess)
        {
            LOG_ERROR("Computing SHA digest failed %d", status);
            result = NULL;
        }
        else
        {
            size_t digest_size = USHAHashSize(SHA256);
            if ((result = Azure_Base64_Encode_Bytes(digest, digest_size)) == NULL)
            {
                LOG_ERROR("Base 64 encode failed after SHA compute");
            }
            else
            {
                // stanford base64 URL replace plus encoding = to _
                (void)STRING_replace(result, '+', '-');
                (void)STRING_replace(result, '/', '_');
                (void)STRING_replace(result, '=', '_');
            }
        }
        free(digest);
    }

    return result;
}

static STRING_HANDLE normalize_alias_file_path(const char *alias)
{
    STRING_HANDLE result;
    STRING_HANDLE alias_sha = NULL;
    size_t alias_len = strlen(alias);

    if ((result = STRING_new()) == NULL)
    {
        LOG_ERROR("Could not allocate normalized file string handle");
    }
    else if ((alias_sha = compute_b64_sha_digest_string((unsigned char*)alias, alias_len)) == NULL)
    {
        LOG_ERROR("Could not compute SHA for normalizing %s", alias);
        STRING_delete(result);
        result = NULL;
    }
    else
    {
        size_t idx = 0, norm_alias_idx = 0;
        char norm_alias[NUM_NORMALIZED_ALIAS_CHARS + 1];

        memset(norm_alias, 0, sizeof(norm_alias));
        while ((norm_alias_idx < NUM_NORMALIZED_ALIAS_CHARS) && (idx < alias_len))
        {
            char c = alias[idx];
            if (((c >= 'A') && (c <= 'Z')) ||
                ((c >= 'a') && (c <= 'z')) ||
                ((c >= '0') && (c <= '9')) ||
                (c == '_') || (c == '-'))
            {
                norm_alias[norm_alias_idx] = c;
                norm_alias_idx++;
            }
            idx++;
        }

        if ((STRING_concat(result, norm_alias) != 0) ||
            (STRING_concat_with_STRING(result, alias_sha) != 0))
        {
            LOG_ERROR("Could not construct normalized path for %s", alias);
            STRING_delete(result);
            result = NULL;
        }
    }

    if (alias_sha != NULL)
    {
        STRING_delete(alias_sha);
    }

    return result;
}

static int build_cert_file_paths(const char *alias, STRING_HANDLE cert_file, STRING_HANDLE pk_file)
{
    int result;
    const char *base_dir_path = get_base_dir();
    STRING_HANDLE normalized_alias;

    if ((normalized_alias = normalize_alias_file_path(alias)) == NULL)
    {
        LOG_ERROR("Could not normalize path to certificate and key for %s", alias);
        result = __FAILURE__;
    }
    else
    {
        if ((STRING_concat(cert_file, base_dir_path) != 0) ||
            (STRING_concat(cert_file, SLASH)  != 0) ||
            (STRING_concat(cert_file, CERTS_DIR)  != 0) ||
            (STRING_concat(cert_file, SLASH)  != 0) ||
            (STRING_concat_with_STRING(cert_file, normalized_alias) != 0) ||
            (STRING_concat(cert_file, CERT_FILE_EXT) != 0))
        {
            LOG_ERROR("Could not construct path to certificate for %s", alias);
            result = __FAILURE__;
        }
        else if ((pk_file != NULL) &&
                 ((STRING_concat(pk_file, base_dir_path) != 0) ||
                  (STRING_concat(pk_file, SLASH)  != 0) ||
                  (STRING_concat(pk_file, CERT_KEYS_DIR)  != 0) ||
                  (STRING_concat(pk_file, SLASH)  != 0) ||
                  (STRING_concat_with_STRING(pk_file, normalized_alias) != 0) ||
                  (STRING_concat(pk_file, PK_FILE_EXT) != 0)))
        {
            LOG_ERROR("Could not construct path to private key for %s", alias);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        STRING_delete(normalized_alias);
    }

    return result;
}

static int build_enc_key_file_path(const char *key_name, STRING_HANDLE key_file)
{
    int result;
    const char *base_dir_path = get_base_dir();
    STRING_HANDLE normalized_alias;

    if ((normalized_alias = normalize_alias_file_path(key_name)) == NULL)
    {
        LOG_ERROR("Could not normalize path to encryption key for %s", key_name);
        result = __FAILURE__;
    }
    else
    {
        if ((STRING_concat(key_file, base_dir_path) != 0) ||
            (STRING_concat(key_file, SLASH)  != 0) ||
            (STRING_concat(key_file, ENC_KEYS_DIR)  != 0) ||
            (STRING_concat(key_file, SLASH)  != 0) ||
            (STRING_concat_with_STRING(key_file, normalized_alias) != 0) ||
            (STRING_concat(key_file, ENC_KEY_FILE_EXT) != 0))
        {
            LOG_ERROR("Could not construct path to save key for %s", key_name);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        STRING_delete(normalized_alias);
    }

    return result;
}

static int save_encryption_key_to_file(const char *key_name, unsigned char *key, size_t key_size)
{
    int result;
    STRING_HANDLE key_file_handle;

    if ((key_file_handle = STRING_new()) == NULL)
    {
        LOG_ERROR("Could not create string handle");
        result = __FAILURE__;
    }
    {
        const char *key_file;
        if (build_enc_key_file_path(key_name, key_file_handle) != 0)
        {
            LOG_ERROR("Could not construct path to key");
            result = __FAILURE__;
        }
        else if ((key_file = STRING_c_str(key_file_handle)) == NULL)
        {
            LOG_ERROR("Key file path NULL");
            result = __FAILURE__;
        }
        else if (write_buffer_to_file(key_file, key, key_size, true) != 0)
        {
            LOG_ERROR("Could not write key to file");
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        STRING_delete(key_file_handle);
    }

    return result;
}

static int load_encryption_key_from_file(CRYPTO_STORE* store, const char *key_name)
{
    int result;
    STRING_HANDLE key_file_handle;

    if ((key_file_handle = STRING_new()) == NULL)
    {
        LOG_ERROR("Could not create string handle");
        result = __FAILURE__;
    }
    else
    {
        const char *key_file;
        unsigned char *key = NULL;
        size_t key_size = 0;

        if (build_enc_key_file_path(key_name, key_file_handle) != 0)
        {
            LOG_ERROR("Could not construct path to key");
            result = __FAILURE__;
        }
        else if ((key_file = STRING_c_str(key_file_handle)) == NULL)
        {
            LOG_ERROR("Key file path NULL");
            result = __FAILURE__;
        }
        else if (((key = read_file_into_buffer(key_file, &key_size)) == NULL) ||
                  (key_size == 0))
        {
            LOG_INFO("Could not read encryption key from file. "
                     " No key file exists or is invalid or permission error.");
            result = __FAILURE__;
        }
        else
        {
            result = put_key(store, HSM_KEY_ENCRYPTION, key_name, key, key_size);
        }

        free(key);
        STRING_delete(key_file_handle);
    }

    return result;
}

static int delete_encryption_key_file(const char *key_name)
{
    int result;
    STRING_HANDLE key_file_handle;

    if ((key_file_handle = STRING_new()) == NULL)
    {
        LOG_ERROR("Could not create string handle");
        result = __FAILURE__;
    }
    else
    {
        const char *key_file;
        if (build_enc_key_file_path(key_name, key_file_handle) != 0)
        {
            LOG_ERROR("Could not construct path to key");
            result = __FAILURE__;
        }
        else if ((key_file = STRING_c_str(key_file_handle)) == NULL)
        {
            LOG_ERROR("Key file path NULL");
            result = __FAILURE__;
        }
        else if (is_file_valid(key_file) && (delete_file(key_file) != 0))
        {
            LOG_ERROR("Could not delete key file");
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        STRING_delete(key_file_handle);
    }

    return result;
}

static CERT_INFO_HANDLE prepare_cert_info_handle
(
    const CRYPTO_STORE *store,
    STORE_ENTRY_PKI_CERT *cert_entry
)
{
    (void)store;
    CERT_INFO_HANDLE result;
    char *cert_contents = NULL, *private_key_contents = NULL;
    size_t private_key_size = 0;
    const char *cert_file;
    const char *pk_file;

    if ((pk_file = STRING_c_str(cert_entry->private_key_file)) == NULL)
    {
        LOG_ERROR("Private key file path is NULL");
        result = NULL;
    }
    else if ((private_key_contents = read_file_into_buffer(pk_file, &private_key_size)) == NULL)
    {
        LOG_ERROR("Could not load private key into buffer %s", pk_file);
        result = NULL;
    }
    else if ((cert_file = STRING_c_str(cert_entry->cert_file)) == NULL)
    {
        LOG_ERROR("Certificate file path NULL");
        result = NULL;
    }
    else if ((cert_contents = read_file_into_cstring(cert_file, NULL)) == NULL)
    {
        LOG_ERROR("Could not read certificate into buffer %s", cert_file);
        result = NULL;
    }
    else
    {
        result = certificate_info_create(cert_contents,
                                         private_key_contents,
                                         private_key_size,
                                         (private_key_size != 0) ? PRIVATE_KEY_PAYLOAD :
                                                                   PRIVATE_KEY_UNKNOWN);
    }

    free(cert_contents);
    free(private_key_contents);

    return result;
}

static STORE_ENTRY_PKI_CERT* create_pki_cert_entry
(
    const char *alias,
    const char *issuer_alias,
    const char *certificate_file,
    const char *private_key_file
)
{
    STORE_ENTRY_PKI_CERT *result;

    if ((result = malloc(sizeof(STORE_ENTRY_PKI_CERT))) == NULL)
    {
        LOG_ERROR("Could not allocate memory to store the certificate for alias %s", alias);
    }
    else if ((result->id = STRING_construct(alias)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for alias %s", alias);
        free(result);
        result = NULL;
    }
    else if ((result->issuer_id = STRING_construct(issuer_alias)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for issuer for alias %s", alias);
        STRING_delete(result->id);
        free(result);
        result = NULL;
    }
    else if ((result->cert_file = STRING_construct(certificate_file)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for cert file for alias %s", alias);
        STRING_delete(result->issuer_id);
        STRING_delete(result->id);
        free(result);
        result = NULL;
    }
    else if ((result->private_key_file = STRING_construct(private_key_file)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for private key file for alias %s", alias);
        STRING_delete(result->cert_file);
        STRING_delete(result->issuer_id);
        STRING_delete(result->id);
        free(result);
        result = NULL;
    }

    return result;
}

static void destroy_pki_cert(STORE_ENTRY_PKI_CERT *pki_cert)
{
    STRING_delete(pki_cert->id);
    STRING_delete(pki_cert->issuer_id);
    STRING_delete(pki_cert->cert_file);
    STRING_delete(pki_cert->private_key_file);
    free(pki_cert);
}

static bool remove_cert_entry_cb
(
    const void *item,
    const void *match_context,
    bool *continue_processing
)
{
    bool result;
    STORE_ENTRY_PKI_CERT *pki_cert = (STORE_ENTRY_PKI_CERT*)item;
    if (strcmp(STRING_c_str(pki_cert->id), (const char*)match_context) == 0)
    {
        destroy_pki_cert(pki_cert);
        *continue_processing = false;
        result = true;
    }
    else
    {
        *continue_processing = true;
        result = false;
    }

    return result;
}

static int put_pki_cert
(
    CRYPTO_STORE *store,
    const char *alias,
    const char *issuer_alias,
    const char *certificate_file,
    const char *private_key_file
)
{
    int result;
    STORE_ENTRY_PKI_CERT *cert_entry;

    cert_entry = create_pki_cert_entry(alias, issuer_alias, certificate_file, private_key_file);
    if (cert_entry == NULL)
    {
        LOG_ERROR("Could not allocate memory to store certificate and or key for %s", alias);
        result = __FAILURE__;
    }
    else
    {
        SINGLYLINKEDLIST_HANDLE cert_list = store->store_entry->pki_certs;
        (void)singlylinkedlist_remove_if(cert_list, remove_cert_entry_cb, alias);
        if (singlylinkedlist_add(cert_list, cert_entry) == NULL)
        {
            LOG_ERROR("Could not insert cert and key in the store");
            destroy_pki_cert(cert_entry);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }
    return result;
}

static int remove_pki_cert(CRYPTO_STORE *store, const char *alias)
{
    int result;
    SINGLYLINKEDLIST_HANDLE certs_list = store->store_entry->pki_certs;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(certs_list, find_pki_cert_cb, alias);
    if (list_item == NULL)
    {
        LOG_DEBUG("Certificate not found %s", alias);
        result = __FAILURE__;
    }
    else
    {
        STORE_ENTRY_PKI_CERT *pki_cert;
        pki_cert = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
        destroy_pki_cert(pki_cert);
        singlylinkedlist_remove(certs_list, list_item);
        result = 0;
    }

    return result;
}

static void destroy_pki_certs(SINGLYLINKEDLIST_HANDLE certs)
{
    LIST_ITEM_HANDLE list_item;
    while ((list_item = singlylinkedlist_get_head_item(certs)) != NULL)
    {
        STORE_ENTRY_PKI_CERT *pki_cert;
        pki_cert = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
        destroy_pki_cert(pki_cert);
        singlylinkedlist_remove(certs, list_item);
    }
}

//##############################################################################
// STORE_ENTRY_PKI_TRUSTED_CERT helpers
//##############################################################################

static bool find_pki_trusted_cert_cb(LIST_ITEM_HANDLE list_item, const void *match_context)
{
    bool result;
    STORE_ENTRY_PKI_CERT *cert = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
    if (strcmp(STRING_c_str(cert->id), (const char*)match_context) == 0)
    {
        result = true;
    }
    else
    {
        result = false;
    }

    return result;
}

static STORE_ENTRY_PKI_TRUSTED_CERT* create_pki_trusted_cert_entry
(
    const char *name,
    const char *certificate_file
)
{
    STORE_ENTRY_PKI_TRUSTED_CERT *result;

    if ((result = malloc(sizeof(STORE_ENTRY_PKI_TRUSTED_CERT))) == NULL)
    {
        LOG_ERROR("Could not allocate memory to store the certificate for %s", name);
    }
    else if ((result->id = STRING_construct(name)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for %s", name);
        free(result);
        result = NULL;
    }
    else if ((result->cert_file = STRING_construct(certificate_file)) == NULL)
    {
        LOG_ERROR("Could not allocate string handle for the file path for %s", name);
        STRING_delete(result->id);
        free(result);
        result = NULL;
    }

    return result;
}

static void destroy_trusted_cert(STORE_ENTRY_PKI_TRUSTED_CERT *trusted_cert)
{
    STRING_delete(trusted_cert->id);
    STRING_delete(trusted_cert->cert_file);
    free(trusted_cert);
}

static bool remove_trusted_cert_entry_cb
(
    const void* item,
    const void* match_context,
    bool* continue_processing
)
{
    bool result;
    STORE_ENTRY_PKI_TRUSTED_CERT* trusted_cert = (STORE_ENTRY_PKI_TRUSTED_CERT*)item;
    if (strcmp(STRING_c_str(trusted_cert->id), (const char*)match_context) == 0)
    {
        destroy_trusted_cert(trusted_cert);
        *continue_processing = false;
        result = true;
    }
    else
    {
        *continue_processing = true;
        result = false;
    }

    return result;
}

static CERT_INFO_HANDLE prepare_trusted_certs_info(CRYPTO_STORE *store)
{
    CERT_INFO_HANDLE result;
    LIST_ITEM_HANDLE list_item;
    SINGLYLINKEDLIST_HANDLE cert_list = store->store_entry->pki_trusted_certs;
    int list_count = 0;

    list_item = singlylinkedlist_get_head_item(cert_list);
    while (list_item != NULL)
    {
        list_count++;
        list_item = singlylinkedlist_get_next_item(list_item);
    }

    if (list_count > 0)
    {
        char **trusted_files;
        int index = 0;
        if ((trusted_files = (char **)calloc(list_count, sizeof(const char*))) == NULL)
        {
            LOG_ERROR("Could not allocate memory to store list of trusted cert files");
            result = NULL;
        }
        else
        {
            char *all_certs;
            list_item = singlylinkedlist_get_head_item(cert_list);
            while (list_item != NULL)
            {
                STORE_ENTRY_PKI_TRUSTED_CERT *trusted_cert;
                trusted_cert = (STORE_ENTRY_PKI_TRUSTED_CERT*)singlylinkedlist_item_get_value(list_item);
                trusted_files[index] = (char*)STRING_c_str(trusted_cert->cert_file);
                index++;
                list_item = singlylinkedlist_get_next_item(list_item);
            }
            if ((all_certs = concat_files_to_cstring((const char**)trusted_files, list_count)) == NULL)
            {
                LOG_ERROR("Could not concat all the trusted cert files");
                result = NULL;
            }
            else
            {
                result = certificate_info_create(all_certs, NULL, 0, PRIVATE_KEY_UNKNOWN);
                free(all_certs);
            }
            free(trusted_files);
        }
    }
    else
    {
        result = NULL;
    }

    return result;
}

static void destroy_pki_trusted_certs(SINGLYLINKEDLIST_HANDLE trusted_certs)
{
    LIST_ITEM_HANDLE list_item;
    while ((list_item = singlylinkedlist_get_head_item(trusted_certs)) != NULL)
    {
        STORE_ENTRY_PKI_TRUSTED_CERT *trusted_cert;
        trusted_cert = (STORE_ENTRY_PKI_TRUSTED_CERT*)singlylinkedlist_item_get_value(list_item);
        destroy_trusted_cert(trusted_cert);
        singlylinkedlist_remove(trusted_certs, list_item);
    }
}

static int put_pki_trusted_cert
(
    CRYPTO_STORE *store,
    const char *alias,
    const char *certificate_file
)
{
    int result;
    STORE_ENTRY_PKI_TRUSTED_CERT *trusted_cert_entry;
    SINGLYLINKEDLIST_HANDLE cert_list = store->store_entry->pki_trusted_certs;
    (void)singlylinkedlist_remove_if(cert_list, remove_trusted_cert_entry_cb, alias);
    trusted_cert_entry = create_pki_trusted_cert_entry(alias, certificate_file);
    if (trusted_cert_entry == NULL)
    {
        LOG_ERROR("Could not allocate memory to store trusted certificate for %s", alias);
        result = __FAILURE__;
    }
    else
    {
        if (singlylinkedlist_add(cert_list, trusted_cert_entry) == NULL)
        {
            LOG_ERROR("Could not insert cert and key in the store");
            destroy_trusted_cert(trusted_cert_entry);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }
    return result;
}

static int remove_pki_trusted_cert(CRYPTO_STORE *store, const char *alias)
{
    int result;
    SINGLYLINKEDLIST_HANDLE certs_list = store->store_entry->pki_trusted_certs;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(certs_list, find_pki_trusted_cert_cb, alias);
    if (list_item == NULL)
    {
        LOG_ERROR("Trusted certificate not found %s", alias);
        result = __FAILURE__;
    }
    else
    {
        STORE_ENTRY_PKI_TRUSTED_CERT *pki_cert;
        pki_cert = (STORE_ENTRY_PKI_TRUSTED_CERT*)singlylinkedlist_item_get_value(list_item);
        destroy_trusted_cert(pki_cert);
        singlylinkedlist_remove(certs_list, list_item);
        result = 0;
    }

    return result;
}

//##############################################################################
// CRYPTO_STORE helpers
//##############################################################################
static CRYPTO_STORE* create_store(const char *store_name)
{
    CRYPTO_STORE_ENTRY *store_entry;
    STRING_HANDLE store_id;
    CRYPTO_STORE *result;

    if ((result = (CRYPTO_STORE*)malloc(sizeof(CRYPTO_STORE))) == NULL)
    {
        LOG_ERROR("Could not allocate memory to create the store");
    }
    else if ((store_entry = (CRYPTO_STORE_ENTRY*)malloc(sizeof(CRYPTO_STORE_ENTRY))) == NULL)
    {
        LOG_ERROR("Could not allocate memory for store entry");
        free(result);
        result = NULL;
    }
    else if ((store_entry->sas_keys = singlylinkedlist_create()) == NULL)
    {
        LOG_ERROR("Could not allocate SAS keys list");
        free(store_entry);
        free(result);
        result = NULL;
    }
    else if ((store_entry->sym_enc_keys = singlylinkedlist_create()) == NULL)
    {
        LOG_ERROR("Could not allocate encryption keys list");
        singlylinkedlist_destroy(store_entry->sas_keys);
        free(store_entry);
        free(result);
        result = NULL;
    }
    else if ((store_entry->pki_certs = singlylinkedlist_create()) == NULL)
    {
        LOG_ERROR("Could not allocate certs list");
        singlylinkedlist_destroy(store_entry->sym_enc_keys);
        singlylinkedlist_destroy(store_entry->sas_keys);
        free(store_entry);
        free(result);
        result = NULL;
    }
    else if ((store_entry->pki_trusted_certs = singlylinkedlist_create()) == NULL)
    {
        LOG_ERROR("Could not allocate trusted certs list");
        singlylinkedlist_destroy(store_entry->pki_certs);
        singlylinkedlist_destroy(store_entry->sym_enc_keys);
        singlylinkedlist_destroy(store_entry->sas_keys);
        free(store_entry);
        free(result);
        result = NULL;
    }
    else if ((store_id = STRING_construct(store_name)) == NULL)
    {
        LOG_ERROR("Could not allocate store id");
        singlylinkedlist_destroy(store_entry->pki_trusted_certs);
        singlylinkedlist_destroy(store_entry->pki_certs);
        singlylinkedlist_destroy(store_entry->sym_enc_keys);
        singlylinkedlist_destroy(store_entry->sas_keys);
        free(store_entry);
        free(result);
        result = NULL;
    }
    else
    {
        result->ref_count = 1;
        result->store_entry = store_entry;
        result->id = store_id;
    }

    return result;
}

static void destroy_store(CRYPTO_STORE *store)
{
    STRING_delete(store->id);
    destroy_pki_trusted_certs(store->store_entry->pki_trusted_certs);
    singlylinkedlist_destroy(store->store_entry->pki_trusted_certs);
    destroy_pki_certs(store->store_entry->pki_certs);
    singlylinkedlist_destroy(store->store_entry->pki_certs);
    destroy_keys(store->store_entry->sym_enc_keys);
    singlylinkedlist_destroy(store->store_entry->sym_enc_keys);
    destroy_keys(store->store_entry->sas_keys);
    singlylinkedlist_destroy(store->store_entry->sas_keys);
    free(store->store_entry);
    free(store);
}

//##############################################################################
// HSM certificate provisioning
//##############################################################################
static CERT_PROPS_HANDLE create_ca_certificate_properties
(
    const char *common_name,
    uint64_t validity,
    const char *alias,
    const char *issuer_alias,
    CERTIFICATE_TYPE type
)
{
    CERT_PROPS_HANDLE certificate_props = cert_properties_create();

    if (certificate_props == NULL)
    {
        LOG_ERROR("Could not create certificate props for %s", alias);
    }
    else if (set_common_name(certificate_props, common_name) != 0)
    {
        LOG_ERROR("Could not set common name for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_validity_seconds(certificate_props, validity) != 0)
    {
        LOG_ERROR("Could not set validity for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_alias(certificate_props, alias) != 0)
    {
        LOG_ERROR("Could not set alias for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_issuer_alias(certificate_props, issuer_alias) != 0)
    {
        LOG_ERROR("Could not set issuer alias for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }
    else if (set_certificate_type(certificate_props, type) != 0)
    {
        LOG_ERROR("Could not set certificate type for %s", alias);
        cert_properties_destroy(certificate_props);
        certificate_props = NULL;
    }

    return certificate_props;
}

static int remove_if_cert_and_key_exist_by_alias
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias
)
{
    int result;
    STRING_HANDLE alias_cert_handle = NULL;
    STRING_HANDLE alias_pk_handle = NULL;
    CRYPTO_STORE *store = (CRYPTO_STORE*)handle;

    if (((alias_cert_handle = STRING_new()) == NULL) ||
        ((alias_pk_handle = STRING_new()) == NULL))
    {
        LOG_ERROR("Could not allocate string handles for storing certificate and key paths");
        result = __FAILURE__;
    }
    else if (build_cert_file_paths(alias, alias_cert_handle, alias_pk_handle) != 0)
    {
        LOG_ERROR("Could not create file paths to the certificate and private key for alias %s", alias);
        result = __FAILURE__;
    }
    else
    {
        const char *cert_file_path = STRING_c_str(alias_cert_handle);
        const char *key_file_path = STRING_c_str(alias_pk_handle);

        if (!is_file_valid(cert_file_path) || !is_file_valid(key_file_path))
        {
            LOG_ERROR("Certificate and key file for alias do not exist %s", alias);
            result = __FAILURE__;
        }
        else
        {
            if (delete_file(cert_file_path) != 0)
            {
                LOG_ERROR("Could not delete certificate file for alias %s", alias);
                result = __FAILURE__;
            }
            else if (delete_file(key_file_path) != 0)
            {
                LOG_ERROR("Could not delete key file for alias %s", alias);
                result = __FAILURE__;
            }
            else if (remove_pki_cert(store, alias) != 0)
            {
                LOG_DEBUG("Could not remove certificate and key from store for alias %s", alias);
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
        }
    }

    if (alias_cert_handle != NULL)
    {
        STRING_delete(alias_cert_handle);
    }
    if (alias_pk_handle != NULL)
    {
        STRING_delete(alias_pk_handle);
    }

    return result;
}

static int load_if_cert_and_key_exist_by_alias
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias,
    const char *issuer_alias
)
{
    int result;

    STRING_HANDLE alias_cert_handle = NULL;
    STRING_HANDLE alias_pk_handle = NULL;

    if (((alias_cert_handle = STRING_new()) == NULL) ||
        ((alias_pk_handle = STRING_new()) == NULL))
    {
        LOG_ERROR("Could not allocate string handles for storing certificate and key paths");
        result = LOAD_ERR_FAILED;
    }
    else if (build_cert_file_paths(alias, alias_cert_handle, alias_pk_handle) != 0)
    {
        LOG_ERROR("Could not create file paths to the certificate and private key for alias %s", alias);
        result = LOAD_ERR_FAILED;
    }
    else
    {
        const char *cert_file_path = STRING_c_str(alias_cert_handle);
        const char *key_file_path = STRING_c_str(alias_pk_handle);
        bool verify_status = false;
        if (is_file_valid(cert_file_path) && is_file_valid(key_file_path))
        {
            if (verify_certificate_helper(handle, alias, issuer_alias,
                                          cert_file_path, key_file_path,
                                          &verify_status) != 0)
            {
                LOG_ERROR("Failure when verifying certificate for alias %s", alias);
                result = LOAD_ERR_FAILED;
            }
            else if (!verify_status)
            {
                LOG_ERROR("Certificate for alias is invalid %s", alias);
                result = LOAD_ERR_VERIFICATION_FAILED;
            }
            else
            {
                if (edge_hsm_client_store_insert_pki_cert(handle,
                                                          alias,
                                                          issuer_alias,
                                                          cert_file_path,
                                                          key_file_path) != 0)
                {
                    LOG_ERROR("Could not load certificates into store for alias %s", alias);
                    result = LOAD_ERR_FAILED;
                }
                else
                {
                    LOG_DEBUG("Successfully loaded pre-existing certificates for alias %s", alias);
                    result = LOAD_SUCCESS;
                }
            }
        }
        else
        {
            result = LOAD_ERR_NOT_FOUND;
        }
    }
    if (alias_cert_handle != NULL)
    {
        STRING_delete(alias_cert_handle);
    }
    if (alias_pk_handle != NULL)
    {
        STRING_delete(alias_pk_handle);
    }

    return result;
}

static int create_owner_ca_cert(uint64_t validity)
{
    int result;
    CERT_PROPS_HANDLE ca_props;
    ca_props = create_ca_certificate_properties(OWNER_CA_COMMON_NAME,
                                                validity,
                                                OWNER_CA_ALIAS,
                                                OWNER_CA_ALIAS,
                                                CERTIFICATE_TYPE_CA);
    if (ca_props == NULL)
    {
        LOG_ERROR("Could not create certificate props for owner CA");
        result = __FAILURE__;
    }
    else
    {
        result = edge_hsm_client_store_create_pki_cert_internal(g_crypto_store, ca_props,
                                                                OWNER_CA_PATHLEN);
        cert_properties_destroy(ca_props);
    }

    return result;
}

static int create_device_ca_cert(uint64_t validity)
{
    int result;
    CERT_PROPS_HANDLE ca_props;
    ca_props = create_ca_certificate_properties(DEVICE_CA_COMMON_NAME,
                                                validity,
                                                hsm_get_device_ca_alias(),
                                                OWNER_CA_ALIAS,
                                                CERTIFICATE_TYPE_CA);
    if (ca_props == NULL)
    {
        LOG_ERROR("Could not create certificate props for device CA");
        result = __FAILURE__;
    }
    else
    {
        result = edge_hsm_client_store_create_pki_cert_internal(g_crypto_store,
                                                                ca_props,
                                                                DEVICE_CA_PATHLEN);
        cert_properties_destroy(ca_props);
    }

    return result;
}

/**
 * Generate the Owner CA and Device CA certificate in order to enable the quick start scenario.
 * Validate each certificate since it might have expired or the issuer certificate has been
 * modified.
 */
static int generate_edge_hsm_certificates_if_needed(uint64_t auto_generated_ca_lifetime)
{
    int result;

    int load_status = load_if_cert_and_key_exist_by_alias(g_crypto_store,
                                                          OWNER_CA_ALIAS,
                                                          OWNER_CA_ALIAS);

    if (load_status == LOAD_ERR_FAILED)
    {
        LOG_INFO("Could not load owner CA certificate and key");
        result = __FAILURE__;
    }
    else if ((load_status == LOAD_ERR_VERIFICATION_FAILED) ||
             (load_status == LOAD_ERR_NOT_FOUND))
    {
        LOG_INFO("Load status %d. Regenerating owner and device CA certs and keys", load_status);
        if (create_owner_ca_cert(auto_generated_ca_lifetime) != 0)
        {
            result = __FAILURE__;
        }
        else if (create_device_ca_cert(auto_generated_ca_lifetime) != 0)
        {
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }
    else
    {
        // owner ca was successfully created, now load/create the device CA cert
        load_status = load_if_cert_and_key_exist_by_alias(g_crypto_store,
                                                          hsm_get_device_ca_alias(),
                                                          OWNER_CA_ALIAS);
        if (load_status == LOAD_ERR_FAILED)
        {
            LOG_INFO("Could not load device CA certificate and key");
            result = __FAILURE__;
        }
        else if ((load_status == LOAD_ERR_VERIFICATION_FAILED) ||
                 (load_status == LOAD_ERR_NOT_FOUND))
        {
            LOG_DEBUG("Load status %d. Generating device CA cert and key", load_status);
            if (create_device_ca_cert(auto_generated_ca_lifetime) != 0)
            {
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int get_tg_env_vars(char **trusted_certs_path, char **device_ca_path, char **device_pk_path)
{
    int result;
    char *tb_path = NULL, *cert_path = NULL, *pk_path = NULL;

    if (hsm_get_env(ENV_TRUSTED_CA_CERTS_PATH, &tb_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_TRUSTED_CA_CERTS_PATH);
        result = __FAILURE__;
    }
    else if (hsm_get_env(ENV_DEVICE_CA_PATH, &cert_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_CA_PATH);
        result = __FAILURE__;
    }
    else if (hsm_get_env(ENV_DEVICE_PK_PATH, &pk_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_PK_PATH);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    if (result != 0)
    {
        FREEIF(tb_path);
        FREEIF(cert_path);
        FREEIF(pk_path);
    }

    *trusted_certs_path = tb_path;
    *device_ca_path = cert_path;
    *device_pk_path = pk_path;

    return result;
}

static int get_device_id_env_vars(char **device_id_cert_path, char **device_id_pk_path)
{
    int result;
    char *cert_path = NULL, *pk_path = NULL;

    if (hsm_get_env(ENV_DEVICE_ID_CERTIFICATE_PATH, &cert_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_ID_CERTIFICATE_PATH);
        result = __FAILURE__;
    }
    else if (hsm_get_env(ENV_DEVICE_ID_PRIVATE_KEY_PATH, &pk_path) != 0)
    {
        LOG_ERROR("Failed to read env variable %s", ENV_DEVICE_ID_PRIVATE_KEY_PATH);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    if (result != 0)
    {
        FREEIF(cert_path);
        FREEIF(pk_path);
    }

    *device_id_cert_path = cert_path;
    *device_id_pk_path = pk_path;

    return result;
}

static int hsm_provision_edge_id_certificate(void)
{
    int result;
    char *device_id_cert_path = NULL;
    char *device_id_pk_path = NULL;
    bool env_set = false;
    unsigned int mask = 0, i = 0;

    if (get_device_id_env_vars(&device_id_cert_path, &device_id_pk_path) != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        if (device_id_cert_path != NULL)
        {
            if ((strlen(device_id_cert_path) != 0) && is_file_valid(device_id_cert_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_ID_CERTIFICATE_PATH, device_id_cert_path);

            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_ID_CERTIFICATE_PATH, device_id_cert_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_ID_CERTIFICATE_PATH);
        }

        if (device_id_pk_path != NULL)
        {
            if ((strlen(device_id_pk_path) != 0) && is_file_valid(device_id_pk_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_ID_PRIVATE_KEY_PATH, device_id_pk_path);

            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_ID_PRIVATE_KEY_PATH, device_id_pk_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_ID_PRIVATE_KEY_PATH);
        }

        LOG_DEBUG("Device identity certificate setup mask 0x%02x", mask);

        if (env_set && (mask != 0x3))
        {
            LOG_ERROR("To setup the Edge device certificates, set "
                      "env variables with valid values:\n  %s\n  %s",
                      ENV_DEVICE_ID_CERTIFICATE_PATH, ENV_DEVICE_ID_PRIVATE_KEY_PATH);
            result = __FAILURE__;
        }
        // since we don't know the issuer, we treat the device certificate as the issuer
        else if (env_set && (edge_hsm_client_store_insert_pki_cert(g_crypto_store,
                                                                   EDGE_DEVICE_ALIAS,
                                                                   EDGE_DEVICE_ALIAS,
                                                                   device_id_cert_path,
                                                                   device_id_pk_path) != 0))
        {
            LOG_ERROR("Failure inserting device identity certificate and key into the HSM store");
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }
    free(device_id_cert_path);
    free(device_id_pk_path);

    return result;
}

static int hsm_provision_edge_ca_certificates(uint64_t auto_generated_ca_lifetime)
{
    int result;
    unsigned int mask = 0, i = 0;
    bool env_set = false;
    char *trusted_certs_path = NULL;
    char *device_ca_path = NULL;
    char *device_ca_pk_path = NULL;

    if (get_tg_env_vars(&trusted_certs_path, &device_ca_path, &device_ca_pk_path) != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        if (trusted_certs_path != NULL)
        {
            if ((strlen(trusted_certs_path) != 0) && is_file_valid(trusted_certs_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_TRUSTED_CA_CERTS_PATH, trusted_certs_path);
            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_TRUSTED_CA_CERTS_PATH, trusted_certs_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_TRUSTED_CA_CERTS_PATH);
        }

        if (device_ca_path != NULL)
        {
            if ((strlen(device_ca_path) != 0) && is_file_valid(device_ca_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_CA_PATH, device_ca_path);

            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_CA_PATH, device_ca_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_CA_PATH);
        }

        if (device_ca_pk_path != NULL)
        {
            if ((strlen(device_ca_pk_path) != 0) && is_file_valid(device_ca_pk_path))
            {
                mask |= 1 << i; i++;
            }
            else
            {
                LOG_ERROR("Path set in env variable %s is invalid or cannot be accessed: '%s'",
                          ENV_DEVICE_PK_PATH, device_ca_pk_path);

            }
            env_set = true;
            LOG_DEBUG("Env %s set to %s", ENV_DEVICE_PK_PATH, device_ca_pk_path);
        }
        else
        {
            LOG_DEBUG("Env %s is NULL", ENV_DEVICE_PK_PATH);
        }

        LOG_DEBUG("Transparent gateway setup mask 0x%02x", mask);

        if (env_set && (mask != 0x7))
        {
            LOG_ERROR("To operate Edge as a transparent gateway, set "
                      "env variables with valid values:\n  %s\n  %s\n  %s",
                      ENV_TRUSTED_CA_CERTS_PATH, ENV_DEVICE_CA_PATH, ENV_DEVICE_PK_PATH);
            result = __FAILURE__;
        }
        // none of the certificate files were provided so generate them if needed
        else if (!env_set && (generate_edge_hsm_certificates_if_needed(auto_generated_ca_lifetime) != 0))
        {
            LOG_ERROR("Failure generating required HSM certificates");
            result = __FAILURE__;
        }
        // since we don't know the issuer, we treat the device CA certificate as the issuer
        else if (env_set && (edge_hsm_client_store_insert_pki_cert(g_crypto_store,
                                                                   hsm_get_device_ca_alias(),
                                                                   hsm_get_device_ca_alias(),
                                                                   device_ca_path,
                                                                   device_ca_pk_path) != 0))
        {
            LOG_ERROR("Failure inserting device CA certificate and key into the HSM store");
            result = __FAILURE__;
        }
        else
        {
            const char *trusted_ca;
            // all required certificate files are available/generated now setup the trust bundle
            if (trusted_certs_path == NULL)
            {
                // certificates were generated so set the Owner CA as the trusted CA cert
                STORE_ENTRY_PKI_CERT *store_entry;
                trusted_ca = NULL;
                if ((store_entry = get_pki_cert(g_crypto_store, OWNER_CA_ALIAS)) == NULL)
                {
                    LOG_ERROR("Failure obtaining owner CA certificate entry");
                }
                else if ((trusted_ca = STRING_c_str(store_entry->cert_file)) == NULL)
                {
                    LOG_ERROR("Failure obtaining owner CA certificate path");
                }
            }
            else
            {
                trusted_ca = trusted_certs_path;
            }

            if (trusted_ca == NULL)
            {
                result = __FAILURE__;
            }
            else
            {
                result = put_pki_trusted_cert(g_crypto_store, DEFAULT_TRUSTED_CA_ALIAS, trusted_ca);
            }
        }

        free(trusted_certs_path);
        free(device_ca_path);
        free(device_ca_pk_path);
    }

    return result;
}

static int hsm_provision(uint64_t auto_generated_ca_lifetime)
{
    int result;

    if (get_base_dir() == NULL)
    {
        LOG_ERROR("HSM base directory does not exist. "
                  "Set environment variable IOTEDGE_HOMEDIR to a valid path.");
        result = __FAILURE__;
    }
    else if (hsm_provision_edge_ca_certificates(auto_generated_ca_lifetime) != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        result = hsm_provision_edge_id_certificate();
    }

    return result;
}

static int hsm_deprovision(void)
{
    return 0;
}

//##############################################################################
// Store interface implementation
//##############################################################################
static int edge_hsm_client_store_create(const char* store_name, uint64_t auto_generated_ca_lifetime)
{
    int result;

    if ((store_name == NULL) || (strlen(store_name) == 0))
    {
        result = __FAILURE__;
    }
    else if ((g_hsm_state == HSM_STATE_UNPROVISIONED) ||
             (g_hsm_state == HSM_STATE_PROVISIONING_ERROR))
    {
        g_crypto_store = create_store(store_name);
        if (g_crypto_store == NULL)
        {
            LOG_ERROR("Could not create HSM store");
            result = __FAILURE__;
        }
        else
        {
            if (hsm_provision(auto_generated_ca_lifetime) != 0)
            {
                destroy_store(g_crypto_store);
                g_crypto_store = NULL;
                g_hsm_state = HSM_STATE_PROVISIONING_ERROR;
                result = __FAILURE__;
            }
            else
            {
                g_store_ref_count = 1;
                g_hsm_state = HSM_STATE_PROVISIONED;
                result = 0;
            }
        }
    }
    else
    {
        g_store_ref_count++;
        result = 0;
    }

    return result;
}

static int edge_hsm_client_store_destroy(const char* store_name)
{
    int result;

    if ((store_name == NULL) || (strlen(store_name) == 0))
    {
        LOG_ERROR("Invald store name parameter");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        g_store_ref_count--;
        if (g_store_ref_count == 0)
        {
            result = hsm_deprovision();
            destroy_store(g_crypto_store);
            g_hsm_state = HSM_STATE_UNPROVISIONED;
            g_crypto_store = NULL;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static HSM_CLIENT_STORE_HANDLE edge_hsm_client_store_open(const char* store_name)
{
    HSM_CLIENT_STORE_HANDLE result;

    if ((store_name == NULL) || (strlen(store_name) == 0))
    {
        LOG_ERROR("Invald store name parameter");
        result = NULL;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = NULL;
    }
    else
    {
        result = (HSM_CLIENT_STORE_HANDLE)g_crypto_store;
    }

    return result;
}

static int edge_hsm_client_store_close(HSM_CLIENT_STORE_HANDLE handle)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invald store name parameter");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int edge_hsm_client_store_insert_sas_key
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* key_name,
    const unsigned char* key,
    size_t key_size
)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle parameter");
        result = __FAILURE__;
    }
    else if ((key_name == NULL) || (strlen(key_name) == 0))
    {
        LOG_ERROR("Invalid key name parameter");
        result = __FAILURE__;
    }
    else if ((key == NULL) || (key_size == 0))
    {
        LOG_ERROR("Invalid key parameters");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = put_key((CRYPTO_STORE*)handle, HSM_KEY_SAS, key_name, key, key_size);
    }

    return result;
}

static int edge_hsm_client_store_remove_key
(
    HSM_CLIENT_STORE_HANDLE handle,
    HSM_KEY_T key_type,
    const char* key_name
)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle parameter");
        result = __FAILURE__;
    }
    else if ((key_type != HSM_KEY_SAS) && (key_type != HSM_KEY_ENCRYPTION))
    {
        LOG_ERROR("Invalid key type parameter");
        result = __FAILURE__;
    }
    else if ((key_name == NULL) || (strlen(key_name) == 0))
    {
        LOG_ERROR("Invalid key name parameter");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
        if (key_type == HSM_KEY_ENCRYPTION)
        {
            if (remove_key((CRYPTO_STORE*)handle, key_type, key_name) != 0)
            {
                LOG_DEBUG("Encryption key not loaded in HSM store %s", key_name);
            }
            result = delete_encryption_key_file(key_name);
        }
        else
        {
            if (remove_key((CRYPTO_STORE*)handle, key_type, key_name) != 0)
            {
                LOG_ERROR("Key not loaded in HSM store %s", key_name);
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
        }
    }

    return result;
}

static KEY_HANDLE open_key
(
    CRYPTO_STORE *store,
    HSM_KEY_T key_type,
    const char* key_name
)
{
    KEY_HANDLE result;
    bool do_key_create = true;

    if (key_type == HSM_KEY_ENCRYPTION)
    {
        if (!key_exists(store, HSM_KEY_ENCRYPTION, key_name) &&
            (load_encryption_key_from_file(store, key_name) != 0))
        {
            LOG_ERROR("HSM store could not load encryption key %s", key_name);
            do_key_create = false;
        }
    }

    if (!do_key_create)
    {
        result = NULL;
    }
    else
    {
        STORE_ENTRY_KEY* key_entry;
        size_t buffer_size = 0;
        const unsigned char *buffer_ptr = NULL;
        if ((key_entry = get_key(store, key_type, key_name)) == NULL)
        {
            LOG_ERROR("Could not find key name %s", key_name);
            result = NULL;
        }
        else if (((buffer_ptr = BUFFER_u_char(key_entry->key)) == NULL) ||
                    (BUFFER_size(key_entry->key, &buffer_size) != 0) ||
                    (buffer_size == 0))
        {
            LOG_ERROR("Invalid key buffer for %s", key_name);
            result = NULL;
        }
        else
        {
            if (key_type == HSM_KEY_ENCRYPTION)
            {
                result = create_encryption_key(buffer_ptr, buffer_size);
            }
            else
            {
                result = create_sas_key(buffer_ptr, buffer_size);
            }
        }
    }

    return result;
}

static KEY_HANDLE open_certificate_private_key
(
    CRYPTO_STORE *store,
    const char* alias
)
{
    KEY_HANDLE result;
    STORE_ENTRY_PKI_CERT *cert_entry;
    const char *pk_file_path;

    if ((cert_entry = get_pki_cert(store, alias)) == NULL)
    {
        LOG_ERROR("Could not find certificate and key for alias %s", alias);
        result = NULL;
    }
    else if ((pk_file_path = STRING_c_str(cert_entry->private_key_file)) == NULL)
    {
        LOG_ERROR("Invalid private key file path buffer for %s", alias);
        result = NULL;
    }
    else
    {
        result = create_cert_key(pk_file_path);
    }

    return result;
}

static KEY_HANDLE edge_hsm_client_open_key
(
    HSM_CLIENT_STORE_HANDLE handle,
    HSM_KEY_T key_type,
    const char* key_name
)
{
    KEY_HANDLE result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle parameter");
        result = NULL;
    }
    else if ((key_name == NULL) || (strlen(key_name) == 0))
    {
        LOG_ERROR("Invalid key name parameter");
        result = NULL;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = NULL;
    }
    else
    {
        CRYPTO_STORE *store = (CRYPTO_STORE*)handle;
        if ((key_type == HSM_KEY_SAS) || (key_type == HSM_KEY_ENCRYPTION))
        {
            result = open_key(store, key_type, key_name);
        }
        else if (key_type == HSM_KEY_ASYMMETRIC_PRIVATE_KEY)
        {
            result = open_certificate_private_key(store, key_name);
        }
        else
        {
            LOG_ERROR("Invalid key type parameter");
            result = NULL;
        }
    }

    return result;
}

static int edge_hsm_client_close_key(HSM_CLIENT_STORE_HANDLE handle, KEY_HANDLE key_handle)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle parameter");
        result = __FAILURE__;
    }
    else if (key_handle == NULL)
    {
        LOG_ERROR("Invalid key handle parameter");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        key_destroy(key_handle);
        result = 0;
    }

    return result;
}

static CERT_INFO_HANDLE get_cert_info_by_alias(HSM_CLIENT_STORE_HANDLE handle, const char* alias)
{
    CERT_INFO_HANDLE result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = NULL;
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid alias value");
        result = NULL;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = NULL;
    }
    else
    {
        STORE_ENTRY_PKI_CERT *cert_entry;
        CRYPTO_STORE *store = (CRYPTO_STORE*)handle;
        if ((cert_entry = get_pki_cert(store, alias)) == NULL)
        {
            LOG_ERROR("Could not find certificate for %s", alias);
            result = NULL;
        }
        else
        {
            result = prepare_cert_info_handle(store, cert_entry);
        }
    }

    return result;
}

static int remove_cert_by_alias(HSM_CLIENT_STORE_HANDLE handle, const char* alias)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = __FAILURE__;
    }
    else if ((alias == NULL) || (strlen(alias) == 0))
    {
        LOG_ERROR("Invalid alias value");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = remove_if_cert_and_key_exist_by_alias((CRYPTO_STORE*)handle, alias);
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_store_get_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
)
{
    CERT_INFO_HANDLE result = get_cert_info_by_alias(handle, alias);

    if (result == NULL)
    {
        LOG_ERROR("Could not obtain certificate info handle for alias: %s", alias);
    }

    return result;
}

static int edge_hsm_client_store_remove_pki_cert(HSM_CLIENT_STORE_HANDLE handle, const char* alias)
{
    return remove_cert_by_alias(handle, alias);
}

static int verify_certificate_helper
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias,
    const char *issuer_alias,
    const char *cert_file_path,
    const char *key_file_path,
    bool *cert_verified
)
{
    int result;
    int cmp = strcmp(alias, issuer_alias);

    if (cmp == 0)
    {
        result = verify_certificate(cert_file_path, key_file_path, cert_file_path, cert_verified);
    }
    else
    {
        STRING_HANDLE issuer_cert_path_handle = NULL;
        CRYPTO_STORE *store = (CRYPTO_STORE*)handle;
        STORE_ENTRY_PKI_CERT *cert_entry;

        const char *issuer_cert_path = NULL;
        if ((cert_entry = get_pki_cert(store, issuer_alias)) != NULL)
        {
            LOG_DEBUG("Certificate already loaded in store for alias %s", issuer_alias);
            issuer_cert_path = STRING_c_str(cert_entry->cert_file);
        }
        else
        {
            if ((issuer_cert_path_handle = STRING_new()) == NULL)
            {
                LOG_ERROR("Could not construct string handle to hold the certificate");
            }
            else if (build_cert_file_paths(issuer_alias, issuer_cert_path_handle, NULL) != 0)
            {
                LOG_ERROR("Could not create file paths to issuer certificate alias %s", issuer_alias);
            }
            else
            {
                issuer_cert_path = STRING_c_str(issuer_cert_path_handle);
            }
        }

        if ((issuer_cert_path == NULL) || !is_file_valid(issuer_cert_path))
        {
            if (issuer_cert_path == NULL)
            {
                LOG_ERROR("Could not find issuer certificate file (null)");
            }
            else
            {
                LOG_ERROR("Could not find issuer certificate file %s", issuer_cert_path);
            }
            result = __FAILURE__;
        }
        else if (verify_certificate(cert_file_path, key_file_path, issuer_cert_path, cert_verified) != 0)
        {
            LOG_ERROR("Error trying to verify certificate %s for alias %s", cert_file_path, alias);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }

        if (issuer_cert_path_handle != NULL)
        {
            STRING_delete(issuer_cert_path_handle);
        }
    }

    return result;
}

static int edge_hsm_client_store_insert_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char *alias,
    const char *issuer_alias,
    const char *cert_file_path,
    const char *key_file_path
)
{
    CRYPTO_STORE *store = (CRYPTO_STORE*)handle;
    int result = put_pki_cert(store, alias, issuer_alias, cert_file_path, key_file_path);
    if (result != 0)
    {
        LOG_ERROR("Could not put PKI certificate and key into the store for %s", alias);
    }

    return result;
}

static int edge_hsm_client_store_create_pki_cert_internal
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle,
    int ca_path_len
)
{
    int result;
    const char* alias;
    const char* issuer_alias;

    if ((alias = get_alias(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Invalid certificate alias value");
        result = __FAILURE__;
    }
    else if ((issuer_alias = get_issuer_alias(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Invalid certificate alias value");
        result = __FAILURE__;
    }
    else
    {
        STRING_HANDLE alias_cert_handle = NULL;
        STRING_HANDLE alias_pk_handle = NULL;

        if (((alias_cert_handle = STRING_new()) == NULL) ||
            ((alias_pk_handle = STRING_new()) == NULL))
        {
            LOG_ERROR("Could not allocate string handles for storing certificate and key paths");
            result = __FAILURE__;
        }
        else if (build_cert_file_paths(alias, alias_cert_handle, alias_pk_handle) != 0)
        {
            LOG_ERROR("Could not create file paths to the certificate and private key for alias %s", alias);
            result = __FAILURE__;
        }
        else
        {
            CRYPTO_STORE *store = (CRYPTO_STORE*)handle;
            const char *issuer_pk_path = NULL;
            const char *issuer_cert_path = NULL;
            const char *alias_pk_path = STRING_c_str(alias_pk_handle);
            const char *alias_cert_path = STRING_c_str(alias_cert_handle);
            result = 0;
            if (strcmp(alias, issuer_alias) != 0)
            {
                // not a self signed certificate request
                STORE_ENTRY_PKI_CERT *issuer_cert_entry;
                if ((issuer_cert_entry = get_pki_cert(store, issuer_alias)) == NULL)
                {
                    LOG_ERROR("Could not get certificate entry for issuer %s", issuer_alias);
                    result = __FAILURE__;
                }
                else
                {
                    issuer_cert_path = STRING_c_str(issuer_cert_entry->cert_file);
                    issuer_pk_path = STRING_c_str(issuer_cert_entry->private_key_file);
                    if ((issuer_pk_path == NULL) || (issuer_cert_path == NULL))
                    {
                        LOG_ERROR("Unexpected NULL file paths found for issuer %s", issuer_alias);
                        result = __FAILURE__;
                    }
                }
            }
            if (result == 0)
            {
                // @note this will overwrite the older the certificate and private key
                // files for the requested alias
                result = generate_pki_cert_and_key(cert_props_handle,
                                                   rand(), // todo check if rand is okay or if we need something stronger like a SHA1
                                                   ca_path_len,
                                                   alias_pk_path,
                                                   alias_cert_path,
                                                   issuer_pk_path,
                                                   issuer_cert_path);
            }

            if (result != 0)
            {
                LOG_ERROR("Could not create PKI certificate and key for %s", alias);
            }
            else
            {
                result = put_pki_cert(store, alias, issuer_alias, alias_cert_path, alias_pk_path);
                if (result != 0)
                {
                    LOG_ERROR("Could not put PKI certificate and key into the store for %s", alias);
                }
            }
        }
        if (alias_cert_handle)
        {
            STRING_delete(alias_cert_handle);
        }
        if (alias_pk_handle)
        {
            STRING_delete(alias_pk_handle);
        }
    }
    return result;
}

static int edge_hsm_client_store_create_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    int result;
    const char* alias;
    const char* issuer_alias;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = __FAILURE__;
    }
    else if (cert_props_handle == NULL)
    {
        LOG_ERROR("Invalid certificate properties value");
        result = __FAILURE__;
    }
    else if ((alias = get_alias(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Invalid certificate alias value");
        result = __FAILURE__;
    }
    else if ((issuer_alias = get_issuer_alias(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Invalid certificate alias value");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        int load_status = load_if_cert_and_key_exist_by_alias(handle, alias, issuer_alias);
        if (load_status == LOAD_ERR_FAILED)
        {
            LOG_INFO("Could not load certificate and key for alias %s", alias);
            result = __FAILURE__;
        }
        else if (load_status == LOAD_ERR_VERIFICATION_FAILED)
        {
            LOG_ERROR("Failed certificate validation for alias %s", alias);
            result = __FAILURE__;
        }
        else if (load_status == LOAD_ERR_NOT_FOUND)
        {
            LOG_INFO("Generating certificate and key for alias %s", alias);
            if (edge_hsm_client_store_create_pki_cert_internal(handle, cert_props_handle, 0) != 0)
            {
                LOG_ERROR("Could not create certificate and key for alias %s", alias);
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int edge_hsm_client_store_insert_pki_trusted_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* alias,
    const char* cert_file_name
)
{
    int result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = __FAILURE__;
    }
    else if (alias == NULL)
    {
        LOG_ERROR("Invalid certificate alias value");
        result = __FAILURE__;
    }
    else if ((cert_file_name == NULL) || (!is_file_valid(cert_file_name)))
    {
        LOG_ERROR("Invalid certificate file name %s", cert_file_name);
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = put_pki_trusted_cert(handle, alias, cert_file_name);
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_store_get_pki_trusted_certs
(
	HSM_CLIENT_STORE_HANDLE handle
)
{
    CERT_INFO_HANDLE result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = NULL;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = NULL;
    }
    else
    {
        result = prepare_trusted_certs_info((CRYPTO_STORE*)handle);
    }
    return result;
}

static int edge_hsm_client_store_remove_pki_trusted_cert
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char *alias
)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = __FAILURE__;
    }
    else if ((alias == NULL) || (strlen(alias) == 0))
    {
        LOG_ERROR("Invalid handle alias value");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else
    {
        result = remove_pki_trusted_cert((CRYPTO_STORE*)handle, alias);
    }

    return result;
}

static int edge_hsm_client_store_insert_encryption_key
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* key_name
)
{
    int result;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = __FAILURE__;
    }
    else if ((key_name == NULL) || (strlen(key_name) == 0))
    {
        LOG_ERROR("Invalid handle alias value");
        result = __FAILURE__;
    }
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = __FAILURE__;
    }
    else if (key_exists((CRYPTO_STORE*)handle, HSM_KEY_ENCRYPTION, key_name))
    {
        LOG_DEBUG("HSM store already has encryption key set %s", key_name);
        result = 0;
    }
    else if (load_encryption_key_from_file(g_crypto_store, key_name) == 0)
    {
        LOG_DEBUG("Loaded encryption key %s from file", key_name);
        result = 0;
    }
    else
    {
        size_t key_size = 0;
        unsigned char *key = NULL;
        if (generate_encryption_key(&key, &key_size) != 0)
        {
            LOG_ERROR("Could not create encryption key for %s", key_name);
            result = __FAILURE__;
        }
        else
        {
            if (save_encryption_key_to_file(key_name, key, key_size) != 0)
            {
                LOG_ERROR("Could not persist encryption key %s to file", key_name);
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
            free(key);
        }
    }

    return result;
}

static const HSM_CLIENT_STORE_INTERFACE edge_hsm_client_store_interface =
{
    edge_hsm_client_store_create,
    edge_hsm_client_store_destroy,
    edge_hsm_client_store_open,
    edge_hsm_client_store_close,
    edge_hsm_client_open_key,
    edge_hsm_client_close_key,
    edge_hsm_client_store_remove_key,
    edge_hsm_client_store_insert_sas_key,
    edge_hsm_client_store_insert_encryption_key,
    edge_hsm_client_store_create_pki_cert,
    edge_hsm_client_store_get_pki_cert,
    edge_hsm_client_store_remove_pki_cert,
    edge_hsm_client_store_insert_pki_trusted_cert,
    edge_hsm_client_store_get_pki_trusted_certs,
    edge_hsm_client_store_remove_pki_trusted_cert
};

const HSM_CLIENT_STORE_INTERFACE* hsm_client_store_interface(void)
{
    return &edge_hsm_client_store_interface;
}
