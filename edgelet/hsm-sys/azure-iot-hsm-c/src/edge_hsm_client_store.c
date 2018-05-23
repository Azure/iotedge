#include <assert.h>
#include <stdlib.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/singlylinkedlist.h"

#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_constants.h"
#include "hsm_key.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//##############################################################################
// Data types
//##############################################################################
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

static HSM_STATE_T g_hsm_state = HSM_STATE_UNPROVISIONED;

static CRYPTO_STORE* g_crypto_store = NULL;

//##############################################################################
// Forward declarations
//##############################################################################
static int edge_hsm_client_store_create_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle
);

static int edge_hsm_client_store_insert_pki_trusted_cert
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias,
	const char* cert_file_name
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
        LOG_ERROR("Key not found %s", key_name);
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

static const char* get_base_dir(void)
{
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        const char *DEFAULT_DIR = DEFAULT_EDGE_HOME_DIR_WIN;
    #else
        const char *DEFAULT_DIR = DEFAULT_EDGE_HOME_DIR_UNIX;
    #endif
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
            char* env_base_path = getenv(ENV_EDGE_HOME_DIR);
            if ((env_base_path != NULL) && (strlen(env_base_path) != 0))
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
                if (make_dir(DEFAULT_DIR) != 0)
                {
                    LOG_ERROR("Could not make IOTEDGED default dir %s", result);
                    status = __FAILURE__;
                }
                else if (STRING_concat(base_dir_path, DEFAULT_DIR) != 0)
                {
                    LOG_ERROR("Could not construct path to HSM dir");
                    status = __FAILURE__;
                }
            }
            if (status == 0)
            {
                if (STRING_concat(base_dir_path, SLASH) ||
                    STRING_concat(base_dir_path, HSM_CRYPTO_DIR))
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

static int cert_file_path_helper(const char *alias, STRING_HANDLE cert_file, STRING_HANDLE pk_file)
{
    static const char *CERT_FILE_EXT = ".cert.pem";
    static const char *PK_FILE_EXT = ".key.pem";
    int result;
    const char *base_dir_path = get_base_dir();

    if ((STRING_concat(cert_file, base_dir_path) != 0) ||
        (STRING_concat(cert_file, SLASH)  != 0) ||
        (STRING_concat(cert_file, alias) != 0) ||
        (STRING_concat(cert_file, CERT_FILE_EXT) != 0))
    {
        LOG_ERROR("Could not construct path to certificate for %s", alias);
        result = __FAILURE__;
    }
    else if ((STRING_concat(pk_file, base_dir_path) != 0) ||
             (STRING_concat(pk_file, SLASH) != 0) ||
             (STRING_concat(pk_file, alias) != 0) ||
             (STRING_concat(pk_file, PK_FILE_EXT) != 0))
    {
        LOG_ERROR("Could not construct path to private key for %s", alias);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static CERT_INFO_HANDLE prepare_cert_info_handle
(
    const CRYPTO_STORE *store,
    STORE_ENTRY_PKI_CERT *cert_entry
)
{
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
    else if ((private_key_contents = read_file_into_cstring(pk_file, &private_key_size)) == NULL)
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
        STRING_HANDLE full_cert;

        if ((full_cert = STRING_construct(cert_contents)) == NULL)
        {
            LOG_ERROR("Could not construct string handle to hold the certificate");
            result = NULL;
        }
        else
        {
            bool is_loop_error = false;
            char *temp_cert_buffer = NULL;
            const char *temp_file_path;
            STORE_ENTRY_PKI_CERT *temp_cert_entry = cert_entry;
            while (STRING_compare(temp_cert_entry->id, temp_cert_entry->issuer_id) != 0)
            {
                const char *issuer_alias;
                if ((issuer_alias = STRING_c_str(temp_cert_entry->issuer_id)) == NULL)
                {
                    LOG_ERROR("Issuer found to be NULL");
                    is_loop_error = true;
                    break;
                }
                else if ((temp_cert_entry = get_pki_cert(store, issuer_alias)) == NULL)
                {
                    LOG_ERROR("Could not find certificate for issuer %s", issuer_alias);
                    is_loop_error = true;
                    break;
                }
                else if ((temp_file_path = STRING_c_str(temp_cert_entry->cert_file)) == NULL)
                {
                    LOG_ERROR("Certificate file path NULL");
                    is_loop_error = true;
                    break;
                }
                else if ((temp_cert_buffer = read_file_into_cstring(temp_file_path, NULL)) == NULL)
                {
                    LOG_ERROR("Could not read certificate into buffer %s", temp_file_path);
                    is_loop_error = true;
                    break;
                }
                else if (STRING_concat(full_cert, temp_cert_buffer) != 0)
                {
                    LOG_ERROR("Could not concatenate issuer certificate %s", temp_file_path);
                    free(temp_cert_buffer);
                    temp_cert_buffer = NULL;
                    is_loop_error = true;
                    break;
                }
                else
                {
                    LOG_DEBUG("Chaining issuer certificate %s", issuer_alias);
                    free(temp_cert_buffer);
                    temp_cert_buffer = NULL;
                }
            }
            if (!is_loop_error)
            {
                result = certificate_info_create(STRING_c_str(full_cert),
                                                 private_key_contents,
                                                 private_key_size,
                                                 (private_key_size != 0) ? PRIVATE_KEY_PAYLOAD :
                                                                           PRIVATE_KEY_UNKNOWN);
            }
            else
            {
                result = NULL;
            }
            STRING_delete(full_cert);
        }
    }

    if (cert_contents != NULL)
    {
        free(cert_contents);
    }
    if (private_key_contents != NULL)
    {
        free(private_key_contents);
    }

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
    SINGLYLINKEDLIST_HANDLE certss_list = store->store_entry->pki_certs;
    LIST_ITEM_HANDLE list_item = singlylinkedlist_find(certss_list, find_pki_cert_cb, alias);
    if (list_item == NULL)
    {
        LOG_ERROR("Certificate not found %s", alias);
        result = __FAILURE__;
    }
    else
    {
        STORE_ENTRY_PKI_CERT *pki_cert;
        pki_cert = (STORE_ENTRY_PKI_CERT*)singlylinkedlist_item_get_value(list_item);
        destroy_pki_cert(pki_cert);
        singlylinkedlist_remove(certss_list, list_item);
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

static int generate_edge_hsm_certificates(void)
{
    int result;
    CERT_PROPS_HANDLE ca_props;
    ca_props = create_ca_certificate_properties(OWNER_CA_COMMON_NAME,
                                                CA_VALIDITY,
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
        result = edge_hsm_client_store_create_pki_cert(g_crypto_store, ca_props);
        cert_properties_destroy(ca_props);
        ca_props = NULL;
    }

    if (result == 0)
    {
        ca_props = create_ca_certificate_properties(DEVICE_CA_COMMON_NAME,
                                                    CA_VALIDITY,
                                                    DEVICE_CA_ALIAS,
                                                    OWNER_CA_ALIAS,
                                                    CERTIFICATE_TYPE_CA);
        if (ca_props == NULL)
        {
            LOG_ERROR("Could not create certificate props for device CA");
            result = __FAILURE__;
        }
        else
        {
            result = edge_hsm_client_store_create_pki_cert(g_crypto_store, ca_props);
            cert_properties_destroy(ca_props);
            ca_props = NULL;
        }
    }

    return result;
}

static int hsm_provision_edge_certificates(void)
{
    int result = 0;
    unsigned int mask = 0, i = 0;
    bool env_set = false;
    const char *owner_ca_path = getenv(ENV_OWNER_CA_PATH);
    const char *device_ca_path = getenv(ENV_DEVICE_CA_PATH);
    const char *device_pk_path = getenv(ENV_DEVICE_PK_PATH);
    const char *device_ca_chain_path = getenv(ENV_DEVICE_CA_CHAIN_PATH);

    if (owner_ca_path != NULL)
    {
        if (is_file_valid(owner_ca_path))
        {
            mask |= 1 << i; i++;
        }
        env_set = true;
    }
    if (device_ca_path != NULL)
    {
        if (is_file_valid(device_ca_path))
        {
            mask |= 1 << i; i++;
        }
        env_set = true;
    }
    if (device_pk_path != NULL)
    {
        if (is_file_valid(device_pk_path))
        {
            mask |= 1 << i; i++;
        }
        env_set = true;
    }
    if (device_ca_chain_path != NULL)
    {
        if (is_file_valid(device_ca_chain_path))
        {
            mask |= 1 << i; i++;
        }
        env_set = true;
    }
    if (env_set)
    {
        if (mask != 0xF)
        {
            LOG_ERROR("To operate Edge as a transparent gateway, set "
                      "env variables with valid values:\n  %s\n  %s\n  %s\n  %s",
                      ENV_OWNER_CA_PATH, ENV_DEVICE_CA_PATH,
                      ENV_DEVICE_CA_CHAIN_PATH, ENV_DEVICE_PK_PATH);
            result = __FAILURE__;
        }
    }
    else
    {
        // none of the certificate files were provided so generate them
        result = generate_edge_hsm_certificates();
    }

    if (result == 0)
    {
        // all required certificate files are available now setup trust bundle
        if (owner_ca_path == NULL)
        {
            STORE_ENTRY_PKI_CERT *store_entry = get_pki_cert(g_crypto_store, OWNER_CA_ALIAS);
            if (store_entry == NULL)
            {
                result = __FAILURE__;
            }
            owner_ca_path = STRING_c_str(store_entry->cert_file);
        }
        if (result == 0)
        {
            result = edge_hsm_client_store_insert_pki_trusted_cert(g_crypto_store,
                                                                   OWNER_CA_ALIAS,
                                                                   owner_ca_path);
        }
    }

    return result;
}

static int hsm_provision(void)
{
    int result;

    if (get_base_dir() == NULL)
    {
        LOG_ERROR("HSM base directory does not exist. "
                  "Set environment variable IOTEDGE_HOMEDIR to a valid path.");
        result = __FAILURE__;
    }
    else
    {
        result = hsm_provision_edge_certificates();
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
static int edge_hsm_client_store_create(const char* store_name)
{
    int result;

    if ((store_name == NULL) || (strlen(store_name) == 0))
    {
        result = __FAILURE__;
    }
    else if (g_hsm_state == HSM_STATE_UNPROVISIONED)
    {
        g_crypto_store = create_store(store_name);
        if (g_crypto_store == NULL)
        {
            LOG_ERROR("Could not create HSM store");
            result = __FAILURE__;
        }
        else
        {
            g_hsm_state = HSM_STATE_PROVISIONED;
            if ((result = hsm_provision()) != 0)
            {
                g_hsm_state = HSM_STATE_PROVISIONING_ERROR;
            }
        }
    }
    else
    {
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
        result = hsm_deprovision();
        destroy_store(g_crypto_store);
        g_hsm_state = HSM_STATE_UNPROVISIONED;
        g_crypto_store = NULL;
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
        result = remove_key((CRYPTO_STORE*)handle, key_type, key_name);
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
    STORE_ENTRY_KEY* key_entry;
    size_t buffer_size = 0;
    const unsigned char *buffer_ptr = NULL;

    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle parameter");
        result = NULL;
    }
    else if ((key_type != HSM_KEY_SAS) && (key_type != HSM_KEY_ENCRYPTION))
    {
        LOG_ERROR("Invalid key type parameter");
        result = NULL;
    }
    else if ((key_name == NULL) || (strlen(key_name) == 0))
    {
        LOG_ERROR("Invalid key name parameter");
        result = NULL;
    }
    else if ((key_entry = get_key((CRYPTO_STORE*)handle, key_type, key_name)) == NULL)
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
    else if (g_hsm_state != HSM_STATE_PROVISIONED)
    {
        LOG_ERROR("HSM store has not been provisioned");
        result = NULL;
    }
    else
    {
        result = create_sas_key(buffer_ptr, buffer_size);
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
        destroy_sas_key(key_handle);
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
    else if (alias == NULL)
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
        result = remove_pki_cert((CRYPTO_STORE*)handle, alias);
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
        STRING_HANDLE alias_cert_handle = NULL;
        STRING_HANDLE alias_pk_handle = NULL;

        if (((alias_cert_handle = STRING_new()) == NULL) ||
            ((alias_pk_handle = STRING_new()) == NULL))
        {
            LOG_ERROR("Could not allocate file paths for storing certificate and key");
            result = __FAILURE__;
        }
        else if (cert_file_path_helper(alias, alias_cert_handle, alias_pk_handle) != 0)
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
    else
    {
        LOG_ERROR("API unsupported");
        result = __FAILURE__;
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
