#include <assert.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/strings.h"
#include "azure_c_shared_utility/singlylinkedlist.h"

#include "hsm_client_data.h"
#include "hsm_client_store.h"
#include "hsm_key.h"
#include "hsm_log.h"

SINGLYLINKEDLIST_HANDLE g_key_store = NULL;
struct KEY_STORE_ENTRY_TAG
{
    STRING_HANDLE key_name;
    BUFFER_HANDLE key;
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
    if (strcmp(STRING_c_str(p_entry->key_name), (const char*)match_context) == 0)
    {
        STRING_delete(p_entry->key_name);
        BUFFER_delete(p_entry->key);
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
    if (strcmp(STRING_c_str(p_entry->key_name), (const char*)match_context) == 0)
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
    int status;
    assert((HSM_CLIENT_STORE_HANDLE)g_key_store == handle);
    assert(key != NULL);
    assert(key_size != 0);
    KEY_STORE_ENTRY* p_entry = (KEY_STORE_ENTRY*)malloc(sizeof(KEY_STORE_ENTRY));
    assert(p_entry != NULL);
	p_entry->key_name = STRING_construct(key_name);
    assert(p_entry->key_name != NULL);
    p_entry->key = BUFFER_create(key, key_size);
    assert(p_entry->key != NULL);
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

static const HSM_CLIENT_STORE_INTERFACE edge_hsm_client_store_interface =
{
    edge_hsm_client_store_create,
    edge_hsm_client_store_destroy,
    edge_hsm_client_store_open,
    edge_hsm_client_store_close,
    edge_hsm_client_open_key,
    edge_hsm_client_close_key,
    edge_hsm_client_store_remove_key,
    edge_hsm_client_store_insert_sas_key
};

const HSM_CLIENT_STORE_INTERFACE* hsm_client_store_interface(void)
{
    return &edge_hsm_client_store_interface;
}
