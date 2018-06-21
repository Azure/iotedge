#ifndef HSM_CONSTANTS_H
#define HSM_CONSTANTS_H

#include <stdint.h>

/* IOTEDGE env variables set by iotedged */
extern const char* const ENV_EDGE_HOME_DIR;
extern const char* const ENV_DEVICE_CA_PATH;
extern const char* const ENV_DEVICE_PK_PATH;
extern const char* const ENV_TRUSTED_CA_CERTS_PATH;

/* HSM directory name under IOTEDGE_HOMEDIR */
extern const char* const DEFAULT_EDGE_HOME_DIR_UNIX;
extern const char* const DEFAULT_EDGE_BASE_DIR_ENV_WIN;
extern const char* const DEFAULT_EDGE_HOME_DIR_WIN;
extern const char* const HSM_CRYPTO_DIR;
extern const char* const HSM_CRYPTO_CERTS_DIR;
extern const char* const HSM_CRYPTO_PK_DIR;

/* HSM C misc constants */
extern const char* const EDGE_STORE_NAME;
extern const char* const EDGELET_IDENTITY_SAS_KEY_NAME;
extern const char* const EDGELET_ENC_KEY_NAME;
extern const char* const DEFAULT_TRUSTED_CA_ALIAS;
extern const char* const OWNER_CA_ALIAS;
extern const char* const OWNER_CA_COMMON_NAME;
extern const char* const DEVICE_CA_COMMON_NAME;
extern const char* const EDGE_CA_ALIAS;
extern const char* const EDGE_CA_COMMON_NAME;
extern const uint64_t CA_VALIDITY;

#endif  //HSM_CONSTANTS_H
