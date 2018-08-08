#ifndef HSM_KEY_H
#define HSM_KEY_H

#ifdef __cplusplus
#include <cstdbool>
#include <cstddef>
extern "C" {
#else
#include <stdbool.h>
#include <stddef.h>
#endif

#include "azure_c_shared_utility/umock_c_prod.h"
#include "hsm_key_interface.h"

enum HSM_PKI_KEY_T_TAG
{
    HSM_PKI_KEY_RSA,
    HSM_PKI_KEY_EC
};
typedef enum HSM_PKI_KEY_T_TAG HSM_PKI_KEY_T;

struct PKI_KEY_PROPS_TAG
{
    HSM_PKI_KEY_T key_type;
    const char *ec_curve_name;
};
typedef struct PKI_KEY_PROPS_TAG PKI_KEY_PROPS;

MOCKABLE_FUNCTION(, KEY_HANDLE, create_sas_key, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, KEY_HANDLE, create_encryption_key, const unsigned char*, key, size_t, key_len);
MOCKABLE_FUNCTION(, KEY_HANDLE, create_cert_key, const char*, key_file_name);

MOCKABLE_FUNCTION(, int, generate_pki_cert_and_key, CERT_PROPS_HANDLE, cert_props_handle,
                    int, serial_number, int, ca_path_len,
                    const char*, key_file_name, const char*, cert_file_name,
                    const char*, issuer_key_file, const char*, issuer_certificate_file);
MOCKABLE_FUNCTION(, int, generate_pki_cert_and_key_with_props, CERT_PROPS_HANDLE, cert_props_handle,
                    int, serial_number, int, ca_path_len,
                    const char*, key_file_name, const char*, cert_file_name,
                    const PKI_KEY_PROPS*, key_props);
MOCKABLE_FUNCTION(, int, generate_encryption_key, unsigned char**, key, size_t*, key_size);
MOCKABLE_FUNCTION(, int, verify_certificate, const char*, certificate, const char*, certificate_key, const char*, issuer_certificate, bool*, verify_status);

#ifdef __cplusplus
}
#endif

#endif  //HSM_KEY_H
