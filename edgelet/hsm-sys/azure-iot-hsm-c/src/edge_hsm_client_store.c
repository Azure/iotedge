#include <assert.h>
#include <stdlib.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/singlylinkedlist.h"

#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_key.h"
#include "hsm_log.h"
#include "hsm_utils.h"

SINGLYLINKEDLIST_HANDLE g_key_store = NULL;
struct KEY_STORE_ENTRY_TAG
{
    STRING_HANDLE id;
    BUFFER_HANDLE key;
    STRING_HANDLE cert_buffer;
    BUFFER_HANDLE private_key_buffer;
};
typedef struct KEY_STORE_ENTRY_TAG KEY_STORE_ENTRY;

static int edge_hsm_client_store_create(const char* store_name)
{
    (void)store_name;
    if (g_key_store == NULL)
    {
        g_key_store = singlylinkedlist_create();
        assert(g_key_store != NULL);
    }
    return 0;
}

static int edge_hsm_client_store_destroy(const char* store_name)
{
    (void)store_name;
    assert(g_key_store != NULL);
    singlylinkedlist_destroy((SINGLYLINKEDLIST_HANDLE)g_key_store);
    g_key_store = NULL;
    return 0;
}

static HSM_CLIENT_STORE_HANDLE edge_hsm_client_store_open(const char* store_name)
{
    (void)store_name;
    assert(g_key_store != NULL);
    return (HSM_CLIENT_STORE_HANDLE)g_key_store;
}

static int edge_hsm_client_store_close(HSM_CLIENT_STORE_HANDLE handle)
{
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    return 0;
}

bool remove_entry_cb(const void* item, const void* match_context, bool* continue_processing)
{
    bool result;
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)item;
    if (strcmp(STRING_c_str(p_entry->id), (const char*)match_context) == 0)
    {
        STRING_delete(p_entry->id);
        BUFFER_delete(p_entry->key);
        STRING_delete(p_entry->cert_buffer);
        BUFFER_delete(p_entry->private_key_buffer);
	free(p_entry);
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

static bool find_entry_cb(LIST_ITEM_HANDLE list_item, const void* match_context)
{
    bool result;
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)singlylinkedlist_item_get_value(list_item);
    if (strcmp(STRING_c_str(p_entry->id), (const char*)match_context) == 0)
    {
        result = true;
    }
    else
    {
        result = false;
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
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    assert(key != NULL);
    assert(key_size != 0);
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)malloc(sizeof(KEY_STORE_ENTRY));
    assert(p_entry != NULL);
	p_entry->id = STRING_construct(key_name);
    assert(p_entry->id != NULL);
    p_entry->key = BUFFER_create(key, key_size);
    assert(p_entry->key != NULL);
	p_entry->cert_buffer = STRING_new();
    assert(p_entry->cert_buffer != NULL);
	p_entry->private_key_buffer = BUFFER_new();
    assert(p_entry->private_key_buffer != NULL);
    singlylinkedlist_remove_if(g_key_store, remove_entry_cb, key_name);
    LIST_ITEM_HANDLE result = singlylinkedlist_add(g_key_store, p_entry);
    assert(result != NULL);
    return 0;
}

static int edge_hsm_client_store_remove_key
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* key_name
)
{
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    int status = singlylinkedlist_remove_if(g_key_store, remove_entry_cb, key_name);
    assert(status == 0);
    return 0;
}

static KEY_HANDLE edge_hsm_client_open_key(HSM_CLIENT_STORE_HANDLE handle, const char* key_name)
{
    size_t key_size = 0;
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    LIST_ITEM_HANDLE list_entry = singlylinkedlist_find(g_key_store, find_entry_cb, key_name);
    assert(list_entry != NULL);
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)singlylinkedlist_item_get_value(list_entry);
    assert(p_entry != NULL);
    assert(p_entry->key != NULL);
    int status = BUFFER_size(p_entry->key, &key_size);
    assert(status == 0);
    return create_sas_key(BUFFER_u_char(p_entry->key), key_size);
}

static int edge_hsm_client_close_key(HSM_CLIENT_STORE_HANDLE handle, KEY_HANDLE key_handle)
{
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    destroy_sas_key(key_handle);
    return 0;
}

static int edge_hsm_client_store_insert_encryption_key(HSM_CLIENT_STORE_HANDLE handle, const char* key_name)
{
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    LOG_ERROR("API unsupported");
    return __FAILURE__;
}

static CERT_INFO_HANDLE get_cert_by_alias(HSM_CLIENT_STORE_HANDLE handle, const char* alias)
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
    else
    {
        assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
        LIST_ITEM_HANDLE list_entry = singlylinkedlist_find(g_key_store, find_entry_cb, alias);
        if (list_entry == NULL)
        {
            LOG_ERROR("Certificate for alias %s not found", alias);
            result = NULL;
        }
        else
        {
            size_t pk_len;
            KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)singlylinkedlist_item_get_value(list_entry);
            assert(p_entry != NULL);
            assert(p_entry->cert_buffer != NULL);
            assert(STRING_length(p_entry->cert_buffer) > 0);
            assert(p_entry->private_key_buffer != NULL);
            pk_len = BUFFER_length(p_entry->private_key_buffer);
            result = certificate_info_create(STRING_c_str(p_entry->cert_buffer),
                                             BUFFER_u_char(p_entry->private_key_buffer),
                                             pk_len,
                                             (pk_len != 0) ? PRIVATE_KEY_PAYLOAD : PRIVATE_KEY_UNKNOWN);
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
    else
    {
        singlylinkedlist_remove_if(g_key_store, remove_entry_cb, alias);
        result = 0;
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_store_get_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
)
{
    return get_cert_by_alias(handle, alias);
}

static int edge_hsm_client_store_remove_pki_cert(HSM_CLIENT_STORE_HANDLE handle, const char* alias)
{
    return remove_cert_by_alias(handle, alias);
}

static char* cert_file_path_helper(const char* relative_path)
{
    char *file_path;
    char* base_path = getenv("EDGEHOMEDIR");
    if (base_path == NULL)
    {
        LOG_ERROR("Environment variable EDGEHOMEDIR is not set");
        file_path = NULL;
    }
    else if (!is_directory_valid(base_path))
    {
        LOG_ERROR("Directory path in env variable EDGEHOMEDIR is invalid");
        file_path = NULL;
    }
    else
    {
        size_t file_path_size = strlen(relative_path) + strlen(base_path) + 1;
        if ((file_path = (char*)malloc(file_path_size)) == NULL)
        {
            LOG_ERROR("Failed to allocate memory to hold file path");
        }
        else
        {
            memset(file_path, 0, file_path_size);
            strcat(file_path, base_path);
            strcat(file_path, relative_path);
            if (!is_file_valid(file_path))
            {
                free(file_path);
                file_path = NULL;
            }
        }
    }
    return file_path;
}

static char* get_edge_hub_cert_path(void)
{
    static const char *EDGE_HUB_CERT_PATH = "/certs/edge-hub-server/cert/edge-hub-server.cert.pem";
    return cert_file_path_helper(EDGE_HUB_CERT_PATH);
}

static char* get_edge_hub_pk_path(void)
{
    static const char *EDGE_HUB_PK_PATH = "/certs/edge-hub-server/private/edge-hub-server.key.pem";
    return cert_file_path_helper(EDGE_HUB_PK_PATH);
}

static char* get_edge_hub_chain_path(void)
{
    static const char *EDGE_HUB_CHAIN_PATH = "/certs/edge-chain-ca/cert/edge-chain-ca.cert.pem";
    return cert_file_path_helper(EDGE_HUB_CHAIN_PATH);
}

static char* get_edge_hub_root_ca_path(void)
{
    static const char *EDGE_HUB_ROOT_CA_PATH = "/certs/edge-device-ca/cert/edge-device-ca-root.cert.pem";
    return cert_file_path_helper(EDGE_HUB_ROOT_CA_PATH);
}

static int edge_hsm_client_store_create_pki_cert
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    int result;
    const char* alias;

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
    else
    {
        // obtain the edgehub server cert, chain cert and private key for now from EDGEHOMEDIR
        char* files[3] = { get_edge_hub_cert_path(),
                           get_edge_hub_chain_path(),
                           get_edge_hub_pk_path() };
        if ((files[0] == NULL) || (files[1] == NULL) || (files[2] == NULL))
        {
            LOG_ERROR("Invalid certificate or private key paths found");
            result = __FAILURE__;
        }
        else
        {
            char* cert_contents = NULL;
            KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)malloc(sizeof(KEY_STORE_ENTRY));
            assert(p_entry != NULL);
            cert_contents = concat_files_to_cstring(files, 2);
            assert(cert_contents != NULL);
            p_entry->id = STRING_construct(alias);
            assert(p_entry->id != NULL);
            p_entry->key = BUFFER_new();
            assert(p_entry->key != NULL);
            p_entry->cert_buffer = STRING_construct(cert_contents);
            assert(p_entry->cert_buffer != NULL);
            free(cert_contents);

            size_t private_key_buffer_size = 0;
            char *private_key_buffer = read_file_into_cstring(files[2], &private_key_buffer_size);
            assert(private_key_buffer != NULL);
            p_entry->private_key_buffer = BUFFER_create((unsigned char*)private_key_buffer, private_key_buffer_size);
            assert(p_entry->private_key_buffer != NULL);
            free(private_key_buffer);
            singlylinkedlist_remove_if(g_key_store, remove_entry_cb, alias);
            LIST_ITEM_HANDLE list_item = singlylinkedlist_add(g_key_store, p_entry);
            assert(list_item != NULL);
            result = 0;
        }
        for (int index = 0; index < sizeof(files)/sizeof(files[0]); index++)
        {
            if (files[index]) free(files[index]);
        }
    }
    return 0;
}

static void insert_trusted_cert(HSM_CLIENT_STORE_HANDLE handle, const char* cert_file_name)
{
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)malloc(sizeof(KEY_STORE_ENTRY));
    assert(p_entry != NULL);
    p_entry->id = STRING_construct("trusted-ca");
    assert(p_entry->id != NULL);
    p_entry->key = BUFFER_new();
    assert(p_entry->key != NULL);
    char* cert_contents = read_file_into_cstring(cert_file_name, NULL);
    assert(cert_contents != NULL);
    p_entry->cert_buffer = STRING_construct(cert_contents);
    assert(p_entry->cert_buffer != NULL);
    free(cert_contents);
    p_entry->private_key_buffer = BUFFER_new();
    assert(p_entry->private_key_buffer != NULL);
    singlylinkedlist_remove_if(g_key_store, remove_entry_cb, "trusted-ca");
    LIST_ITEM_HANDLE list_item = singlylinkedlist_add(g_key_store, p_entry);
    assert(list_item != NULL);
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
    else if ((cert_file_name == NULL) || (is_file_valid(cert_file_name)))
    {
        LOG_ERROR("Invalid certificate file name %s", cert_file_name);
        result = __FAILURE__;
    }
    else
    {
        insert_trusted_cert(handle, cert_file_name);
        result = 0;
    }

    return result;
}

static CERT_INFO_HANDLE edge_hsm_client_store_get_pki_trusted_certs
(
	HSM_CLIENT_STORE_HANDLE handle
)
{
    static bool trusted_ca_inserted = false;
    CERT_INFO_HANDLE result;
    if (handle == NULL)
    {
        LOG_ERROR("Invalid handle value");
        result = NULL;
    }
    else
    {
        if (!trusted_ca_inserted)
        {
            char *cert_file_name = get_edge_hub_root_ca_path();
            if (cert_file_name == NULL)
            {
                LOG_ERROR("Trusted CA certificate unavailable");
                result = NULL;
            }
            else
            {
                insert_trusted_cert(handle, cert_file_name);
                free(cert_file_name);
                trusted_ca_inserted = true;
                result = get_cert_by_alias(handle, "trusted-ca");
            }
        }
        else
        {
            result = get_cert_by_alias(handle, "trusted-ca");
        }
    }
    return result;
}

static int edge_hsm_client_store_remove_pki_trusted_cert
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
)
{
    return __FAILURE__;
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
