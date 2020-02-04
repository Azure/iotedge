// Copyright (c) Microsoft. All rights reserved.
#include "hsm_constants.h"

/* IOTEDGE env variables set by iotedged */
const char* const ENV_EDGE_HOME_DIR = "IOTEDGE_HOMEDIR";
const char* const ENV_DEVICE_CA_PATH = "IOTEDGE_DEVICE_CA_CERT";
const char* const ENV_DEVICE_PK_PATH = "IOTEDGE_DEVICE_CA_PK";
const char* const ENV_TRUSTED_CA_CERTS_PATH = "IOTEDGE_TRUSTED_CA_CERTS";
const char* const ENV_REGISTRATION_ID = "IOTEDGE_REGISTRATION_ID";
const char* const ENV_DEVICE_ID_CERTIFICATE_PATH = "IOTEDGE_DEVICE_IDENTITY_CERT";
const char* const ENV_DEVICE_ID_PRIVATE_KEY_PATH = "IOTEDGE_DEVICE_IDENTITY_PK";

/* HSM directory name under IOTEDGE_HOMEDIR */
const char* const DEFAULT_EDGE_HOME_DIR_UNIX = "/var/lib/iotedge"; // note MacOS is included
const char* const DEFAULT_EDGE_BASE_DIR_ENV_WIN = "ProgramData";
const char* const DEFAULT_EDGE_HOME_DIR_WIN = "iotedge";
const char* const HSM_CRYPTO_DIR = "hsm";
const char* const HSM_CRYPTO_CERTS_DIR = "certs";
const char* const HSM_CRYPTO_PK_DIR = "private";

/* HSM C misc constants */
const char* const EDGE_STORE_NAME = "edgelet";
const char* const EDGELET_IDENTITY_SAS_KEY_NAME = "edgelet-identity";
const char* const EDGELET_ENC_KEY_NAME = "edgelet-master";
const char* const DEFAULT_TRUSTED_CA_ALIAS = "edgelet-trusted-ca";
const char* const OWNER_CA_ALIAS = "edge_owner_ca";
const char* const OWNER_CA_COMMON_NAME = "Test Edge Owner CA";
const char* const DEVICE_CA_COMMON_NAME = "Test Edge Device CA";
const char* const EDGE_CA_ALIAS = "iotedged_ca";
const char* const EDGE_CA_COMMON_NAME = "Test Iotedge CA";
const char* const EDGE_DEVICE_ALIAS = "edgelet_device";

// Note: `iotedge check` has a warning message that references the 90-day expiry in `fn settings_certificates`.
// Update that when changing the value here.
const uint64_t CA_VALIDITY = 90 * 24 * 3600; // 90 days
