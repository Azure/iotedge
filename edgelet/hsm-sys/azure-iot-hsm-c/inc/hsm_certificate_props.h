// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef HSM_CERTIFICATE_PROPS_H
#define HSM_CERTIFICATE_PROPS_H

#ifdef __cplusplus
#include <cstddef>
#include <cstdlib>
#include <cstdint>
extern "C" {
#else
#include <stddef.h>
#include <stdlib.h>
#include <stdint.h>
#endif /* __cplusplus */

/** defines whether the HSM API supports SAN entries */
#define HSM_FEATURE_CERTIFICATE_SAN

typedef struct HSM_CERT_PROPS_TAG* CERT_PROPS_HANDLE;

typedef enum CERTIFICATE_TYPE_TAG
{
    CERTIFICATE_TYPE_UNKNOWN = 0,
    CERTIFICATE_TYPE_CLIENT,
    CERTIFICATE_TYPE_SERVER,
    CERTIFICATE_TYPE_CA
} CERTIFICATE_TYPE;

/**
* @brief    Creates a certificate property handle to be used in set properties
*           of a certificate
*
* @return   A valid handle on success, NULL on failure
*/
extern CERT_PROPS_HANDLE cert_properties_create(void);

/**
* @brief            Deallocates all info a certificate props handle
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
*/
extern void cert_properties_destroy(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the number of seconds a certificate will be valid from creation
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param validity_secs  The number of seconds for the cert is valid, must be greater than 0
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_validity_seconds(CERT_PROPS_HANDLE handle, uint64_t validity_secs);

/**
* @brief                Gets the number of mins a certificate will be valid from creation
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The number of seconds a cert is valid, 0 failure
*/
extern uint64_t get_validity_seconds(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the common name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param common_name    The common name to be set on the certificate
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_common_name(CERT_PROPS_HANDLE handle, const char* common_name);

/**
* @brief                Gets the common name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The common name that shall be set on the certificate
*/
extern const char* get_common_name(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the country on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param country_name   The country name to be set on the certificate.  Must be 2 letters
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_country_name(CERT_PROPS_HANDLE handle, const char* country_name);

/**
* @brief                Gets the country on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The country that shall be set on the certificate
*/
extern const char* get_country_name(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the state name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param state_name     The state name to be set on the certificate
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_state_name(CERT_PROPS_HANDLE handle, const char* state_name);

/**
* @brief                Gets the state name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The state name that shall be set on the certificate
*/
extern const char* get_state_name(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the locality on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param locality       The locality to be set on the certificate
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_locality(CERT_PROPS_HANDLE handle, const char* locality);

/**
* @brief                Gets the locality on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The locality that shall be set on the certificate
*/
extern const char* get_locality(CERT_PROPS_HANDLE handle);

/**
* @brief                Sets the organization name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param org_name       The organization name to be set on the certificate
*
* @return               On success 0 on.  Non-zero on failure
*/
extern int set_organization_name(CERT_PROPS_HANDLE handle, const char* org_name);

/**
* @brief                Gets the organization name on the certificate
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The organization name that shall be set on the certificate
*/
extern const char* get_organization_name(CERT_PROPS_HANDLE handle);

/**
* @brief            Sets the country on the certificate
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param ou         The organization_unit to be set on the certificate
*
* @return           On success 0 on.  Non-zero on failure
*/
extern int set_organization_unit(CERT_PROPS_HANDLE handle, const char* ou);

/**
* @brief            Gets the country on the certificate
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return           The organization_unit value that is set on the certificate
*/
extern const char* get_organization_unit(CERT_PROPS_HANDLE handle);

/**
* @brief            Sets the certificate type that should be produced
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param type       A CERTIFICATE_TYPE to be requested
*
* @return           On success 0 on.  Non-zero on failure
*/
extern int set_certificate_type(CERT_PROPS_HANDLE handle, CERTIFICATE_TYPE type);

/**
* @brief                Gets the type of certificate that should be produced
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The certificate type that should be requested
*/
extern CERTIFICATE_TYPE get_certificate_type(CERT_PROPS_HANDLE handle);

/**
* @brief            Sets the certificate issuer alias
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param type       A issuer alias to be set
*
* @return           On success 0 on.  Non-zero on failure
*/
extern int set_issuer_alias(CERT_PROPS_HANDLE handle, const char* issuer_alias);

/**
* @brief                Gets the issuer alias type
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The issuer alias set on the certificate
*/
extern const char* get_issuer_alias(CERT_PROPS_HANDLE handle);

/**
* @brief            Sets the certificate alias
*
* @param handle     The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param type       A alias to be set
*
* @return           On success 0 on.  Non-zero on failure
*/
extern int set_alias(CERT_PROPS_HANDLE handle, const char* alias);

/**
* @brief                Gets the alias type
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
*
* @return               The alias set on the certificate
*/
extern const char* get_alias(CERT_PROPS_HANDLE handle);

/**
* @brief                  Sets the certificate subject alternate names
*
* @param handle           The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param san_list         A pointer to a list of string containing san entries
* @param num_entries      The number of entries in the list
*
* @return                 On success 0 on.  Non-zero on failure.
*/
extern int set_san_entries
(
    CERT_PROPS_HANDLE handle,
    const char* san_list[],
    size_t num_san_entries
);

/**
* @brief                Gets the alias type
*
* @param handle         The CERT_PROPS_HANDLE that was created by the cert_properties_create call
* @param num_entries    The number of entries in the list will be returned
*
* @return               A pointer to a list of NULL terminated strings containing the SAN entries,
*                       NULL otherwise.
*/
extern const char * const* get_san_entries(CERT_PROPS_HANDLE handle, size_t *num_entries);

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif // HSM_CERTIFICATE_PROPS_H