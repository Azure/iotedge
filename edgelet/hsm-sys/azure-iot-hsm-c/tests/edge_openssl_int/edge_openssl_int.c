// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include <openssl/bio.h>
#include <openssl/err.h>
#include <openssl/pem.h>
#include <openssl/x509.h>
#include <openssl/x509v3.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "hsm_client_store.h"
#include "hsm_log.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

static char* TEST_IOTEDGE_HOMEDIR = NULL;
static char* TEST_IOTEDGE_HOMEDIR_GUID = NULL;
static char* TEST_TEMP_DIR = NULL;
static char* TEST_TEMP_DIR_GUID = NULL;

#define TEST_VALIDITY         3600
#define TEST_SERIAL_NUM       1000

#define TEST_CA_CN_1            "cn_ca_1"
#define TEST_CA_CN_2            "cn_ca_2"
#define TEST_SERVER_CN_1        "cn_server_1"
#define TEST_SERVER_CN_3        "cn_server_3"
#define TEST_CLIENT_CN_1        "cn_client_1"

#define TEST_CA_ALIAS_1         "test_ca_alias_1"
#define TEST_CA_ALIAS_2         "test_ca_alias_2"
#define TEST_SERVER_ALIAS_1     "test_server_alias_1"
#define TEST_SERVER_ALIAS_3     "test_server_alias_3"
#define TEST_CLIENT_ALIAS_1     "test_client_alias_1"

#define TEST_CA_CERT_RSA_FILE_1_NAME     "ca_rsa_cert_1.cert.pem"
#define TEST_CA_CERT_RSA_FILE_2_NAME     "ca_rsa_cert_2.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_1_NAME "server_rsa_cert_1.cert.pem"
#define TEST_SERVER_CERT_RSA_FILE_3_NAME "server_rsa_cert_3.cert.pem"
#define TEST_CLIENT_CERT_RSA_FILE_1_NAME "client_rsa_cert_1.cert.pem"
static char *TEST_CA_CERT_RSA_FILE_1     = NULL;
static char *TEST_CA_CERT_RSA_FILE_2     = NULL;
static char *TEST_SERVER_CERT_RSA_FILE_1 = NULL;
static char *TEST_SERVER_CERT_RSA_FILE_3 = NULL;
static char *TEST_CLIENT_CERT_RSA_FILE_1 = NULL;

#define TEST_CA_PK_RSA_FILE_1_NAME       "ca_rsa_cert_1.key.pem"
#define TEST_CA_PK_RSA_FILE_2_NAME       "ca_rsa_cert_2.key.pem"
#define TEST_SERVER_PK_RSA_FILE_1_NAME   "server_rsa_cert_1.key.pem"
#define TEST_SERVER_PK_RSA_FILE_3_NAME   "server_rsa_cert_3.key.pem"
#define TEST_CLIENT_PK_RSA_FILE_1_NAME   "client_rsa_cert_1.key.pem"
static char *TEST_CA_PK_RSA_FILE_1       = NULL;
static char *TEST_CA_PK_RSA_FILE_2       = NULL;
static char *TEST_SERVER_PK_RSA_FILE_1   = NULL;
static char *TEST_SERVER_PK_RSA_FILE_3   = NULL;
static char *TEST_CLIENT_PK_RSA_FILE_1   = NULL;

#define TEST_CA_CERT_ECC_FILE_1_NAME     "ca_ecc_cert_1.cert.pem"
#define TEST_CA_CERT_ECC_FILE_2_NAME     "ca_ecc_cert_2.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_1_NAME "server_ecc_cert_1.cert.pem"
#define TEST_SERVER_CERT_ECC_FILE_3_NAME "server_ecc_cert_3.cert.pem"
#define TEST_CLIENT_CERT_ECC_FILE_1_NAME "client_ecc_cert_1.cert.pem"
static char *TEST_CA_CERT_ECC_FILE_1     = NULL;
static char *TEST_CA_CERT_ECC_FILE_2     = NULL;
static char *TEST_SERVER_CERT_ECC_FILE_1 = NULL;
static char *TEST_SERVER_CERT_ECC_FILE_3 = NULL;
static char *TEST_CLIENT_CERT_ECC_FILE_1 = NULL;

#define TEST_CA_PK_ECC_FILE_1_NAME       "ca_ecc_cert_1.key.pem"
#define TEST_CA_PK_ECC_FILE_2_NAME       "ca_ecc_cert_2.key.pem"
#define TEST_SERVER_PK_ECC_FILE_1_NAME   "server_ecc_cert_1.key.pem"
#define TEST_SERVER_PK_ECC_FILE_3_NAME   "server_ecc_cert_3.key.pem"
#define TEST_CLIENT_PK_ECC_FILE_1_NAME   "client_ecc_cert_1.key.pem"
static char *TEST_CA_PK_ECC_FILE_1       = NULL;
static char *TEST_CA_PK_ECC_FILE_2       = NULL;
static char *TEST_SERVER_PK_ECC_FILE_1   = NULL;
static char *TEST_SERVER_PK_ECC_FILE_3   = NULL;
static char *TEST_CLIENT_PK_ECC_FILE_1   = NULL;

#define TEST_CHAIN_FILE_PATH_NAME        "chain_file.pem"
static char *TEST_CHAIN_FILE_PATH        = NULL;

#define TEST_X509_EXT_BASIC_CONSTRIANTS         "X509v3 Basic Constraints"
#define TEST_X509_EXT_KEY_USAGE                 "X509v3 Key Usage"
#define TEST_X509_EXT_KEY_EXT_USAGE             "X509v3 Extended Key Usage"
#define TEST_X509_EXT_SAN                       "X509v3 Subject Alternative Name"
#define TEST_X509_EXT_SUBJ_KEY_IDENTIFIER       "X509v3 Subject Key Identifier"
#define TEST_X509_EXT_AUTH_KEY_IDENTIFIER       "X509v3 Authority Key Identifier"
#define TEST_X509_KEY_USAGE_DIG_SIG             "Digital Signature"
#define TEST_X509_KEY_USAGE_NON_REPUDIATION     "Non Repudiation"
#define TEST_X509_KEY_USAGE_KEY_ENCIPHER        "Key Encipherment"
#define TEST_X509_KEY_USAGE_DATA_ENCIPHER       "Data Encipherment"
#define TEST_X509_KEY_USAGE_KEY_AGREEMENT       "Key Agreement"
#define TEST_X509_KEY_USAGE_KEY_CERT_SIGN       "Certificate Sign"
#define TEST_X509_KEY_EXT_USAGE_SERVER_AUTH     "TLS Web Server Authentication"
#define TEST_X509_KEY_EXT_USAGE_CLIENT_AUTH     "TLS Web Client Authentication"

#define MAX_PATHLEN_STRING_SIZE 32
#define MAX_X509_EXT_SIZE 512

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    char *temp_dir, *guid;
    temp_dir = hsm_test_util_create_temp_dir(&guid);
    ASSERT_IS_NOT_NULL_WITH_MSG(guid, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(temp_dir, "Line:" TOSTRING(__LINE__));
    printf("Temp dir created: [%s]\r\n", temp_dir);
    *pp_temp_dir = temp_dir;
    *pp_temp_dir_guid = guid;
}

static void test_helper_teardown_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    ASSERT_IS_NOT_NULL_WITH_MSG(pp_temp_dir, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(pp_temp_dir_guid, "Line:" TOSTRING(__LINE__));

    char *temp_dir = *pp_temp_dir;
    char *guid = *pp_temp_dir_guid;
    ASSERT_IS_NOT_NULL_WITH_MSG(temp_dir, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL_WITH_MSG(guid, "Line:" TOSTRING(__LINE__));

    hsm_test_util_delete_dir(guid);
    free(temp_dir);
    free(guid);
    *pp_temp_dir = NULL;
    *pp_temp_dir_guid = NULL;
}

static char* prepare_file_path(const char* base_dir, const char* file_name)
{
    size_t path_size = get_max_file_path_size();
    char *file_path = calloc(path_size, 1);
    ASSERT_IS_NOT_NULL_WITH_MSG(file_path, "Line:" TOSTRING(__LINE__));
    int status = snprintf(file_path, path_size, "%s%s", base_dir, file_name);
    ASSERT_IS_TRUE_WITH_MSG(((status > 0) || (status < (int)path_size)), "Line:" TOSTRING(__LINE__));

    return file_path;
}

static void test_helper_setup_homedir(void)
{
    test_helper_setup_temp_dir(&TEST_IOTEDGE_HOMEDIR, &TEST_IOTEDGE_HOMEDIR_GUID);
    hsm_test_util_setenv("IOTEDGE_HOMEDIR", TEST_IOTEDGE_HOMEDIR);
    printf("IoT Edge home dir set to %s\n", TEST_IOTEDGE_HOMEDIR);
}

static CERT_PROPS_HANDLE test_helper_create_certificate_props
(
    const char *common_name,
    const char *alias,
    const char *issuer_alias,
    CERTIFICATE_TYPE type,
    uint64_t validity
)
{
    CERT_PROPS_HANDLE cert_props_handle = cert_properties_create();
    ASSERT_IS_NOT_NULL_WITH_MSG(cert_props_handle, "Line:" TOSTRING(__LINE__));
    set_validity_seconds(cert_props_handle, validity);
    set_common_name(cert_props_handle, common_name);
    set_country_name(cert_props_handle, "US");
    set_state_name(cert_props_handle, "Test State");
    set_locality(cert_props_handle, "Test Locality");
    set_organization_name(cert_props_handle, "Test Org");
    set_organization_unit(cert_props_handle, "Test Org Unit");
    set_certificate_type(cert_props_handle, type);
    set_issuer_alias(cert_props_handle, issuer_alias);
    set_alias(cert_props_handle, alias);
    return cert_props_handle;
}

static void test_helper_generate_pki_certificate
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    int path_len,
    const char *private_key_file,
    const char *cert_file,
    const char *issuer_private_key_file,
    const char *issuer_cert_file
)
{
    int result = generate_pki_cert_and_key(cert_props_handle,
                                           serial_num,
                                           path_len,
                                           private_key_file,
                                           cert_file,
                                           issuer_private_key_file,
                                           issuer_cert_file);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
}

static void test_helper_generate_self_signed
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    int path_len,
    const char *private_key_file,
    const char *cert_file,
    const PKI_KEY_PROPS *key_props
)
{
    int result = generate_pki_cert_and_key_with_props(cert_props_handle,
                                                      serial_num,
                                                      path_len,
                                                      private_key_file,
                                                      cert_file,
                                                      key_props);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));
}

void test_helper_server_chain_validator(const PKI_KEY_PROPS *key_props)
{
    // arrange
    CERT_PROPS_HANDLE ca_root_handle;
    CERT_PROPS_HANDLE int_ca_root_handle;
    CERT_PROPS_HANDLE server_root_handle;

    ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                          TEST_CA_ALIAS_1,
                                                          TEST_CA_ALIAS_1,
                                                          CERTIFICATE_TYPE_CA,
                                                          TEST_VALIDITY);

    int_ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_2,
                                                              TEST_CA_ALIAS_2,
                                                              TEST_CA_ALIAS_1,
                                                              CERTIFICATE_TYPE_CA,
                                                              TEST_VALIDITY);

    server_root_handle = test_helper_create_certificate_props(TEST_SERVER_CN_3,
                                                              TEST_SERVER_ALIAS_3,
                                                              TEST_CA_ALIAS_2,
                                                              CERTIFICATE_TYPE_SERVER,
                                                              TEST_VALIDITY);

    // act
    test_helper_generate_self_signed(ca_root_handle,
                                     TEST_SERIAL_NUM + 1,
                                     2,
                                     TEST_CA_PK_RSA_FILE_1,
                                     TEST_CA_CERT_RSA_FILE_1,
                                     key_props);

    test_helper_generate_pki_certificate(int_ca_root_handle,
                                         TEST_SERIAL_NUM + 2,
                                         1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         TEST_SERIAL_NUM + 3,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    // assert
    bool cert_verified = false;
    int status = verify_certificate(TEST_CA_CERT_RSA_FILE_2, TEST_CA_PK_RSA_FILE_2, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_2, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_SERVER_CERT_RSA_FILE_3, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_TRUE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    ASSERT_IS_FALSE_WITH_MSG(cert_verified, "Line:" TOSTRING(__LINE__));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, cert_verified, "Line:" TOSTRING(__LINE__));

    // cleanup
    delete_file(TEST_SERVER_PK_RSA_FILE_3);
    delete_file(TEST_SERVER_CERT_RSA_FILE_3);
    delete_file(TEST_CA_PK_RSA_FILE_2);
    delete_file(TEST_CA_CERT_RSA_FILE_2);
    delete_file(TEST_CA_PK_RSA_FILE_1);
    delete_file(TEST_CA_CERT_RSA_FILE_1);
    cert_properties_destroy(server_root_handle);
    cert_properties_destroy(int_ca_root_handle);
    cert_properties_destroy(ca_root_handle);
}

static X509* test_helper_load_certificate_file(const char* cert_file_name)
{
    BIO* cert_file = BIO_new_file(cert_file_name, "r");
    ASSERT_IS_NOT_NULL_WITH_MSG(cert_file, "Line:" TOSTRING(__LINE__));
    X509* x509_cert = PEM_read_bio_X509(cert_file, NULL, NULL, NULL);
    // make sure the file is closed before asserting below
    BIO_free_all(cert_file);
    ASSERT_IS_NOT_NULL_WITH_MSG(x509_cert, "Line:" TOSTRING(__LINE__));
    return x509_cert;
}

// parts of the implementation taken from X509V3_extensions_print
// https://github.com/openssl/openssl/blob/32f803d88ec3df7f95dfbf840c271f7438ce3357/crypto/x509v3/v3_prn.c#L138
static void test_helper_validate_extension
(
    X509* input_test_cert,
    const char *ext_name,
    size_t expected_num_ext_name_entries,
    const char * const* expected_vals,
    size_t num_expted_vals
)
{
    size_t nid_match = 0, match_count = 0;
    X509_CINF *cert_inf = NULL;
    STACK_OF(X509_EXTENSION) *ext_list;

    cert_inf = input_test_cert->cert_info;
    ext_list = cert_inf->extensions;
    ASSERT_IS_TRUE_WITH_MSG((sk_X509_EXTENSION_num(ext_list) > 0), "Found zero extensions");

    for (int ext_idx=0; ext_idx < sk_X509_EXTENSION_num(ext_list); ext_idx++)
    {
        int sz;
        char output_buffer[MAX_X509_EXT_SIZE];
        ASN1_OBJECT *obj;
        X509_EXTENSION *ext;

        ext = sk_X509_EXTENSION_value(ext_list, ext_idx);
        ASSERT_IS_NOT_NULL_WITH_MSG(ext, "Line:" TOSTRING(__LINE__));

        obj = X509_EXTENSION_get_object(ext);
        ASSERT_IS_NOT_NULL_WITH_MSG(obj, "Line:" TOSTRING(__LINE__));

        memset(output_buffer, 0, MAX_X509_EXT_SIZE);
        sz = i2t_ASN1_OBJECT(output_buffer, MAX_X509_EXT_SIZE, obj);
        // if size is larger use the call twice first to get size and then allocate or increase MAX_X509_EXT_SIZE
        ASSERT_IS_FALSE_WITH_MSG((sz > MAX_X509_EXT_SIZE), "Unexpected buffer size");

        if (strcmp(ext_name, output_buffer) == 0)
        {
            long sz;
            char *memst = NULL;

            printf("\r\nTesting Extension Contents: [%s]\r\n", output_buffer);

            BIO *mem_bio = BIO_new(BIO_s_mem());
            ASSERT_IS_NOT_NULL_WITH_MSG(mem_bio, "Line:" TOSTRING(__LINE__));
            // print the extension contents into the mem_bio
            X509V3_EXT_print(mem_bio, ext, 0, 0);
            sz = BIO_get_mem_data(mem_bio, &memst);
            ASSERT_IS_TRUE_WITH_MSG((sz > 0), "Line:" TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL_WITH_MSG(memst, "Line:" TOSTRING(__LINE__));
            char *output_str = calloc(sz + 1, 1);
            ASSERT_IS_NOT_NULL_WITH_MSG(output_str, "Line:" TOSTRING(__LINE__));
            memcpy(output_str, memst, sz);
            printf("\r\n Obtained Extension value from cert. Size:[%ld] Data:[%s]", sz, output_str);
            for (size_t idx = 0; idx < num_expted_vals; idx++)
            {
                if (expected_vals != NULL)
                {
                    if (strstr(output_str, (char*)expected_vals[idx]) != NULL)
                    {
                        printf("\r\n MATCHED[%s]\r\n", (char*)expected_vals[idx]);
                        match_count++;
                    }
                }
            }
            free(output_str);
            BIO_free_all(mem_bio);
            nid_match++;
        }
    }

    ASSERT_ARE_EQUAL_WITH_MSG(size_t, expected_num_ext_name_entries, nid_match,  "NID match count failed");
    ASSERT_ARE_EQUAL_WITH_MSG(size_t, num_expted_vals, match_count,  "Match count failed");
}

static void test_helper_validate_all_x509_extensions
(
    const char *cert_file_path,
    CERT_PROPS_HANDLE handle,
    int pathlen
)
{
    char const** expected_key_usage_vals; size_t expected_key_usage_vals_size;
    char const** expected_ext_key_usage_vals; size_t expected_ext_key_usage_vals_size;
    char const** expected_basic_constraints_vals; size_t expected_basic_constraints_val_sizes;
    const char * const* sans; size_t expected_san_entry_val_sizes;
    char expected_path_len_string[MAX_PATHLEN_STRING_SIZE];
    size_t idx;
    bool test_path_len = false;

    CERTIFICATE_TYPE cert_type = get_certificate_type(handle);
    ASSERT_ARE_NOT_EQUAL_WITH_MSG(size_t, CERTIFICATE_TYPE_UNKNOWN, cert_type, "Unknown cert type not supported");

    // setup common extension expected values such as basic constraints and SAN entries
    idx = 0;
    expected_basic_constraints_val_sizes = (pathlen != -1)? 2 : 1;
    expected_basic_constraints_vals = calloc(expected_basic_constraints_val_sizes, sizeof(void*));
    expected_basic_constraints_vals[idx++] = (cert_type == CERTIFICATE_TYPE_CA) ? "CA:TRUE" : "CA:FALSE";
    if (pathlen != -1)
    {
        memset(expected_path_len_string, 0, MAX_PATHLEN_STRING_SIZE);
        snprintf(expected_path_len_string, MAX_PATHLEN_STRING_SIZE, "pathlen:%d", pathlen);
        expected_basic_constraints_vals[idx++] = expected_path_len_string;
    }

    sans = get_san_entries(handle, &expected_san_entry_val_sizes);

    if (cert_type == CERTIFICATE_TYPE_CA)
    {
        idx = 0;
        expected_key_usage_vals_size = 2;
        expected_key_usage_vals = calloc(expected_key_usage_vals_size, sizeof(SIZED_BUFFER));
        ASSERT_IS_NOT_NULL_WITH_MSG(expected_key_usage_vals, "Line:" TOSTRING(__LINE__));
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DIG_SIG;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_CERT_SIGN;

        idx = 0;
        expected_ext_key_usage_vals_size = 0;
        expected_ext_key_usage_vals = NULL;
    }
    else if (cert_type == CERTIFICATE_TYPE_CLIENT)
    {
        idx = 0;
        expected_key_usage_vals_size = 4;
        expected_key_usage_vals = calloc(expected_key_usage_vals_size, sizeof(void*));
        ASSERT_IS_NOT_NULL_WITH_MSG(expected_key_usage_vals, "Line:" TOSTRING(__LINE__));
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DIG_SIG;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_NON_REPUDIATION;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DATA_ENCIPHER;

        idx = 0;
        expected_ext_key_usage_vals_size = 1;
        expected_ext_key_usage_vals = calloc(expected_ext_key_usage_vals_size, sizeof(void*));
        ASSERT_IS_NOT_NULL_WITH_MSG(expected_ext_key_usage_vals, "Line:" TOSTRING(__LINE__));
        expected_ext_key_usage_vals[idx++] = TEST_X509_KEY_EXT_USAGE_CLIENT_AUTH;
    }
    else
    {
        idx = 0;
        expected_key_usage_vals_size = 5;
        expected_key_usage_vals = calloc(expected_key_usage_vals_size, sizeof(void*));
        ASSERT_IS_NOT_NULL_WITH_MSG(expected_key_usage_vals, "Line:" TOSTRING(__LINE__));
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DIG_SIG;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_NON_REPUDIATION;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DATA_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_AGREEMENT;

        idx = 0;
        expected_ext_key_usage_vals_size = 1;
        expected_ext_key_usage_vals = calloc(expected_ext_key_usage_vals_size, sizeof(SIZED_BUFFER));
        ASSERT_IS_NOT_NULL_WITH_MSG(expected_ext_key_usage_vals, "Line:" TOSTRING(__LINE__));
        expected_ext_key_usage_vals[idx++] = TEST_X509_KEY_EXT_USAGE_SERVER_AUTH;
    }
    X509* cert = test_helper_load_certificate_file(cert_file_path);
    test_helper_validate_extension(cert, TEST_X509_EXT_BASIC_CONSTRIANTS, 1, expected_basic_constraints_vals, expected_basic_constraints_val_sizes);
    test_helper_validate_extension(cert, TEST_X509_EXT_SAN, expected_san_entry_val_sizes, sans, expected_san_entry_val_sizes);
    test_helper_validate_extension(cert, TEST_X509_EXT_KEY_USAGE, 1, expected_key_usage_vals, expected_key_usage_vals_size);
    test_helper_validate_extension(cert, TEST_X509_EXT_KEY_EXT_USAGE, (expected_ext_key_usage_vals_size>0)?1:0, expected_ext_key_usage_vals, expected_ext_key_usage_vals_size);
    test_helper_validate_extension(cert, TEST_X509_EXT_SUBJ_KEY_IDENTIFIER, 1, NULL, 0);
    test_helper_validate_extension(cert, TEST_X509_EXT_AUTH_KEY_IDENTIFIER, 1, NULL, 0);

    // cleanup
    X509_free(cert);
    if (expected_basic_constraints_vals) free((void*)expected_basic_constraints_vals);
    if (expected_key_usage_vals) free((void*)expected_key_usage_vals);
    if (expected_ext_key_usage_vals) free((void*)expected_ext_key_usage_vals);
}

void test_helper_x509_ext_validator(const PKI_KEY_PROPS *key_props)
{
    // arrange
    CERT_PROPS_HANDLE ca_root_handle;
    CERT_PROPS_HANDLE int_ca_root_handle;
    CERT_PROPS_HANDLE server_root_handle;
    CERT_PROPS_HANDLE client_root_handle;
    int status;

    ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                          TEST_CA_ALIAS_1,
                                                          TEST_CA_ALIAS_1,
                                                          CERTIFICATE_TYPE_CA,
                                                          TEST_VALIDITY);

    int_ca_root_handle = test_helper_create_certificate_props(TEST_CA_CN_2,
                                                              TEST_CA_ALIAS_2,
                                                              TEST_CA_ALIAS_1,
                                                              CERTIFICATE_TYPE_CA,
                                                              TEST_VALIDITY);

    server_root_handle = test_helper_create_certificate_props(TEST_SERVER_CN_3,
                                                              TEST_SERVER_ALIAS_3,
                                                              TEST_CA_ALIAS_2,
                                                              CERTIFICATE_TYPE_SERVER,
                                                              TEST_VALIDITY);

    client_root_handle = test_helper_create_certificate_props(TEST_SERVER_CN_3,
                                                              TEST_CLIENT_ALIAS_1,
                                                              TEST_CA_ALIAS_2,
                                                              CERTIFICATE_TYPE_CLIENT,
                                                              TEST_VALIDITY);

    // add SAN entries
    const char* ca_san_list[] = {"URI:edgetest://ca/root/pathlen/2"};
    const char* int_ca_san_list[] = {"URI:edgetest://ca/int/pathlen/1"};
    const char* server_san_list[] = {"URI:edgetest://server/test1", "DNS:test.contoso.com"};
    const char* client_san_list[] = {"URI:edgetest://client/test2", "email:test@contoso.com"};
    status = set_san_entries(ca_root_handle, ca_san_list, sizeof(ca_san_list)/sizeof(ca_san_list[0]));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    status = set_san_entries(int_ca_root_handle, int_ca_san_list, sizeof(int_ca_san_list)/sizeof(int_ca_san_list[0]));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    status = set_san_entries(server_root_handle, server_san_list, sizeof(server_san_list)/sizeof(server_san_list[0]));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
    status = set_san_entries(client_root_handle, client_san_list, sizeof(client_san_list)/sizeof(client_san_list[0]));
    ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

    // act
    test_helper_generate_self_signed(ca_root_handle,
                                     TEST_SERIAL_NUM + 1,
                                     2,
                                     TEST_CA_PK_RSA_FILE_1,
                                     TEST_CA_CERT_RSA_FILE_1,
                                     key_props);

    test_helper_generate_pki_certificate(int_ca_root_handle,
                                         TEST_SERIAL_NUM + 2,
                                         1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         TEST_SERIAL_NUM + 3,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    test_helper_generate_pki_certificate(client_root_handle,
                                         TEST_SERIAL_NUM + 4,
                                         0,
                                         TEST_CLIENT_PK_RSA_FILE_1,
                                         TEST_CLIENT_CERT_RSA_FILE_1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    // assert
    test_helper_validate_all_x509_extensions(TEST_CA_CERT_RSA_FILE_1, ca_root_handle, 2);
    test_helper_validate_all_x509_extensions(TEST_CA_CERT_RSA_FILE_2, int_ca_root_handle, 1);
    test_helper_validate_all_x509_extensions(TEST_SERVER_CERT_RSA_FILE_3, server_root_handle, -1);
    test_helper_validate_all_x509_extensions(TEST_CLIENT_CERT_RSA_FILE_1, client_root_handle, -1);

    // cleanup
    delete_file(TEST_CLIENT_PK_RSA_FILE_1);
    delete_file(TEST_CLIENT_CERT_RSA_FILE_1);
    delete_file(TEST_SERVER_PK_RSA_FILE_3);
    delete_file(TEST_SERVER_CERT_RSA_FILE_3);
    delete_file(TEST_CA_PK_RSA_FILE_2);
    delete_file(TEST_CA_CERT_RSA_FILE_2);
    delete_file(TEST_CA_PK_RSA_FILE_1);
    delete_file(TEST_CA_CERT_RSA_FILE_1);
    cert_properties_destroy(client_root_handle);
    cert_properties_destroy(server_root_handle);
    cert_properties_destroy(int_ca_root_handle);
    cert_properties_destroy(ca_root_handle);
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_int_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        test_helper_setup_homedir();
        test_helper_setup_temp_dir(&TEST_TEMP_DIR, &TEST_TEMP_DIR_GUID);

        TEST_CA_CERT_RSA_FILE_1     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_RSA_FILE_1_NAME);
        TEST_CA_CERT_RSA_FILE_2     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_RSA_FILE_2_NAME);
        TEST_SERVER_CERT_RSA_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_RSA_FILE_1_NAME);
        TEST_SERVER_CERT_RSA_FILE_3 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_RSA_FILE_3_NAME);
        TEST_CLIENT_CERT_RSA_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_CERT_RSA_FILE_1_NAME);

        TEST_CA_PK_RSA_FILE_1       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_RSA_FILE_1_NAME);
        TEST_CA_PK_RSA_FILE_2       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_RSA_FILE_2_NAME);
        TEST_SERVER_PK_RSA_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_RSA_FILE_1_NAME);
        TEST_SERVER_PK_RSA_FILE_3   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_RSA_FILE_3_NAME);
        TEST_CLIENT_PK_RSA_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_PK_RSA_FILE_1_NAME);

        TEST_CA_CERT_ECC_FILE_1     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_ECC_FILE_1_NAME);
        TEST_CA_CERT_ECC_FILE_2     = prepare_file_path(TEST_TEMP_DIR, TEST_CA_CERT_ECC_FILE_2_NAME);
        TEST_SERVER_CERT_ECC_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_ECC_FILE_1_NAME);
        TEST_SERVER_CERT_ECC_FILE_3 = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_CERT_ECC_FILE_3_NAME);
        TEST_CLIENT_CERT_ECC_FILE_1 = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_CERT_ECC_FILE_1_NAME);

        TEST_CA_PK_ECC_FILE_1       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_ECC_FILE_1_NAME);
        TEST_CA_PK_ECC_FILE_2       = prepare_file_path(TEST_TEMP_DIR, TEST_CA_PK_ECC_FILE_2_NAME);
        TEST_SERVER_PK_ECC_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_ECC_FILE_1_NAME);
        TEST_SERVER_PK_ECC_FILE_3   = prepare_file_path(TEST_TEMP_DIR, TEST_SERVER_PK_ECC_FILE_3_NAME);
        TEST_CLIENT_PK_ECC_FILE_1   = prepare_file_path(TEST_TEMP_DIR, TEST_CLIENT_PK_ECC_FILE_1_NAME);

        TEST_CHAIN_FILE_PATH = prepare_file_path(TEST_TEMP_DIR, TEST_CHAIN_FILE_PATH_NAME);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        free(TEST_CA_CERT_RSA_FILE_1); TEST_CA_CERT_RSA_FILE_1 = NULL;
        free(TEST_CA_CERT_RSA_FILE_2); TEST_CA_CERT_RSA_FILE_2 = NULL;
        free(TEST_SERVER_CERT_RSA_FILE_1); TEST_SERVER_CERT_RSA_FILE_1 = NULL;
        free(TEST_SERVER_CERT_RSA_FILE_3); TEST_SERVER_CERT_RSA_FILE_3 = NULL;
        free(TEST_CLIENT_CERT_RSA_FILE_1); TEST_CLIENT_CERT_RSA_FILE_1 = NULL;

        free(TEST_CA_PK_RSA_FILE_1); TEST_CA_PK_RSA_FILE_1 = NULL;
        free(TEST_CA_PK_RSA_FILE_2); TEST_CA_PK_RSA_FILE_2 = NULL;
        free(TEST_SERVER_PK_RSA_FILE_1); TEST_SERVER_PK_RSA_FILE_1 = NULL;
        free(TEST_SERVER_PK_RSA_FILE_3); TEST_SERVER_PK_RSA_FILE_3 = NULL;
        free(TEST_CLIENT_PK_RSA_FILE_1); TEST_CLIENT_PK_RSA_FILE_1 = NULL;

        free(TEST_CA_CERT_ECC_FILE_1); TEST_CA_CERT_ECC_FILE_1 = NULL;
        free(TEST_CA_CERT_ECC_FILE_2); TEST_CA_CERT_ECC_FILE_2 = NULL;
        free(TEST_SERVER_CERT_ECC_FILE_1); TEST_SERVER_CERT_ECC_FILE_1 = NULL;
        free(TEST_SERVER_CERT_ECC_FILE_3); TEST_SERVER_CERT_ECC_FILE_3 = NULL;
        free(TEST_CLIENT_CERT_ECC_FILE_1); TEST_CLIENT_CERT_ECC_FILE_1 = NULL;

        free(TEST_CA_PK_ECC_FILE_1); TEST_CA_PK_ECC_FILE_1 = NULL;
        free(TEST_CA_PK_ECC_FILE_2); TEST_CA_PK_ECC_FILE_2 = NULL;
        free(TEST_SERVER_PK_ECC_FILE_1); TEST_SERVER_PK_ECC_FILE_1 = NULL;
        free(TEST_SERVER_PK_ECC_FILE_3); TEST_SERVER_PK_ECC_FILE_3 = NULL;
        free(TEST_CLIENT_PK_ECC_FILE_1); TEST_CLIENT_PK_ECC_FILE_1 = NULL;

        free(TEST_CHAIN_FILE_PATH); TEST_CHAIN_FILE_PATH = NULL;

        test_helper_teardown_temp_dir(&TEST_TEMP_DIR, &TEST_TEMP_DIR_GUID);
        test_helper_teardown_temp_dir(&TEST_IOTEDGE_HOMEDIR, &TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(test_self_signed_rsa_server)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_SERVER_CN_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 CERTIFICATE_TYPE_SERVER,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_1,
                                         TEST_SERVER_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_SERVER_PK_RSA_FILE_1);
        delete_file(TEST_SERVER_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_rsa_client)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CLIENT_CN_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CLIENT,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         0,
                                         TEST_CLIENT_PK_RSA_FILE_1,
                                         TEST_CLIENT_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CLIENT_PK_RSA_FILE_1);
        delete_file(TEST_CLIENT_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_non_ca_cert_with_non_zero_path_fails)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_SERVER_CN_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 CERTIFICATE_TYPE_SERVER,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        int result = generate_pki_cert_and_key_with_props(cert_props_handle,
                                                          TEST_SERIAL_NUM,
                                                          2,
                                                          TEST_SERVER_PK_RSA_FILE_1,
                                                          TEST_SERVER_CERT_RSA_FILE_1,
                                                          &key_props);
        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, result, "Line:" TOSTRING(__LINE__));

        // cleanup
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_rsa_ca)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CA,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_RSA_FILE_1);
        delete_file(TEST_CA_CERT_RSA_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

#if USE_ECC_KEYS
    TEST_FUNCTION(test_self_signed_ecc_server)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_SERVER_CN_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 TEST_SERVER_ALIAS_1,
                                                                 CERTIFICATE_TYPE_SERVER,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         0,
                                         TEST_SERVER_PK_ECC_FILE_1,
                                         TEST_SERVER_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_SERVER_PK_ECC_FILE_1);
        delete_file(TEST_SERVER_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_ecc_client)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CLIENT_CN_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 TEST_CLIENT_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CLIENT,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         0,
                                         TEST_CLIENT_PK_ECC_FILE_1,
                                         TEST_CLIENT_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CLIENT_PK_ECC_FILE_1);
        delete_file(TEST_CLIENT_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }

    TEST_FUNCTION(test_self_signed_ecc_ca)
    {
        // arrange
        CERT_PROPS_HANDLE cert_props_handle;
        cert_props_handle = test_helper_create_certificate_props(TEST_CA_CN_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 TEST_CA_ALIAS_1,
                                                                 CERTIFICATE_TYPE_CA,
                                                                 TEST_VALIDITY);
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act
        test_helper_generate_self_signed(cert_props_handle,
                                         TEST_SERIAL_NUM,
                                         2,
                                         TEST_CA_PK_ECC_FILE_1,
                                         TEST_CA_CERT_ECC_FILE_1,
                                         &key_props);

        // assert

        // cleanup
        delete_file(TEST_CA_PK_ECC_FILE_1);
        delete_file(TEST_CA_CERT_ECC_FILE_1);
        cert_properties_destroy(cert_props_handle);
    }
#endif //USE_ECC_KEYS

    TEST_FUNCTION(test_self_signed_rsa_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

#if USE_ECC_KEYS
    TEST_FUNCTION(test_self_signed_ecc_default_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, NULL };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }

    TEST_FUNCTION(test_self_signed_ecc_primes_curve_server_chain)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_EC, "prime256v1" };

        // act, assert
        test_helper_server_chain_validator(&key_props);

        // cleanup
    }
#endif //USE_ECC_KEYS

    TEST_FUNCTION(test_x509v3_extensions)
    {
        // arrange
        PKI_KEY_PROPS key_props = { HSM_PKI_KEY_RSA, NULL };

        // act, assert
        test_helper_x509_ext_validator(&key_props);

        //cleanup
    }

END_TEST_SUITE(edge_openssl_int_tests)
