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
#include "azure_c_shared_utility/azure_base64.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_c_shared_utility/strings.h"
#include "testrunnerswitcher.h"
#include "test_utils.h"
#include "hsm_client_store.h"
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

#define HMAC_SHA256_DIGEST_LEN 256

static const char * TEST_RSA_ASYMMETRIC_PRIVATE_KEY =
    "-----BEGIN RSA PRIVATE KEY-----\n" \
    "MIIEpAIBAAKCAQEAlu3aHGjvNk6sdZFsczd3p0m5qyJTWsgUozTYrbJeVlTyajSj\n" \
    "zR4cdq7Xs1Cb2/wdf65mxSqC14MzmZ9nEOEyK30Uk+FOQh/ekh7kLD4AICt5+X3B\n" \
    "iV2cSJkKH+euNSFOi9lj5diTAkLnie0VXUJKNhSubyPAUgSiR5mD4paBGRaTTFSc\n" \
    "6yWEMms472IwNRLpee0uU4DaozDXv/sBOKRsYmewtVvtCsn4ew+eB1E1X9O92XeL\n" \
    "idW4N8GESuZLrfcg1vTqzZ9eZ7ZwDg5VpaomV3YBnwOo7rqHcBwnoSfJyqGRlYil\n" \
    "sTmqnfNnX87ESKRxQ1vJ+06iwXIUclnTJ7xJEwIDAQABAoIBAQCF8UX8qn+IcZ+J\n" \
    "oupdAd+1Xa9hmc/ho+j0wiR9WetwsGiGKnsnwM4/4YDZyPLY8tB3DJ514flGK1Cy\n" \
    "yA0epMvyXknRx0S9WC0c/j8+qDNSWWMhMCJ+ts3Ie9DJacFns0xSvjVyuJYWjquO\n" \
    "8xFft0HG5um7Bj5aS3R9GFc70pd1W/+vDrblcU4qX8R7LKZBLsP+MJz9dKTkt3ab\n" \
    "IYHF7NO8m6Ahp2cnZf9Q69+KNVbfu8FaJyFN2HRyRKvnwDRcxnDbXYS0cDRwBkSC\n" \
    "7ko09OsTT02W4q7Hkd1aNO2tgkdWC9t5tgCd1qDYp6lMVhnLR3oswHNQPd4U6LRM\n" \
    "FrX6XLfBAoGBAMSdFnsuMPKQL0fu20TKBjjjSUKNAaCnke0MGo4TMsA0hS4yuPqC\n" \
    "J5VPJcLM7m3wI7xtPRssTp6SHO5Feg9Riix5fV4FVU0AcQgKWIbrGgp6aXu8dz5v\n" \
    "pewWrlsWQKVO4LWsHfeqZKnv9aXPYrbida00feJxOMcrOAIrexXL9RRzAoGBAMSE\n" \
    "Q5OlUWibqbMhHsACtKu1ENQQVKKkVyJuygUQvIOYRO8//ouGSIELnknAUmjDMiIi\n" \
    "u6mqR3BdGryagO+Wv1GFWRc5rb8gzr8M5Ir4RuATbJ9+E7MrcX5dWXbXjVeelilV\n" \
    "PpDWDX5tT/Aow2NH8DIKCjk/R6I9XCgCIXH8UXDhAoGBAJW3jTP1w54h/28GSwBB\n" \
    "2qUdJl9AIrokgDGDIwGHSwEjvTqls0hHLj87SuTgyrr6vyuv/3Uesyt61f729vCN\n" \
    "ReuCA95Br2f4axoVTr5GbskF2Cc6J49q021JBDImasm2m9SboSJEJW1mZaeCmYfs\n" \
    "QHHJZAa38uVvWrIETDEX46NTAoGAOyJ111MS+UCGQ1H/F9Z4mYbl5np3jW2YjtL5\n" \
    "1aZgo9TJQZlnNoMVBEgDvLuz0LSUPHNpNzf3QVey+PghPneFYLmYwoVnxDDSJely\n" \
    "SGNHqJwPvrrIoMy83UKn7jwU2z3sf8mYBytyag3o1SLfENwP6m7c/rcNDkQanCtv\n" \
    "9wXvV+ECgYBu+JRVOCb3/7SuRgafex8OQpV3ype7M6yLiTn8I/170ma9x787VoNV\n" \
    "epaG2j1pN++0b23tclP1Klql4zmdTZtCoTkkhigQv0i/A0/hicpK92VqHdWXQs1D\n" \
    "b5ufSKwS6brLwRR6lXo3Vv9aayuXMadsE94lxmMhnX1osZUibPqAew==\n" \
    "-----END RSA PRIVATE KEY-----\n";

#define TEST_RSA_PRIVATE_KEY_FILE_NAME "rsa_test_private_key.pem"
static char* TEST_RSA_PRIVATE_KEY_FILE = NULL;

static const char * TEST_RSA_ASYMMETRIC_PUBLIC_KEY =
    "-----BEGIN PUBLIC KEY-----\n" \
    "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAlu3aHGjvNk6sdZFsczd3\n" \
    "p0m5qyJTWsgUozTYrbJeVlTyajSjzR4cdq7Xs1Cb2/wdf65mxSqC14MzmZ9nEOEy\n" \
    "K30Uk+FOQh/ekh7kLD4AICt5+X3BiV2cSJkKH+euNSFOi9lj5diTAkLnie0VXUJK\n" \
    "NhSubyPAUgSiR5mD4paBGRaTTFSc6yWEMms472IwNRLpee0uU4DaozDXv/sBOKRs\n" \
    "YmewtVvtCsn4ew+eB1E1X9O92XeLidW4N8GESuZLrfcg1vTqzZ9eZ7ZwDg5Vpaom\n" \
    "V3YBnwOo7rqHcBwnoSfJyqGRlYilsTmqnfNnX87ESKRxQ1vJ+06iwXIUclnTJ7xJ\n" \
    "EwIDAQAB\n" \
    "-----END PUBLIC KEY-----\n";

#define TEST_RSA_PUBLIC_KEY_FILE_NAME "rsa_test_public_key.pem"
static char* TEST_RSA_PUBLIC_KEY_FILE = NULL;

#define MAX_PATHLEN_STRING_SIZE 32
#define MAX_X509_EXT_SIZE 512

#define TEST_RAND_SIZE_BYTES_SMALL    5
#define TEST_RAND_SIZE_BYTES_MEDIUM   32
#define TEST_RAND_SIZE_BYTES_LARGE    256

//#############################################################################
// Test helpers
//#############################################################################

static void test_helper_setup_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    char *temp_dir, *guid;
    temp_dir = hsm_test_util_create_temp_dir(&guid);
    ASSERT_IS_NOT_NULL(guid, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(temp_dir, "Line:" MU_TOSTRING(__LINE__));
    printf("Temp dir created: [%s]\r\n", temp_dir);
    *pp_temp_dir = temp_dir;
    *pp_temp_dir_guid = guid;
}

static void test_helper_teardown_temp_dir(char **pp_temp_dir, char **pp_temp_dir_guid)
{
    ASSERT_IS_NOT_NULL(pp_temp_dir, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(pp_temp_dir_guid, "Line:" MU_TOSTRING(__LINE__));

    char *temp_dir = *pp_temp_dir;
    char *guid = *pp_temp_dir_guid;
    ASSERT_IS_NOT_NULL(temp_dir, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_NOT_NULL(guid, "Line:" MU_TOSTRING(__LINE__));

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
    ASSERT_IS_NOT_NULL(file_path, "Line:" MU_TOSTRING(__LINE__));
    int status = snprintf(file_path, path_size, "%s%s", base_dir, file_name);
    ASSERT_IS_TRUE(((status > 0) || (status < (int)path_size)), "Line:" MU_TOSTRING(__LINE__));

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
    ASSERT_IS_NOT_NULL(cert_props_handle, "Line:" MU_TOSTRING(__LINE__));
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
    int path_len,
    const char *private_key_file,
    const char *cert_file,
    const char *issuer_private_key_file,
    const char *issuer_cert_file
)
{
    int result = generate_pki_cert_and_key(cert_props_handle,
                                           path_len,
                                           private_key_file,
                                           cert_file,
                                           issuer_private_key_file,
                                           issuer_cert_file);
    ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
}

static void test_helper_generate_self_signed
(
    CERT_PROPS_HANDLE cert_props_handle,
    long serial_num,
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
    ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
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
                                         1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    // assert
    bool cert_verified = false;
    int status = verify_certificate(TEST_CA_CERT_RSA_FILE_2, TEST_CA_PK_RSA_FILE_2, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_TRUE(cert_verified, "Line:" MU_TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_2, &cert_verified);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_TRUE(cert_verified, "Line:" MU_TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_SERVER_CERT_RSA_FILE_3, &cert_verified);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_TRUE(cert_verified, "Line:" MU_TOSTRING(__LINE__));
    cert_verified = false;
    status = verify_certificate(TEST_SERVER_CERT_RSA_FILE_3, TEST_SERVER_PK_RSA_FILE_3, TEST_CA_CERT_RSA_FILE_1, &cert_verified);
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_IS_FALSE(cert_verified, "Line:" MU_TOSTRING(__LINE__));
    ASSERT_ARE_EQUAL(int, 0, cert_verified, "Line:" MU_TOSTRING(__LINE__));

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
    BIO* cert_file = BIO_new_file(cert_file_name, "rb");
    ASSERT_IS_NOT_NULL(cert_file, "Line:" MU_TOSTRING(__LINE__));
    X509* x509_cert = PEM_read_bio_X509(cert_file, NULL, NULL, NULL);
    // make sure the file is closed before asserting below
    BIO_free_all(cert_file);
    ASSERT_IS_NOT_NULL(x509_cert, "Line:" MU_TOSTRING(__LINE__));
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

//https://www.openssl.org/docs/man1.1.0/crypto/OPENSSL_VERSION_NUMBER.html
// this checks if openssl version major minor is greater than or equal to version # 1.1.0
#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
    const STACK_OF(X509_EXTENSION) *ext_list = X509_get0_extensions(input_test_cert);
#else
    X509_CINF *cert_inf = input_test_cert->cert_info;
    STACK_OF(X509_EXTENSION) *ext_list = cert_inf->extensions;
#endif

    ASSERT_IS_TRUE((sk_X509_EXTENSION_num(ext_list) > 0), "Found zero extensions");

    for (int ext_idx=0; ext_idx < sk_X509_EXTENSION_num(ext_list); ext_idx++)
    {
        int sz;
        char output_buffer[MAX_X509_EXT_SIZE];
        ASN1_OBJECT *obj;
        X509_EXTENSION *ext;

        ext = sk_X509_EXTENSION_value(ext_list, ext_idx);
        ASSERT_IS_NOT_NULL(ext, "Line:" MU_TOSTRING(__LINE__));

        obj = X509_EXTENSION_get_object(ext);
        ASSERT_IS_NOT_NULL(obj, "Line:" MU_TOSTRING(__LINE__));

        memset(output_buffer, 0, MAX_X509_EXT_SIZE);
        sz = i2t_ASN1_OBJECT(output_buffer, MAX_X509_EXT_SIZE, obj);
        // if size is larger use the call twice first to get size and then allocate or increase MAX_X509_EXT_SIZE
        ASSERT_IS_FALSE((sz > MAX_X509_EXT_SIZE), "Unexpected buffer size");

        if (strcmp(ext_name, output_buffer) == 0)
        {
            long sz;
            char *memst = NULL;

            printf("\r\nTesting Extension Contents: [%s]\r\n", output_buffer);

            BIO *mem_bio = BIO_new(BIO_s_mem());
            ASSERT_IS_NOT_NULL(mem_bio, "Line:" MU_TOSTRING(__LINE__));
            // print the extension contents into the mem_bio
            X509V3_EXT_print(mem_bio, ext, 0, 0);
            sz = BIO_get_mem_data(mem_bio, &memst);
            ASSERT_IS_TRUE((sz > 0), "Line:" MU_TOSTRING(__LINE__));
            ASSERT_IS_NOT_NULL(memst, "Line:" MU_TOSTRING(__LINE__));
            char *output_str = calloc(sz + 1, 1);
            ASSERT_IS_NOT_NULL(output_str, "Line:" MU_TOSTRING(__LINE__));
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

    ASSERT_ARE_EQUAL(size_t, expected_num_ext_name_entries, nid_match,  "NID match count failed");
    ASSERT_ARE_EQUAL(size_t, num_expted_vals, match_count,  "Match count failed");
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
    ASSERT_ARE_NOT_EQUAL(size_t, CERTIFICATE_TYPE_UNKNOWN, cert_type, "Unknown cert type not supported");

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
        ASSERT_IS_NOT_NULL(expected_key_usage_vals, "Line:" MU_TOSTRING(__LINE__));
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
        ASSERT_IS_NOT_NULL(expected_key_usage_vals, "Line:" MU_TOSTRING(__LINE__));
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DIG_SIG;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_NON_REPUDIATION;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DATA_ENCIPHER;

        idx = 0;
        expected_ext_key_usage_vals_size = 1;
        expected_ext_key_usage_vals = calloc(expected_ext_key_usage_vals_size, sizeof(void*));
        ASSERT_IS_NOT_NULL(expected_ext_key_usage_vals, "Line:" MU_TOSTRING(__LINE__));
        expected_ext_key_usage_vals[idx++] = TEST_X509_KEY_EXT_USAGE_CLIENT_AUTH;
    }
    else
    {
        idx = 0;
        expected_key_usage_vals_size = 5;
        expected_key_usage_vals = calloc(expected_key_usage_vals_size, sizeof(void*));
        ASSERT_IS_NOT_NULL(expected_key_usage_vals, "Line:" MU_TOSTRING(__LINE__));
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DIG_SIG;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_NON_REPUDIATION;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_DATA_ENCIPHER;
        expected_key_usage_vals[idx++] = TEST_X509_KEY_USAGE_KEY_AGREEMENT;

        idx = 0;
        expected_ext_key_usage_vals_size = 1;
        expected_ext_key_usage_vals = calloc(expected_ext_key_usage_vals_size, sizeof(SIZED_BUFFER));
        ASSERT_IS_NOT_NULL(expected_ext_key_usage_vals, "Line:" MU_TOSTRING(__LINE__));
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
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    status = set_san_entries(int_ca_root_handle, int_ca_san_list, sizeof(int_ca_san_list)/sizeof(int_ca_san_list[0]));
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    status = set_san_entries(server_root_handle, server_san_list, sizeof(server_san_list)/sizeof(server_san_list[0]));
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
    status = set_san_entries(client_root_handle, client_san_list, sizeof(client_san_list)/sizeof(client_san_list[0]));
    ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));

    // act
    test_helper_generate_self_signed(ca_root_handle,
                                     TEST_SERIAL_NUM + 1,
                                     2,
                                     TEST_CA_PK_RSA_FILE_1,
                                     TEST_CA_CERT_RSA_FILE_1,
                                     key_props);

    test_helper_generate_pki_certificate(int_ca_root_handle,
                                         1,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2,
                                         TEST_CA_PK_RSA_FILE_1,
                                         TEST_CA_CERT_RSA_FILE_1);

    test_helper_generate_pki_certificate(server_root_handle,
                                         0,
                                         TEST_SERVER_PK_RSA_FILE_3,
                                         TEST_SERVER_CERT_RSA_FILE_3,
                                         TEST_CA_PK_RSA_FILE_2,
                                         TEST_CA_CERT_RSA_FILE_2);

    test_helper_generate_pki_certificate(client_root_handle,
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

        TEST_RSA_PRIVATE_KEY_FILE = prepare_file_path(TEST_TEMP_DIR, TEST_RSA_PRIVATE_KEY_FILE_NAME);
        int status = write_cstring_to_file(TEST_RSA_PRIVATE_KEY_FILE, TEST_RSA_ASYMMETRIC_PRIVATE_KEY);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));

        TEST_RSA_PUBLIC_KEY_FILE = prepare_file_path(TEST_TEMP_DIR, TEST_RSA_PUBLIC_KEY_FILE_NAME);
        status = write_cstring_to_file(TEST_RSA_PUBLIC_KEY_FILE, TEST_RSA_ASYMMETRIC_PUBLIC_KEY);
        ASSERT_ARE_EQUAL(int, 0, status, "Line:" MU_TOSTRING(__LINE__));
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

        free(TEST_RSA_PRIVATE_KEY_FILE); TEST_RSA_PRIVATE_KEY_FILE = NULL;
        free(TEST_RSA_PUBLIC_KEY_FILE); TEST_RSA_PUBLIC_KEY_FILE = NULL;

        test_helper_teardown_temp_dir(&TEST_TEMP_DIR, &TEST_TEMP_DIR_GUID);
        test_helper_teardown_temp_dir(&TEST_IOTEDGE_HOMEDIR, &TEST_IOTEDGE_HOMEDIR_GUID);
        TEST_MUTEX_DESTROY(g_testByTest);
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
        ASSERT_ARE_NOT_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));

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


    // The following test requires some prior setup in order to validate
    // the key sign interface
    //
    // 1) Setup test keys
    //      a) Generate a RSA public-private keypair using openssl
    //         $> openssl genrsa -out private.pem 2048
    //      b) Obtain the public key
    //        $> openssl rsa -in private.pem -outform PEM -pubout -out public.pem
    //      c) Copy the resulting file buffers.
    //         See TEST_RSA_ASYMMETRIC_PRIVATE_KEY, TEST_RSA_ASYMMETRIC_PUBLIC_KEY
    //      d) These buffers need to be exported to files for testing.
    //         See TestClassInitialize.
    //
    // 2) Determine exptected test values based on the generated keys above
    //      a) Prepare the test data to sign
    //          $> echo -n "your test string" > tbs.txt
    //      b) The expected HMAC digest was computed as follows:
    //          b1) Output binary of the HMAC sign.hmac.sha256.bin
    //              $> openssl dgst -sign private.pem -keyform PEM -out sign.hmac.sha256.bin tbs.txt
    //      c) Convert binary to base64 for ease of test
    //              $> base64 sign.hmac.sha256.bin > sign.hmac.sha256.base64
    TEST_FUNCTION(test_rsa_key_sign)
    {
        // arrange
        const char *tbs = "What is sunshine without rain? - Logic";
        size_t tbs_size = strlen(tbs); // we do not use the null term char in the tbs
        KEY_HANDLE key_handle = create_cert_key(TEST_RSA_PRIVATE_KEY_FILE);
        unsigned char * digest = NULL;
        size_t digest_size = 0;
        STRING_HANDLE output_b64;
        const char *expected_base64_sig =
            "P+xw2s65fBegf3e7Y1BiaVsbiJuqDa219Fn55RYyER6fOXqLszcq+LIiF8DRDubsvha4q/2elTNV"
            "rpWt+kLBJ8iwJwn8CHVSmfstPscyC94NAAIw3Td90BEed1LLVrFmQ0W6Zw7xnC7yXqoL1JydZwmZ"
            "gY9JAJxqaDnfcZT7HvYnAcyTGLkO5lpj7Zg1EPywfchUJir1Mq4TAM0ha77iboodQp5Ig2Kmk8ed"
            "LihsYplD0fvoeUMZ+fbGhQOJ367j/ZfGaRusGX23Yqu95BDHC5COhCp3Gm80iymxfhz8gtqqsIhE"
            "bbEp4XB+IJj6ZOxA7rhYZuyCsv23Mh6zRD2Hvg==";

        // act
        int result = key_sign(key_handle, (unsigned char*)tbs, tbs_size, &digest, &digest_size);

        // assert
        ASSERT_IS_NOT_NULL(digest, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, HMAC_SHA256_DIGEST_LEN, digest_size, "Line:" MU_TOSTRING(__LINE__));
        output_b64 = Azure_Base64_Encode_Bytes(digest, digest_size);
        ASSERT_ARE_EQUAL(int, 0, strcmp(expected_base64_sig, STRING_c_str(output_b64)), "Line:" MU_TOSTRING(__LINE__));

        // cleanup
        free(digest);
        key_destroy(key_handle);
        STRING_delete(output_b64);
    }

    TEST_FUNCTION(test_rand_small_buf)
    {
        // arrange
        unsigned char unexpected_buffer[TEST_RAND_SIZE_BYTES_SMALL];
        unsigned char output_buffer[TEST_RAND_SIZE_BYTES_SMALL];
        size_t buffer_sz = sizeof(unexpected_buffer);

        memset(unexpected_buffer, 0xF1, buffer_sz);
        memset(output_buffer, 0xF1, buffer_sz);

        // act
        int result = generate_rand_buffer(output_buffer, buffer_sz);

        // assert
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
        // if this assertion fails it implies that the call to generate_rand_buffer
        // never updated the buffer and yet returned a success OR
        // the statistically improbable event occured that the random bytes returned
        // exactly what the unexpected_buffer of size N was setup with
        // P(test failure) = P(0xF1) * P(0xF1) * ... * P(0xF1) = ((1/256) ^ N) == very small
        ASSERT_ARE_NOT_EQUAL(int, 0, memcmp(unexpected_buffer, output_buffer, buffer_sz), "Line:" MU_TOSTRING(__LINE__));

        //cleanup
    }

    TEST_FUNCTION(test_rand_medium_buf)
    {
        // arrange
        unsigned char unexpected_buffer[TEST_RAND_SIZE_BYTES_MEDIUM];
        unsigned char output_buffer[TEST_RAND_SIZE_BYTES_MEDIUM];
        size_t buffer_sz = sizeof(unexpected_buffer);

        memset(unexpected_buffer, 0xF1, buffer_sz);
        memset(output_buffer, 0xF1, buffer_sz);

        // act
        int result = generate_rand_buffer(output_buffer, buffer_sz);

        // assert
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
        // if this assertion fails it implies that the call to generate_rand_buffer
        // never updated the buffer and yet returned a success OR
        // the statistically improbable event occured that the random bytes returned
        // exactly what the unexpected_buffer of size N was setup with
        // P(test failure) = P(0xF1) * P(0xF1) * ... * P(0xF1) = ((1/256) ^ N) == very small
        ASSERT_ARE_NOT_EQUAL(int, 0, memcmp(unexpected_buffer, output_buffer, buffer_sz), "Line:" MU_TOSTRING(__LINE__));

        //cleanup
    }

    TEST_FUNCTION(test_rand_large_buf)
    {
        // arrange
        unsigned char unexpected_buffer[TEST_RAND_SIZE_BYTES_LARGE];
        unsigned char output_buffer[TEST_RAND_SIZE_BYTES_LARGE];
        size_t buffer_sz = sizeof(unexpected_buffer);

        memset(unexpected_buffer, 0xF1, buffer_sz);
        memset(output_buffer, 0xF1, buffer_sz);

        // act
        int result = generate_rand_buffer(output_buffer, buffer_sz);

        // assert
        ASSERT_ARE_EQUAL(int, 0, result, "Line:" MU_TOSTRING(__LINE__));
        // if this assertion fails it implies that the call to generate_rand_buffer
        // never updated the buffer and yet returned a success OR
        // the statistically improbable event occured that the random bytes returned
        // exactly what the unexpected_buffer of size N was setup with
        // P(test failure) = P(0xF1) * P(0xF1) * ... * P(0xF1) = ((1/256) ^ N) == very small
        ASSERT_ARE_NOT_EQUAL(int, 0, memcmp(unexpected_buffer, output_buffer, buffer_sz), "Line:" MU_TOSTRING(__LINE__));

        //cleanup
    }

    TEST_FUNCTION(test_rand_multiple_calls)
    {
        // arrange
        int result_1, result_2;
        unsigned char output_buffer_1[TEST_RAND_SIZE_BYTES_LARGE];
        unsigned char output_buffer_2[TEST_RAND_SIZE_BYTES_LARGE];
        size_t buffer_sz = sizeof(output_buffer_1);

        memset(output_buffer_1, 0xF1, buffer_sz);
        memset(output_buffer_2, 0xF1, buffer_sz);

        // act
        result_1 = generate_rand_buffer(output_buffer_1, buffer_sz);
        result_2 = generate_rand_buffer(output_buffer_2, buffer_sz);

        // assert
        ASSERT_ARE_EQUAL(int, 0, result_1, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, 0, result_2, "Line:" MU_TOSTRING(__LINE__));
        // if this assertion fails it implies that the call to generate_rand_buffer
        // never updated the buffer and yet returned a success OR
        // the statistically improbable event occured that the random bytes returned
        // exactly what the output_buffer_2 of size N was setup with
        // P(test failure) = P(0xF1) * P(0xF1) * ... * P(0xF1) = ((1/256) ^ N) == very small
        ASSERT_ARE_NOT_EQUAL(int, 0, memcmp(output_buffer_1, output_buffer_2, buffer_sz), "Line:" MU_TOSTRING(__LINE__));

        //cleanup
    }

END_TEST_SUITE(edge_openssl_int_tests)
