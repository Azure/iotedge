// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef CERTIFICATE_INFO_H
#define CERTIFICATE_INFO_H

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

typedef struct CERT_DATA_INFO_TAG* CERT_INFO_HANDLE;

typedef enum PRIVATE_KEY_TYPE_TAG
{
    PRIVATE_KEY_UNKNOWN = 0,
    PRIVATE_KEY_PAYLOAD,
    PRIVATE_KEY_REFERENCE
} PRIVATE_KEY_TYPE;

/**
* @brief            Creates the certificate information object and initializes the values
*
* @param certificate    The certificate in PEM format
* @param private_key    A value or reference to the certificate private key
* @param pk_len         The length of the private key
* @param pk_type        Indicates the type of the private key either the value or reference
*
* @return           On success a valid CERT_INFO_HANDLE or NULL on failure
*/
extern CERT_INFO_HANDLE certificate_info_create(const char* certificate, const void* private_key, size_t priv_key_len, PRIVATE_KEY_TYPE pk_type);

/**
* @brief            Frees all resources associated with this object
*
* @param handle     The handle created in certificate_info_create
*
*/
extern void certificate_info_destroy(CERT_INFO_HANDLE handle);

/**
* @brief            Retrieves the certificate associated with this object
*
* @param handle     The handle created in certificate_info_create
*
* @return           On success the certificate value or NULL on failure
*/
extern const char* certificate_info_get_certificate(CERT_INFO_HANDLE handle);

/**
* @brief            Retrieves the private key value or reference
*
* @param handle     The handle created in certificate_info_create
* @param pk_len     The length of the returned private key value
*
* @return           On success the private key value or NULL on failure
*/
extern const void* certificate_info_get_private_key(CERT_INFO_HANDLE handle, size_t* priv_key_len);

/**
* @brief            Retrieves the UTC time in seconds the certificate is valid from
*
* @param handle     The handle created in certificate_info_create
*
* @return           On success the UTC time value or 0 on failure
*/
extern int64_t certificate_info_get_valid_from(CERT_INFO_HANDLE handle);

/**
* @brief            Retrieves the UTC time in seconds the certificate is valid to
*
* @param handle     The handle created in certificate_info_create
*
* @return           On success the UTC time value or 0 on failure
*/
extern int64_t certificate_info_get_valid_to(CERT_INFO_HANDLE handle);

/**
* @brief            Retrieves the type of private key
*
* @param handle     The handle created in certificate_info_create
*
* @return           On success the PRIVATE_KEY_TYPE value or PRIVATE_KEY_PAYLOAD on failure
*/
extern PRIVATE_KEY_TYPE certificate_info_private_key_type(CERT_INFO_HANDLE handle);

extern const char* certificate_info_get_chain(CERT_INFO_HANDLE handle);
extern const char* certificate_info_get_issuer(CERT_INFO_HANDLE handle);
extern const char* certificate_info_get_common_name(CERT_INFO_HANDLE handle);

#ifdef __cplusplus
}
#endif /* __cplusplus */

#endif // CERTIFICATE_INFO_H