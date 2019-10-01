#ifndef HSM_CLIENT_STORE_H
#define HSM_CLIENT_STORE_H

#ifdef __cplusplus
#include <cstdbool>
#include <cstddef>
#include <cstdlib>
extern "C" {
#else
#include <stdbool.h>
#include <stddef.h>
#include <stdlib.h>
#endif /* __cplusplus */

#include "hsm_client_data.h"
#include "hsm_key_interface.h"

typedef void* HSM_CLIENT_STORE_HANDLE;
typedef int (*HSM_CLIENT_STORE_CREATE)(const char* store_name, uint64_t auto_generated_ca_lifetime);
typedef int (*HSM_CLIENT_STORE_DESTROY)(const char* store_name);
typedef HSM_CLIENT_STORE_HANDLE (*HSM_CLIENT_STORE_OPEN)(const char* store_name);
typedef int (*HSM_CLIENT_STORE_CLOSE)(HSM_CLIENT_STORE_HANDLE handle);
typedef int (*HSM_CLIENT_STORE_REMOVE_KEY)(HSM_CLIENT_STORE_HANDLE handle, HSM_KEY_T key_type, const char* key_name);
typedef KEY_HANDLE (*HSM_CLIENT_STORE_OPEN_KEY)(HSM_CLIENT_STORE_HANDLE handle, HSM_KEY_T key_type, const char* key_name);
typedef int (*HSM_CLIENT_STORE_CLOSE_KEY)(HSM_CLIENT_STORE_HANDLE handle, KEY_HANDLE key_handle);
typedef int (*HSM_CLIENT_STORE_INSERT_SAS_KEY)(HSM_CLIENT_STORE_HANDLE handle,
                                               const char* key_name,
                                               const unsigned char* key,
                                               size_t key_len);
typedef int (*HSM_CLIENT_STORE_INSERT_ENCRYPTION_KEY)(HSM_CLIENT_STORE_HANDLE handle, const char* key_name);

typedef int (*HSM_CLIENT_STORE_CREATE_PKI_CERT)
(
    HSM_CLIENT_STORE_HANDLE handle,
    CERT_PROPS_HANDLE cert_props_handle
);

typedef CERT_INFO_HANDLE (*HSM_CLIENT_STORE_GET_PKI_CERT)
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
);

typedef int (*HSM_CLIENT_STORE_REMOVE_PKI_CERT)
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
);

typedef int (*HSM_CLIENT_STORE_INSERT_PKI_TRUSTED_CERT)
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias,
	const char* file_name
);

typedef CERT_INFO_HANDLE (*HSM_CLIENT_STORE_GET_PKI_TRUSTED_CERTS)
(
	HSM_CLIENT_STORE_HANDLE handle
);

typedef int (*HSM_CLIENT_STORE_REMOVE_PKI_TRUSTED_CERT)
(
	HSM_CLIENT_STORE_HANDLE handle,
    const char* alias
);

struct HSM_CLIENT_STORE_INTERFACE_TAG {
    HSM_CLIENT_STORE_CREATE hsm_client_store_create;
    HSM_CLIENT_STORE_DESTROY hsm_client_store_destroy;
    HSM_CLIENT_STORE_OPEN hsm_client_store_open;
    HSM_CLIENT_STORE_CLOSE hsm_client_store_close;
    HSM_CLIENT_STORE_OPEN_KEY hsm_client_store_open_key;
    HSM_CLIENT_STORE_CLOSE_KEY hsm_client_store_close_key;
    HSM_CLIENT_STORE_REMOVE_KEY hsm_client_store_remove_key;
    HSM_CLIENT_STORE_INSERT_SAS_KEY hsm_client_store_insert_sas_key;
    HSM_CLIENT_STORE_INSERT_ENCRYPTION_KEY hsm_client_store_insert_encryption_key;
    HSM_CLIENT_STORE_CREATE_PKI_CERT hsm_client_store_create_pki_cert;
    HSM_CLIENT_STORE_GET_PKI_CERT hsm_client_store_get_pki_cert;
    HSM_CLIENT_STORE_REMOVE_PKI_CERT hsm_client_store_remove_pki_cert;
    HSM_CLIENT_STORE_INSERT_PKI_TRUSTED_CERT hsm_client_store_insert_pki_trusted_cert;
    HSM_CLIENT_STORE_GET_PKI_TRUSTED_CERTS hsm_client_store_get_pki_trusted_certs;
    HSM_CLIENT_STORE_REMOVE_PKI_TRUSTED_CERT hsm_client_store_remove_pki_trusted_cert;
};
typedef struct HSM_CLIENT_STORE_INTERFACE_TAG HSM_CLIENT_STORE_INTERFACE;
const HSM_CLIENT_STORE_INTERFACE* hsm_client_store_interface(void);

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif //HSM_CLIENT_STORE_H
