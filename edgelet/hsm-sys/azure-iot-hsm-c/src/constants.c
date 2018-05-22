#include "hsm_constants.h"

/* IOTEDGE env variables set by iotedged */
const char* const ENV_EDGE_HOME_DIR = "IOTEDGE_HOMEDIR";
const char* const ENV_DEVICE_CA_PATH = "IOTEDGE_DEVICE_CA_PATH";
const char* const ENV_DEVICE_CA_CHAIN_PATH = "IOTEDGE_DEVICE_CA_CHAIN_PATH";
const char* const ENV_DEVICE_PK_PATH = "IOTEDGE_DEVICE_PK_PATH";
const char* const ENV_OWNER_CA_PATH = "IOTEDGE_OWNER_CA_PATH";

/* HSM directory name under IOTEDGE_HOMEDIR */
const char* const DEFAULT_EDGE_HOME_DIR_UNIX = "/var/lib/azure-iot-edge"; // note MacOS is included
const char* const DEFAULT_EDGE_HOME_DIR_WIN = "C:\\ProgramData\\azure-iot-edge\\data";
const char* const HSM_CRYPTO_DIR = "hsm";
const char* const HSM_CRYPTO_CERTS_DIR = "certs";
const char* const HSM_CRYPTO_PK_DIR = "private";

/* HSM C misc constants */
const char* const EDGE_STORE_NAME = "edgelet";
const char* const EDGELET_IDENTITY_SAS_KEY_NAME = "edgelet-identity";
const char* const OWNER_CA_ALIAS = "edge_owner_ca";
const char* const OWNER_CA_COMMON_NAME = "Test Edge Owner CA";
const char* const DEVICE_CA_COMMON_NAME = "Test Edge Device CA";
const char* const EDGE_CA_ALIAS = "iotedged_ca";
const char* const EDGE_CA_COMMON_NAME = "Test Iotedge CA";
const uint64_t CA_VALIDITY = 90 * 24 * 3600; // 90 days
