// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <inttypes.h>
#include <limits.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stddef.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <sys/types.h>

//#############################################################################
// Memory allocator test hooks
//#############################################################################

static void* test_hook_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void* test_hook_gballoc_calloc(size_t num, size_t size)
{
    return calloc(num, size);
}

static void* test_hook_gballoc_realloc(void* ptr, size_t size)
{
    return realloc(ptr, size);
}

static void test_hook_gballoc_free(void* ptr)
{
    free(ptr);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umock_c_negative_tests.h"
#include "umocktypes_charptr.h"
#include "umocktypes_stdint.h"
#include <openssl/asn1.h>
#include <openssl/bio.h>
#include <openssl/err.h>
#include <openssl/ec.h>
#include <openssl/pem.h>
#include <openssl/x509.h>
#include <openssl/x509v3.h>
#include <openssl/evp.h>
#include "hsm_certificate_props.h"

//#############################################################################
// Declare and enable MOCK definitions
//#############################################################################

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    #include <io.h>
    typedef int MODE_T;
    static int EXPECTED_CREATE_FLAGS = _O_CREAT|_O_WRONLY|_O_TRUNC;
    static int EXPECTED_MODE_FLAGS = _S_IREAD|_S_IWRITE;
#else
    #include <unistd.h>
    typedef mode_t MODE_T;
    static int EXPECTED_CREATE_FLAGS = O_CREAT|O_WRONLY|O_TRUNC;
    static int EXPECTED_MODE_FLAGS = S_IRUSR|S_IWUSR;
#endif

typedef void (*MOCKED_CALLBACK)(int,int,void *);

//#############################################################################
// Forward declarations
//#############################################################################
static char *test_helper_strdup(const char *s);

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "edge_openssl_common.h"
#include "hsm_utils.h"

MOCKABLE_FUNCTION(, EVP_PKEY*, EVP_PKEY_new);
MOCKABLE_FUNCTION(, void, EVP_PKEY_free, EVP_PKEY*, x);
MOCKABLE_FUNCTION(, int, mocked_OPEN, const char*, path, int, flags, MODE_T, mode);
MOCKABLE_FUNCTION(, int, mocked_CLOSE, int, fd);
MOCKABLE_FUNCTION(, BIGNUM*, BN_new);
MOCKABLE_FUNCTION(, void, BN_free, BIGNUM*, a);
MOCKABLE_FUNCTION(, int, BN_set_word, BIGNUM*, a, BN_ULONG, w);
MOCKABLE_FUNCTION(, RSA*, RSA_new);
MOCKABLE_FUNCTION(, void, RSA_free, RSA*, r);
MOCKABLE_FUNCTION(, int, RSA_generate_key_ex, RSA*, rsa, int, bits, BIGNUM*, e_value, BN_GENCB*, cb);
MOCKABLE_FUNCTION(, int, EVP_PKEY_set1_RSA, EVP_PKEY*, pkey, RSA*, key);

MOCKABLE_FUNCTION(, const char*, OBJ_nid2sn, int, n);
MOCKABLE_FUNCTION(, int, OBJ_txt2nid, const char*, s);
MOCKABLE_FUNCTION(, EC_KEY*, EC_KEY_new_by_curve_name, int, nid);
MOCKABLE_FUNCTION(, void, EC_KEY_set_asn1_flag, EC_KEY*, key, int, flag);
MOCKABLE_FUNCTION(, int, EC_KEY_generate_key, EC_KEY*, eckey);
MOCKABLE_FUNCTION(, int, EVP_PKEY_set1_EC_KEY, EVP_PKEY*, pkey, EC_KEY*, key);
MOCKABLE_FUNCTION(, void, EC_KEY_free, EC_KEY*, r);
MOCKABLE_FUNCTION(, EVP_PKEY*, X509_get_pubkey, X509*, x);
MOCKABLE_FUNCTION(, int, EVP_PKEY_base_id, const EVP_PKEY*, pkey);
MOCKABLE_FUNCTION(, RSA*, RSA_generate_key, int, bits, unsigned long, e_value, MOCKED_CALLBACK, cb, void*, cb_arg);
MOCKABLE_FUNCTION(, EC_KEY*, EVP_PKEY_get1_EC_KEY, EVP_PKEY*, pkey);
MOCKABLE_FUNCTION(, const EC_GROUP*, EC_KEY_get0_group, const EC_KEY*, key);
MOCKABLE_FUNCTION(, int, EC_GROUP_get_curve_name, const EC_GROUP*, group);

//https://www.openssl.org/docs/man1.1.0/crypto/OPENSSL_VERSION_NUMBER.html
// this checks if openssl version major minor is greater than or equal to version # 1.1.0
#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
    MOCKABLE_FUNCTION(, int, EVP_PKEY_bits, const EVP_PKEY*, pkey);
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, const X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, const X509*, x, int, nid, int, lastpos);
#else
    MOCKABLE_FUNCTION(, int, EVP_PKEY_bits, EVP_PKEY*, pkey);
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, X509*, x, int, nid, int, lastpos);
#endif

MOCKABLE_FUNCTION(, BIO*, BIO_new_file, const char*, filename, const char*, mode);
MOCKABLE_FUNCTION(, int, PEM_X509_INFO_write_bio, BIO*, bp, X509_INFO*, xi, EVP_CIPHER*, enc, unsigned char*, kstr, int, klen, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, int, BIO_write, BIO*, b, const void*, in, int, inl);
MOCKABLE_FUNCTION(, void, BIO_free_all, BIO*, bio);
MOCKABLE_FUNCTION(, EVP_PKEY*, PEM_read_bio_PrivateKey, BIO*, bp, EVP_PKEY**, x, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, int, X509_set_version, X509*, x, long, version);
MOCKABLE_FUNCTION(, int, ASN1_INTEGER_set, ASN1_INTEGER*, a, long, v);
MOCKABLE_FUNCTION(, int, X509_set_pubkey, X509*, x, EVP_PKEY*, pkey);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notBefore, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notAfter, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, X509_gmtime_adj, ASN1_TIME*, s, long, adj);
MOCKABLE_FUNCTION(, time_t, get_utc_time_from_asn_string, const unsigned char*, time_value, size_t, length);
MOCKABLE_FUNCTION(, BIO*, BIO_new_fd, int, fd, int, close_flag);
MOCKABLE_FUNCTION(, int, PEM_write_bio_PrivateKey, BIO*, bp, EVP_PKEY*, x, const EVP_CIPHER*, enc, unsigned char*, kstr, int, klen, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, ASN1_INTEGER*, X509_get_serialNumber, X509*, a);
MOCKABLE_FUNCTION(, BASIC_CONSTRAINTS*, BASIC_CONSTRAINTS_new);
MOCKABLE_FUNCTION(, void, BASIC_CONSTRAINTS_free, BASIC_CONSTRAINTS*, bc);
MOCKABLE_FUNCTION(, ASN1_INTEGER*, ASN1_INTEGER_new);
MOCKABLE_FUNCTION(, int, X509_add1_ext_i2d, X509*, x, int, nid, void*, value, int, crit, unsigned long, flags);
MOCKABLE_FUNCTION(, int, X509_NAME_get_text_by_NID, X509_NAME*, name, int, nid, char*, buf, int, len);
MOCKABLE_FUNCTION(, int, X509_NAME_add_entry_by_txt, X509_NAME*, name, const char*, field, int, type, const unsigned char*, bytes, int, len, int, loc, int, set);
MOCKABLE_FUNCTION(, int, X509_set_issuer_name, X509*, x, X509_NAME*, name);
MOCKABLE_FUNCTION(, X509*, X509_new);
MOCKABLE_FUNCTION(, void, X509_free, X509*, a);
MOCKABLE_FUNCTION(, X509_STORE*, X509_STORE_new);
MOCKABLE_FUNCTION(, void, X509_STORE_free, X509_STORE*, a);
MOCKABLE_FUNCTION(, const EVP_MD*, EVP_sha256);
MOCKABLE_FUNCTION(, int, X509_sign, X509*, x, EVP_PKEY*, pkey, const EVP_MD*, md);
MOCKABLE_FUNCTION(, int, X509_verify, X509*, a, EVP_PKEY*, r);
MOCKABLE_FUNCTION(, int, X509_verify_cert, X509_STORE_CTX*, ctx);
MOCKABLE_FUNCTION(, X509_STORE_CTX*, X509_STORE_CTX_new);
MOCKABLE_FUNCTION(, void, X509_STORE_CTX_free, X509_STORE_CTX*, ctx);
MOCKABLE_FUNCTION(, int, X509_STORE_set_flags, X509_STORE*, ctx, unsigned long, flags);
MOCKABLE_FUNCTION(, int, X509_STORE_CTX_get_error, X509_STORE_CTX*, ctx);
MOCKABLE_FUNCTION(, const char*, X509_verify_cert_error_string, long, n);
MOCKABLE_FUNCTION(, X509_LOOKUP_METHOD*, X509_LOOKUP_file);
MOCKABLE_FUNCTION(, X509_LOOKUP*, X509_STORE_add_lookup, X509_STORE*, v, X509_LOOKUP_METHOD*, m);
MOCKABLE_FUNCTION(, int, X509_LOOKUP_ctrl, X509_LOOKUP*, ctx, int, cmd, const char*, argc, long, argl, char**, ret);
MOCKABLE_FUNCTION(, X509_LOOKUP_METHOD*, X509_LOOKUP_hash_dir);
MOCKABLE_FUNCTION(, X509*, PEM_read_bio_X509, BIO*, bp, X509**, x, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, int, PEM_write_bio_X509, BIO*, bp, X509*, x);
MOCKABLE_FUNCTION(, int, X509_STORE_CTX_init, X509_STORE_CTX*, ctx, X509_STORE*, store, X509*, x509, struct stack_st_X509*, chain);
MOCKABLE_FUNCTION(, uint64_t, get_validity_seconds, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_common_name, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_country_name, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_state_name, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_locality, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_organization_name, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char*, get_organization_unit, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, CERTIFICATE_TYPE, get_certificate_type, CERT_PROPS_HANDLE, handle);
MOCKABLE_FUNCTION(, const char * const*, get_san_entries, CERT_PROPS_HANDLE, handle, size_t*, num_entries);

MOCKABLE_FUNCTION(, X509_EXTENSION*, mocked_X509V3_EXT_conf_nid, struct lhash_st_CONF_VALUE*, conf, X509V3_CTX*, ctx, int, ext_nid, char*, value);
MOCKABLE_FUNCTION(, int, X509_add_ext, X509*, x, X509_EXTENSION*, ex, int, loc);
MOCKABLE_FUNCTION(, void, X509_EXTENSION_free, X509_EXTENSION*, ex);

MOCKABLE_FUNCTION(, void, X509V3_set_ctx, X509V3_CTX*, ctx, X509*, issuer, X509*, subj, X509_REQ*, req, X509_CRL*, crl, int, flags);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "hsm_key.h"

//#############################################################################
// Test defines and data
//#############################################################################

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)
static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

#define MAX_FAILED_FUNCTION_LIST_SIZE 128

#define TEST_SERIAL_NUMBER 1
#define TEST_PATH_LEN_CA 1
#define TEST_PATH_LEN_NON_CA 0
#define TEST_KEY_FILE "key.pem"
#define TEST_CERT_FILE "cert.pem"
#define TEST_BAD_CHAIN_CERT_FILE "bad_chain_cert.pem"
#define TEST_ISSUER_KEY_FILE "issuer_key.pem"
#define TEST_ISSUER_CERT_FILE "issuer_cert.pem"
#define TEST_ISSUER_CERT_DATA "test_issuer_certificate_data"
#define TEST_ISSUER_KEY_DATA "test_key_data"
#define TEST_VALID_CHAIN_CERT_DATA "test_certificate_data" TEST_ISSUER_CERT_DATA
#define TEST_INVALID_CHAIN_CERT_DATA "test_certificate_data"
#define TEST_EC_NUM_BITS 256
#define TEST_CURVE_NAME "TEST_CURVE"
#define TEST_CURVE_NAME_ID (int)0x100
#define TEST_ERROR_CODE 0x10
#define TEST_ERROR_STRING "TEST_ERROR_MESSAGE"
#define TEST_VALID_RSA_CA_CERT_KEY_LEN 4096
#define TEST_VALID_RSA_SERVER_KEY_LEN 2048
#define TEST_VALID_RSA_CLIENT_KEY_LEN 2048
#define TEST_VALID_ECC_CA_CERT_KEY_LEN 256
#define TEST_VALID_ECC_SERVER_KEY_LEN 256
#define TEST_VALID_ECC_CLIENT_KEY_LEN 256
#define MAX_SUBJECT_VALUE_SIZE 129

#define TEST_PROPS_VALIDITY_SECONDS (uint64_t)1000
#define TEST_PROPS_COMMON_NAME "test_common_name"
#define TEST_PROPS_COUNTRY_NAME_DEFLT "test_country_name_default"
#define TEST_PROPS_COUNTRY_NAME_ISSUER "test_country_name_issuer"
#define TEST_PROPS_STATE_NAME_DEFLT "test_state_name_default"
#define TEST_PROPS_STATE_NAME_ISSUER "test_state_name_issuer"
#define TEST_PROPS_LOCALITY_NAME_DEFLT "test_locality_name_default"
#define TEST_PROPS_LOCALITY_NAME_ISSUER "test_locality_name_issuer"
#define TEST_PROPS_ORG_NAME_DEFLT "test_organization_name_default"
#define TEST_PROPS_ORG_NAME_ISSUER "test_organization_name_issuer"
#define TEST_PROPS_ORG_UNIT_NAME_DEFLT "test_organization_unit_default"
#define TEST_PROPS_ORG_UNIT_NAME_ISSUER "test_organization_unit_issuer"
#define TEST_PROPS_CERT_TYPE CERTIFICATE_TYPE_CA

#define TEST_EVP_KEY (EVP_PKEY*)0x2000
#define TEST_BIGNUM (BIGNUM*)0x2001
#define TEST_RSA (RSA*)0x2002
#define TEST_ECC_GROUP (int)0x2003
#define TEST_EC_PUB_KEY (EC_KEY*)0x2004
#define TEST_EC_KEY (EC_KEY*)0x2005
#define TEST_RSA_KEY (RSA*)0x2006
#define TEST_PUB_KEY (EVP_PKEY*)0x2007
#define TEST_PUB_GROUP (EC_GROUP*)0x2008
#define TEST_BIO (BIO*)0x2009
#define TEST_BIO_WRITE_KEY (BIO*)0x2010
#define TEST_BIO_WRITE_CERT (BIO*)0x2011
#define TEST_ISSUER_EVP_KEY (EVP_PKEY*)0x2012
#define TEST_FD_BIO (BIO*)0x2013
#define TEST_ASN1_SERIAL_NUM (ASN1_INTEGER*)0x2014
#define TEST_ASN1_INTEGER (ASN1_INTEGER*)0x2015
#define TEST_X509_SUBJECT_NAME (X509_NAME*)0x2016
#define TEST_X509_SUBJECT_ISSUER_NAME (X509_NAME*)0x2017
#define TEST_X509 (X509*)0x2018
#define TEST_ISSUER_X509 (X509*)0x2019
#define TEST_ISSUER_PUB_KEY (EVP_PKEY*)0x2020
#define TEST_X509_STORE (X509_STORE*)0x2021
#define TEST_EVP_SHA256_MD (EVP_MD*)0x2022
#define TEST_STORE_CTXT (X509_STORE_CTX*)0x2023
#define TEST_X509_LOOKUP_METHOD_FILE (X509_LOOKUP_METHOD*)0x2024
#define TEST_X509_LOOKUP_METHOD_HASH (X509_LOOKUP_METHOD*)0x2025
#define TEST_X509_LOOKUP_LOAD_FILE (X509_LOOKUP*)0x2026
#define TEST_X509_LOOKUP_LOAD_HASH (X509_LOOKUP*)0x2027
#define TEST_X509_LOOKUP (X509_LOOKUP*)0x2028
#define TEST_CERT_PROPS_HANDLE (CERT_PROPS_HANDLE)0x2029
#define TEST_WRITE_PRIVATE_KEY_FD (int)0x2030
#define TEST_WRITE_CERTIFICATE_FD (int)0x2031
#define TEST_NID_EXTENSION (X509_EXTENSION*)0x2032
#define TEST_UTC_TIME_FROM_ASN1 1000
#define VALID_ASN1_TIME_STRING_UTC_FORMAT 0x17
#define VALID_ASN1_TIME_STRING_UTC_LEN    13
#define INVALID_ASN1_TIME_STRING_UTC_FORMAT 0
#define INVALID_ASN1_TIME_STRING_UTC_LEN    0

typedef struct VERIFY_CERT_TEST_PARAMS_TAG
{
    const char *cert_file;
    const char *key_file;
    const char *issuer_cert_file;
    bool force_set_verify_return_value;
    ASN1_TIME *force_set_asn1_time;
    bool skid_set;
} VERIFY_CERT_TEST_PARAMS;

struct SUBJECT_FIELDS_TAG
{
    const char *country_name;
    const char *state_name;
    const char *locality_name;
    const char *organization_name;
    const char *organization_unit_name;
};
typedef struct SUBJECT_FIELDS_TAG SUBJECT_FIELDS;

static PKI_KEY_PROPS TEST_VALID_KEY_PROPS_RSA = {
    .key_type = HSM_PKI_KEY_RSA,
    .ec_curve_name = NULL
};

static PKI_KEY_PROPS TEST_VALID_KEY_PROPS_ECC = {
    .key_type = HSM_PKI_KEY_EC,
    .ec_curve_name = TEST_CURVE_NAME
};

//#define TEST_BEFORE_ASN1_STRING (unsigned char*)"BEF012345678"
static ASN1_TIME TEST_ASN1_TIME_BEFORE = {
    .length = VALID_ASN1_TIME_STRING_UTC_LEN,
    .type = VALID_ASN1_TIME_STRING_UTC_FORMAT,
    .data = (unsigned char*)"BEF012345678",
    .flags = 0
};

//#define TEST_AFTER_ASN1_STRING (unsigned char*)"AFT012345678"
static ASN1_TIME TEST_ASN1_TIME_AFTER = {
    .length = VALID_ASN1_TIME_STRING_UTC_LEN,
    .type = VALID_ASN1_TIME_STRING_UTC_FORMAT,
    .data = (unsigned char*)"AFT012345678",
    .flags = 0
};

unsigned char ASN1_DATA_EXPIRED[] = "EXP012345678";
static ASN1_TIME TEST_ASN1_TIME_AFTER_EXPIRED = {
    .length = VALID_ASN1_TIME_STRING_UTC_LEN,
    .type = VALID_ASN1_TIME_STRING_UTC_FORMAT,
    .data = ASN1_DATA_EXPIRED,
    .flags = 0
};

static ASN1_TIME TEST_UTC_NOW_TIME_FROM_ASN1 = {
    VALID_ASN1_TIME_STRING_UTC_LEN,
    VALID_ASN1_TIME_STRING_UTC_FORMAT,
    NULL,
    0
};

static BASIC_CONSTRAINTS TEST_CA_BASIC_CONSTRAINTS = {
    .ca = 1,
    .pathlen = NULL
};

static BASIC_CONSTRAINTS TEST_NON_CA_BASIC_CONSTRAINTS = {
    .ca = 0,
    .pathlen = NULL
};

const char *TEST_SAN_ENTRIES[] = { "DNS: TESTDNS", "URI: scheme://simple/scheme/v/1" };
size_t TEST_NUM_SAN_ENTRIES = sizeof(TEST_SAN_ENTRIES)/sizeof(TEST_SAN_ENTRIES[0]);

//#############################################################################
// Mocked functions test hooks
//#############################################################################

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s",
                   ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

char* test_hook_read_file_into_cstring(const char* file_name, size_t *output_buffer_size)
{
    char *result;
    size_t size;

    if (strcmp(file_name, TEST_CERT_FILE) == 0)
    {
        result = test_helper_strdup(TEST_VALID_CHAIN_CERT_DATA);
        ASSERT_IS_NOT_NULL(result);
        size = strlen(TEST_VALID_CHAIN_CERT_DATA) + 1;
    }
    else if (strcmp(file_name, TEST_BAD_CHAIN_CERT_FILE) == 0)
    {
        result = test_helper_strdup(TEST_INVALID_CHAIN_CERT_DATA);
        ASSERT_IS_NOT_NULL(result);
        size = strlen(TEST_INVALID_CHAIN_CERT_DATA) + 1;
    }
    else if (strcmp(file_name, TEST_ISSUER_CERT_FILE) == 0)
    {
        result = test_helper_strdup(TEST_ISSUER_CERT_DATA);
        ASSERT_IS_NOT_NULL(result);
        size = strlen(TEST_ISSUER_CERT_DATA) + 1;
    }
    else
    {
        result = NULL;
        size = 0;
    }

    if (output_buffer_size) *output_buffer_size = size;
    return result;
}

static int test_hook_mocked_OPEN(const char *pathname, int flags, MODE_T mode)
{
    (void)pathname;
    (void)flags;
    (void)mode;

    return TEST_WRITE_PRIVATE_KEY_FD;
}

static int test_hook_mocked_CLOSE(int fd)
{
    (void)fd;

    return 0;
}

static EVP_PKEY* test_hook_EVP_PKEY_new(void)
{
    return TEST_EVP_KEY;
}

static BIGNUM* test_hook_BN_new(void)
{
    return TEST_BIGNUM;
}

static void test_hook_EVP_PKEY_free(EVP_PKEY *x)
{
    (void)x;
}

static int test_hook_BN_set_word(BIGNUM *a, BN_ULONG w)
{
    (void)a;
    (void)w;

    return 1;
}

static void test_hook_BN_free(BIGNUM *a)
{
    (void)a;
}

static RSA* test_hook_RSA_new(void)
{
    return TEST_RSA;
}

static void test_hook_RSA_free(RSA *r)
{
    (void)r;
}

static int test_hook_RSA_generate_key_ex(RSA *rsa, int bits, BIGNUM *e_value, BN_GENCB *cb)
{
    (void)rsa;
    (void)bits;
    (void)e_value;
    (void)cb;

    return 1;
}

static int test_hook_EVP_PKEY_set1_RSA(EVP_PKEY *pkey, RSA *key)
{
    (void)pkey;
    (void)key;

    return 1;
}

static int test_hook_OBJ_txt2nid(const char *s)
{
    (void)s;

    return TEST_ECC_GROUP;
}

static EC_KEY* test_hook_EC_KEY_new_by_curve_name(int nid)
{
    (void)nid;

    return TEST_EC_KEY;
}

static void test_hook_EC_KEY_set_asn1_flag(EC_KEY *key, int flag)
{
    (void)key;
    (void)flag;
}

static int test_hook_EC_KEY_generate_key(EC_KEY *eckey)
{
    (void)eckey;

    return 1;
}

static int test_hook_EVP_PKEY_set1_EC_KEY(EVP_PKEY *pkey, EC_KEY *key)
{
    (void)pkey;
    (void)key;

    return 1;
}

static void test_hook_EC_KEY_free(EC_KEY *r)
{
    (void)r;
}

static EVP_PKEY* test_hook_X509_get_pubkey(X509 *x)
{
    (void)x;

    return TEST_PUB_KEY;
}

static int test_hook_EVP_PKEY_base_id(const EVP_PKEY *pkey)
{
    (void)pkey;

    return EVP_PKEY_RSA;
}

static RSA* test_hook_RSA_generate_key(int bits, unsigned long e_value, MOCKED_CALLBACK cb, void *cb_arg)
{
    (void)bits;
    (void)e_value;
    (void)cb;
    (void)cb_arg;

    return TEST_RSA_KEY;
}

static EC_KEY* test_hook_EVP_PKEY_get1_EC_KEY(EVP_PKEY *pkey)
{
    (void)pkey;

    return TEST_EC_PUB_KEY;
}

static const EC_GROUP* test_hook_EC_KEY_get0_group(const EC_KEY *key)
{
    (void)key;

    return TEST_PUB_GROUP;
}

static int test_hook_EC_GROUP_get_curve_name(const EC_GROUP* group)
{
    (void)group;

    return TEST_CURVE_NAME_ID;
}

static const char* test_hook_OBJ_nid2sn(int n)
{
    (void)n;

    return TEST_CURVE_NAME;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static int test_hook_EVP_PKEY_bits(const EVP_PKEY *pkey)
#else
static int test_hook_EVP_PKEY_bits(EVP_PKEY *pkey)
#endif
{
    (void)pkey;

    return TEST_EC_NUM_BITS;
}

static BIO* test_hook_BIO_new_file(const char *filename, const char *mode)
{
    (void)filename;
    (void)mode;

    return TEST_BIO;
}

static int test_hook_PEM_X509_INFO_write_bio
(
    BIO *bp,
    X509_INFO *xi,
    EVP_CIPHER *enc,
    unsigned char *kstr,
    int klen,
    pem_password_cb *cb,
    void *u
)
{
    (void)bp;
    (void)xi;
    (void)enc;
    (void)kstr;
    (void)klen;
    (void)cb;
    (void)u;

    return 1;
}

static int test_hook_BIO_write(BIO *b, const void *in, int inl)
{
    (void)b;
    (void)in;

    return inl;
}

static void test_hook_BIO_free_all(BIO *bio)
{
    (void)bio;
}

static EVP_PKEY* test_hook_PEM_read_bio_PrivateKey
(
    BIO *bp,
    EVP_PKEY **x,
    pem_password_cb *cb,
    void *u
)
{
    (void)bp;
    (void)x;
    (void)cb;
    (void)u;

    return TEST_ISSUER_EVP_KEY;
}

static int test_hook_X509_set_version(X509 *x, long version)
{
    (void)x;
    (void)version;

    return 1;
}

static int test_hook_ASN1_INTEGER_set(ASN1_INTEGER *a, long v)
{
    (void)a;
    (void)v;

    return 1;
}

static int test_hook_X509_set_pubkey(X509 *x, EVP_PKEY *pkey)
{
    (void)x;
    (void)pkey;

    return 1;
}

static ASN1_TIME* test_hook_X509_get_notBefore(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_BEFORE;
}

static ASN1_TIME* test_hook_X509_get_notAfter(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_AFTER;
}

static time_t test_hook_get_utc_time_from_asn_string
(
    const unsigned char *time_value,
    size_t length
)
{
    (void)time_value;
    (void)length;

    time_t now = time(NULL);
    int offset = TEST_UTC_TIME_FROM_ASN1;

    if (memcmp(time_value, ASN1_DATA_EXPIRED, sizeof(ASN1_DATA_EXPIRED)) == 0)
    {
        // this ensures that certificate will always be evaluated as expired
        offset = -5;
    }
    return now + offset;
}

static ASN1_TIME* test_hook_X509_gmtime_adj(ASN1_TIME *s, long adj)
{
    (void)s;
    (void)adj;

    return &TEST_UTC_NOW_TIME_FROM_ASN1;
}

static void* test_hook_read_file_into_buffer
(
    const char *file_name,
    size_t *output_buffer_size
)
{
    (void)file_name;
    size_t test_data_len = strlen(TEST_ISSUER_CERT_DATA);
    size_t test_data_size = test_data_len + 1;
    void *data = test_hook_gballoc_malloc(test_data_size);
    ASSERT_IS_NOT_NULL_WITH_MSG(data, "Line:" TOSTRING(__LINE__));
    memset(data, 0, test_data_size);
    memcpy(data, TEST_ISSUER_CERT_DATA, test_data_len);
    if (output_buffer_size) *output_buffer_size = test_data_size;
    return data;
}

static BIO* test_hook_BIO_new_fd(int fd,int close_flag)
{
    (void)fd;
    (void)close_flag;

    return TEST_FD_BIO;
}

static int test_hook_PEM_write_bio_PrivateKey
(
    BIO *bp,
    EVP_PKEY *x,
    const EVP_CIPHER *enc,
    unsigned char *kstr,
    int klen,
    pem_password_cb *cb,
    void *u
)
{
    (void)bp;
    (void)x;
    (void)enc;
    (void)kstr;
    (void)klen;
    (void)cb;
    (void)u;

    return 1;
}

static ASN1_INTEGER* test_hook_X509_get_serialNumber(X509 *a)
{
    (void)a;

    return TEST_ASN1_SERIAL_NUM;
}

static int test_hook_X509_add1_ext_i2d(X509 *x, int nid, void *value, int crit, unsigned long flags)
{
    (void)x;
    (void)nid;
    (void)value;
    (void)crit;
    (void)flags;

    return 1;
}

static int test_hook_X509_NAME_get_text_by_NID(X509_NAME *name, int nid, char *buf, int len)
{
    (void)name;
    (void)buf;
    (void)len;
    int result = 0;
    const char *value;

    if ((len == 0) || (buf == NULL))
    {
        value = NULL;
        result = 0;
    }
    else
    {
        switch (nid)
        {
            case NID_countryName:
            value = TEST_PROPS_COUNTRY_NAME_ISSUER;
            result = 1;
            break;

            case NID_stateOrProvinceName:
            value = TEST_PROPS_STATE_NAME_ISSUER;
            result = 1;
            break;

            case NID_localityName:
            value = TEST_PROPS_LOCALITY_NAME_ISSUER;
            result = 1;
            break;

            case NID_organizationName:
            value = TEST_PROPS_ORG_NAME_ISSUER;
            result = 1;
            break;

            case NID_organizationalUnitName:
            value = TEST_PROPS_ORG_UNIT_NAME_ISSUER;
            result = 1;
            break;

            default:
                value = NULL;
                result = 0;
        };
    }

    memset(buf, 0, len);
    if ((result == 1) && (value != NULL))
    {
        strncpy(buf, value, len - 1);
    }
    return result;
}

static int test_hook_X509_NAME_add_entry_by_txt
(
    X509_NAME *name,
    const char *field,
    int type,
    const unsigned char *bytes,
    int len,
    int loc,
    int set
)
{
    (void)name;
    (void)field;
    (void)type;
    (void)bytes;
    (void)len;
    (void)loc;
    (void)set;
    return 1;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static X509_NAME* test_hook_X509_get_subject_name(const X509 *a)
#else
static X509_NAME* test_hook_X509_get_subject_name(X509 *a)
#endif
{
    (void)a;
    return TEST_X509_SUBJECT_NAME;
}

static int test_hook_X509_set_issuer_name(X509 *x, X509_NAME *name)
{
    (void)x;
    (void)name;
    return 1;
}

static BASIC_CONSTRAINTS* test_hook_BASIC_CONSTRAINTS_new(void)
{
    return &TEST_CA_BASIC_CONSTRAINTS;
}

static void test_hook_BASIC_CONSTRAINTS_free(BASIC_CONSTRAINTS *bc)
{
    (void)bc;
}

static ASN1_INTEGER* test_hook_ASN1_INTEGER_new(void)
{
    return TEST_ASN1_INTEGER;
}

static X509* test_hook_X509_new(void)
{
    return TEST_X509;
}

static void test_hook_X509_free(X509 *a)
{
    (void)a;
}

static X509_STORE* test_hook_X509_STORE_new(void)
{
    return TEST_X509_STORE;
}

static void test_hook_X509_STORE_free(X509_STORE *a)
{
    (void)a;
}

static const EVP_MD* test_hook_EVP_sha256(void)
{
    return TEST_EVP_SHA256_MD;
}

static int test_hook_X509_sign(X509 *x, EVP_PKEY *pkey, const EVP_MD *md)
{
    (void)x;
    (void)pkey;
    (void)md;
    return 1;
}

static int test_hook_X509_verify(X509 *a, EVP_PKEY *r)
{
    (void)a;
    (void)r;
    return 1;
}

int test_hook_X509_verify_cert(X509_STORE_CTX *ctx)
{
    (void)ctx;
    return 1;
}

X509_STORE_CTX* test_hook_X509_STORE_CTX_new(void)
{
    return TEST_STORE_CTXT;
}

void test_hook_X509_STORE_CTX_free(X509_STORE_CTX *ctx)
{
    (void)ctx;
}

static const char* test_hook_X509_verify_cert_error_string(long n)
{
    (void)n;

    return TEST_ERROR_STRING;
}

static int test_hook_X509_STORE_set_flags(X509_STORE *ctx, unsigned long flags)
{
    (void)ctx;
    (void)flags;

    return 1;
}

static int test_hook_X509_STORE_CTX_get_error(X509_STORE_CTX *ctx)
{
    (void)ctx;

    return TEST_ERROR_CODE;
}

static X509_LOOKUP_METHOD* test_hook_X509_LOOKUP_file(void)
{
    return TEST_X509_LOOKUP_METHOD_FILE;
}

static X509_LOOKUP* test_hook_X509_STORE_add_lookup(X509_STORE *v, X509_LOOKUP_METHOD *m)
{
    (void)v;
    (void)m;

    return TEST_X509_LOOKUP;
}

static int test_hook_X509_LOOKUP_ctrl
(
    X509_LOOKUP *ctx,
    int cmd,
    const char *argc,
    long argl,
    char **ret
)
{
    (void)ctx;
    (void)cmd;
    (void)argc;
    (void)argl;
    (void)ret;

    return 1;
}

static X509_LOOKUP_METHOD* test_hook_X509_LOOKUP_hash_dir(void)
{
    return TEST_X509_LOOKUP_METHOD_HASH;
}

static X509* test_hook_PEM_read_bio_X509(BIO *bp, X509 **x, pem_password_cb *cb, void *u)
{
    (void)bp;
    (void)x;
    (void)cb;
    (void)u;

    return TEST_X509;
}

static int test_hook_PEM_write_bio_X509(BIO *bp, X509 *x)
{
    (void)bp;
    (void)x;

    return 1;
}

static int test_hook_X509_STORE_CTX_init
(
    X509_STORE_CTX *ctx,
    X509_STORE *store,
    X509 *x509,
    struct stack_st_X509* chain
)
{
    (void)ctx;
    (void)store;
    (void)x509;
    (void)chain;

    return 1;
}

static uint64_t test_hook_get_validity_seconds(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_VALIDITY_SECONDS;
}

static const char* test_hook_get_common_name(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_COMMON_NAME;
}

static const char* test_hook_get_country_name(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_COUNTRY_NAME_DEFLT;
}

static const char* test_hook_get_state_name(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_STATE_NAME_DEFLT;
}

static const char* test_hook_get_locality(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_LOCALITY_NAME_DEFLT;
}

static const char* test_hook_get_organization_name(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_ORG_NAME_DEFLT;
}

static const char* test_hook_get_organization_unit(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_ORG_UNIT_NAME_DEFLT;
}

static CERTIFICATE_TYPE test_hook_get_certificate_type(CERT_PROPS_HANDLE handle)
{
    (void)handle;

    return TEST_PROPS_CERT_TYPE;
}

static X509_EXTENSION* test_hook_mocked_X509V3_EXT_conf_nid
(
    struct lhash_st_CONF_VALUE *conf,
    X509V3_CTX *ctx,
    int ext_nid,
    char *value
)
{
    (void)conf;
    (void)ctx;
    (void)ext_nid;
    (void)value;

    return TEST_NID_EXTENSION;
}

static int test_hook_X509_add_ext(X509* x, X509_EXTENSION* ex, int loc)
{
    (void)x;
    (void)ex;
    (void)loc;

    return 1;
}

static void test_hook_X509_EXTENSION_free(X509_EXTENSION* ex)
{
    (void)ex;
}

static const char * const* test_hook_get_san_entries(CERT_PROPS_HANDLE handle, size_t *num_entries)
{
    (void)handle;
    *num_entries = TEST_NUM_SAN_ENTRIES;
    return TEST_SAN_ENTRIES;
}

static void test_hook_X509V3_set_ctx
(
    X509V3_CTX *ctx,
    X509 *issuer,
    X509 *subj,
    X509_REQ *req,
    X509_CRL *crl,
    int flags
)
{
    (void)ctx;
    (void)issuer;
    (void)subj;
    (void)req;
    (void)crl;
    (void)flags;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static int test_hook_X509_get_ext_by_NID(const X509 *x, int nid, int lastpos)
#else
static int test_hook_X509_get_ext_by_NID(X509 *x, int nid, int lastpos)
#endif
{
    (void)x;
    (void)nid;
    (void)lastpos;

    return 1;
}

//#############################################################################
// Test helpers
//#############################################################################
static bool test_helper_is_windows(void)
{
#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    return true;
#else
    return false;
#endif
}

static char *test_helper_strdup(const char *s)
{
    size_t len = strlen(s);
    size_t size = len + 1;
    char *result = test_hook_gballoc_malloc(size);
    ASSERT_IS_NOT_NULL_WITH_MSG(result, "Line:" TOSTRING(__LINE__));
    memset(result, 0, size);
    strcpy(result, s);
    return result;
}

void test_helper_generate_rsa_key(int key_len, size_t *index, char *failed_function_list, size_t failed_function_size)
{
    size_t i = *index;

    EXPECTED_CALL(EVP_PKEY_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(BN_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BN_set_word(TEST_BIGNUM, RSA_F4));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(RSA_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(RSA_generate_key_ex(TEST_RSA, key_len, TEST_BIGNUM, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(EVP_PKEY_set1_RSA(TEST_EVP_KEY, TEST_RSA));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(RSA_free(TEST_RSA));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(BN_free(TEST_BIGNUM));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    *index = i;
}

void test_helper_generate_ecc_key(bool is_self_signed, size_t *index, char *failed_function_list, size_t failed_function_size)
{
    size_t i = *index;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(EVP_PKEY_get1_EC_KEY(TEST_ISSUER_PUB_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(EC_KEY_get0_group(TEST_EC_PUB_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(EC_GROUP_get_curve_name(TEST_PUB_GROUP));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(OBJ_nid2sn(TEST_CURVE_NAME_ID));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(EVP_PKEY_bits(TEST_ISSUER_PUB_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    // generate_ecc_key
    STRICT_EXPECTED_CALL(OBJ_txt2nid(TEST_CURVE_NAME));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(EC_KEY_new_by_curve_name(TEST_ECC_GROUP));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(EC_KEY_set_asn1_flag(TEST_EC_KEY, OPENSSL_EC_NAMED_CURVE));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(EC_KEY_generate_key(TEST_EC_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(EVP_PKEY_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(EVP_PKEY_set1_EC_KEY(TEST_EVP_KEY, TEST_EC_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(EC_KEY_free(TEST_EC_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(EC_KEY_free(TEST_EC_PUB_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    *index = i;
}

static void test_helper_cert_create_with_subject
(
    bool is_self_signed,
    bool use_rsa,
    int key_len,
    CERTIFICATE_TYPE cert_type,
    SUBJECT_FIELDS *set_return_subject,
    char *failed_function_list,
    size_t failed_function_size
)
{
    uint64_t failed_function_bitmask = 0;
    size_t i = 0;
    int key_type;

    if (use_rsa)
    {
        key_type = EVP_PKEY_RSA;
    }
    else
    {
        key_type = EVP_PKEY_EC;
    }

    umock_c_reset_all_calls();

    EXPECTED_CALL(initialize_openssl());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(get_validity_seconds(TEST_CERT_PROPS_HANDLE));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(get_common_name(TEST_CERT_PROPS_HANDLE));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(get_certificate_type(TEST_CERT_PROPS_HANDLE)).SetReturn(cert_type);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(BIO_new_file(TEST_ISSUER_CERT_FILE, "r"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(PEM_read_bio_X509(TEST_BIO, NULL, NULL, NULL)).SetReturn(TEST_ISSUER_X509);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(BIO_new_file(TEST_ISSUER_KEY_FILE, "r"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(PEM_read_bio_PrivateKey(TEST_BIO, NULL, NULL, NULL)).SetReturn(TEST_ISSUER_EVP_KEY);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_get_pubkey(TEST_ISSUER_X509)).SetReturn(TEST_ISSUER_PUB_KEY);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(EVP_PKEY_base_id(TEST_ISSUER_PUB_KEY)).SetReturn(key_type);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (use_rsa)
    {
        test_helper_generate_rsa_key(key_len, &i, failed_function_list, failed_function_size);
    }
    else
    {
        test_helper_generate_ecc_key(is_self_signed, &i, failed_function_list, failed_function_size);
    }

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(EVP_PKEY_free(TEST_ISSUER_PUB_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    if (test_helper_is_windows())
    {
        STRICT_EXPECTED_CALL(BIO_new_file(TEST_KEY_FILE, "w")).SetReturn(TEST_BIO_WRITE_KEY);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }
    else
    {
        STRICT_EXPECTED_CALL(mocked_OPEN(TEST_KEY_FILE, EXPECTED_CREATE_FLAGS, EXPECTED_MODE_FLAGS)).SetReturn(TEST_WRITE_PRIVATE_KEY_FD);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BIO_new_fd(TEST_WRITE_PRIVATE_KEY_FD, 0)).SetReturn(TEST_BIO_WRITE_KEY);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    STRICT_EXPECTED_CALL(PEM_write_bio_PrivateKey(TEST_BIO_WRITE_KEY, TEST_EVP_KEY, NULL, NULL, 0, NULL, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO_WRITE_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if (!test_helper_is_windows())
    {
        STRICT_EXPECTED_CALL(mocked_CLOSE(TEST_WRITE_PRIVATE_KEY_FD));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    STRICT_EXPECTED_CALL(X509_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_set_version(TEST_X509, 0x2));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_get_serialNumber(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(ASN1_INTEGER_set(TEST_ASN1_SERIAL_NUM, TEST_SERIAL_NUMBER));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_set_pubkey(TEST_X509, TEST_EVP_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(mocked_X509_get_notBefore(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_gmtime_adj(&TEST_ASN1_TIME_BEFORE, 0));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(mocked_X509_get_notAfter(TEST_ISSUER_X509));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(get_utc_time_from_asn_string(TEST_ASN1_TIME_AFTER.data, VALID_ASN1_TIME_STRING_UTC_LEN));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    STRICT_EXPECTED_CALL(mocked_X509_get_notAfter(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_gmtime_adj(&TEST_ASN1_TIME_AFTER, TEST_UTC_TIME_FROM_ASN1));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    if (cert_type == CERTIFICATE_TYPE_CA)
    {
        STRICT_EXPECTED_CALL(BASIC_CONSTRAINTS_new()).SetReturn(&TEST_CA_BASIC_CONSTRAINTS);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(ASN1_INTEGER_new());
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(ASN1_INTEGER_set(TEST_ASN1_INTEGER, TEST_PATH_LEN_CA));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(X509_add1_ext_i2d(TEST_X509, NID_basic_constraints, &TEST_CA_BASIC_CONSTRAINTS, 1, X509V3_ADD_DEFAULT));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BASIC_CONSTRAINTS_free(&TEST_CA_BASIC_CONSTRAINTS));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }
    else
    {
        STRICT_EXPECTED_CALL(BASIC_CONSTRAINTS_new()).SetReturn(&TEST_NON_CA_BASIC_CONSTRAINTS);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(X509_add1_ext_i2d(TEST_X509, NID_basic_constraints, &TEST_NON_CA_BASIC_CONSTRAINTS, 0, X509V3_ADD_DEFAULT));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BASIC_CONSTRAINTS_free(&TEST_NON_CA_BASIC_CONSTRAINTS));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    if (cert_type == CERTIFICATE_TYPE_CA)
    {
        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_key_usage, "critical, digitalSignature, keyCertSign"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;
    }
    else if (cert_type == CERTIFICATE_TYPE_CLIENT)
    {
        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_key_usage, "critical, nonRepudiation, digitalSignature, keyEncipherment, dataEncipherment"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;

        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_ext_key_usage, "clientAuth"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;
    }
    else
    {
        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_key_usage, "critical, nonRepudiation, digitalSignature, keyEncipherment, dataEncipherment, keyAgreement"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;

        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_ext_key_usage, "serverAuth"));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;
    }

    STRICT_EXPECTED_CALL(get_san_entries(TEST_CERT_PROPS_HANDLE, IGNORED_PTR_ARG));
    i++;

    for (size_t san_idx = 0; san_idx < TEST_NUM_SAN_ENTRIES; san_idx++)
    {
        STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, NULL, NID_subject_alt_name, (char*)TEST_SAN_ENTRIES[san_idx]));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
        i++;
    }

    X509_NAME* issuer_subject;
    if (!is_self_signed)
    {
        issuer_subject = TEST_X509_SUBJECT_ISSUER_NAME;

        STRICT_EXPECTED_CALL(X509_get_subject_name(TEST_ISSUER_X509)).SetReturn(issuer_subject);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }
    else
    {
        issuer_subject = TEST_X509_SUBJECT_NAME;
    }

    STRICT_EXPECTED_CALL(X509_get_subject_name(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    const char *country = TEST_PROPS_COUNTRY_NAME_DEFLT;
    if (set_return_subject != NULL) country = set_return_subject->country_name;
    STRICT_EXPECTED_CALL(get_country_name(TEST_CERT_PROPS_HANDLE)).SetReturn(country);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if ((!is_self_signed) && (country == NULL))
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(issuer_subject, NID_countryName, IGNORED_PTR_ARG, MAX_SUBJECT_VALUE_SIZE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (country != NULL)
    {
        STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "C", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    const char *state = TEST_PROPS_STATE_NAME_DEFLT;
    if (set_return_subject != NULL) state = set_return_subject->state_name;
    STRICT_EXPECTED_CALL(get_state_name(TEST_CERT_PROPS_HANDLE)).SetReturn(state);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if ((!is_self_signed) && (state == NULL))
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(issuer_subject, NID_stateOrProvinceName, IGNORED_PTR_ARG, MAX_SUBJECT_VALUE_SIZE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (state != NULL)
    {
        STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "ST", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    const char *locality = TEST_PROPS_LOCALITY_NAME_DEFLT;
    if (set_return_subject != NULL) locality = set_return_subject->locality_name;
    STRICT_EXPECTED_CALL(get_locality(TEST_CERT_PROPS_HANDLE)).SetReturn(locality);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if ((!is_self_signed) && (locality == NULL))
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(issuer_subject, NID_localityName, IGNORED_PTR_ARG, MAX_SUBJECT_VALUE_SIZE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (locality != NULL)
    {
        STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "L", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    const char *organization = TEST_PROPS_ORG_NAME_DEFLT;
    if (set_return_subject != NULL) organization = set_return_subject->organization_name;
    STRICT_EXPECTED_CALL(get_organization_name(TEST_CERT_PROPS_HANDLE)).SetReturn(organization);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if ((!is_self_signed) && (organization == NULL))
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(issuer_subject, NID_organizationName, IGNORED_PTR_ARG, MAX_SUBJECT_VALUE_SIZE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (organization != NULL)
    {
        STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "O", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    const char *organization_unit = TEST_PROPS_ORG_UNIT_NAME_DEFLT;
    if (set_return_subject != NULL) organization_unit = set_return_subject->organization_unit_name;
    STRICT_EXPECTED_CALL(get_organization_unit(TEST_CERT_PROPS_HANDLE)).SetReturn(organization_unit);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if ((!is_self_signed) && (organization_unit == NULL))
    {
        STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(issuer_subject, NID_organizationalUnitName, IGNORED_PTR_ARG, MAX_SUBJECT_VALUE_SIZE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (organization_unit != NULL)
    {
        STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "OU", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    STRICT_EXPECTED_CALL(X509_NAME_add_entry_by_txt(TEST_X509_SUBJECT_NAME, "CN", MBSTRING_ASC, IGNORED_PTR_ARG, -1, -1, 0));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_set_issuer_name(TEST_X509, issuer_subject));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    // subject key identifier
    STRICT_EXPECTED_CALL(X509V3_set_ctx(IGNORED_PTR_ARG, NULL, TEST_X509, NULL, NULL, 0));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, IGNORED_PTR_ARG, NID_subject_key_identifier, "hash"));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
    i++;

    // auth key identifier
    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(X509V3_set_ctx(IGNORED_PTR_ARG, TEST_ISSUER_X509, TEST_X509, NULL, NULL, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }
    else
    {
        STRICT_EXPECTED_CALL(X509V3_set_ctx(IGNORED_PTR_ARG, TEST_X509, TEST_X509, NULL, NULL, 0));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    STRICT_EXPECTED_CALL(mocked_X509V3_EXT_conf_nid(NULL, IGNORED_PTR_ARG, NID_authority_key_identifier, "issuer:always,keyid:always"));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_add_ext(TEST_X509, TEST_NID_EXTENSION, -1));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_EXTENSION_free(TEST_NID_EXTENSION));
    i++;

    EXPECTED_CALL(EVP_sha256());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(X509_sign(TEST_X509, TEST_ISSUER_EVP_KEY, TEST_EVP_SHA256_MD));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }
    else
    {
        STRICT_EXPECTED_CALL(X509_sign(TEST_X509, TEST_EVP_KEY, TEST_EVP_SHA256_MD));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    if (test_helper_is_windows())
    {
        STRICT_EXPECTED_CALL(BIO_new_file(TEST_CERT_FILE, "w")).SetReturn(TEST_BIO_WRITE_CERT);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }
    else
    {
        STRICT_EXPECTED_CALL(mocked_OPEN(TEST_CERT_FILE, EXPECTED_CREATE_FLAGS, EXPECTED_MODE_FLAGS)).SetReturn(TEST_WRITE_CERTIFICATE_FD);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        STRICT_EXPECTED_CALL(BIO_new_fd(TEST_WRITE_CERTIFICATE_FD, 0)).SetReturn(TEST_BIO_WRITE_CERT);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;
    }

    STRICT_EXPECTED_CALL(PEM_write_bio_X509(TEST_BIO_WRITE_CERT, TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(read_file_into_buffer(TEST_ISSUER_CERT_FILE, IGNORED_PTR_ARG));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        int cert_data_size = (int)(strlen(TEST_ISSUER_CERT_DATA)) + 1;
        STRICT_EXPECTED_CALL(BIO_write(TEST_BIO_WRITE_CERT, IGNORED_PTR_ARG, cert_data_size)).SetReturn(cert_data_size);
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        failed_function_list[i++] = 1;

        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO_WRITE_CERT));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if (!test_helper_is_windows())
    {
        STRICT_EXPECTED_CALL(mocked_CLOSE(TEST_WRITE_CERTIFICATE_FD));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    STRICT_EXPECTED_CALL(X509_free(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(EVP_PKEY_free(TEST_EVP_KEY));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if (!is_self_signed)
    {
        STRICT_EXPECTED_CALL(X509_free(TEST_ISSUER_X509));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(EVP_PKEY_free(TEST_ISSUER_EVP_KEY));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }
}

static void test_helper_cert_create
(
    bool is_self_signed,
    bool use_rsa,
    int key_len,
    CERTIFICATE_TYPE cert_type,
    char *failed_function_list,
    size_t failed_function_size
)
{
    test_helper_cert_create_with_subject(is_self_signed, use_rsa, key_len, cert_type,
                                         NULL, failed_function_list, failed_function_size);
}

static void test_helper_load_cert_file
(
    const char *file,
    X509 *set_return,
    size_t *index,
    char *failed_function_list,
    size_t failed_function_size
)
{
    size_t i = *index;

    STRICT_EXPECTED_CALL(BIO_new_file(file, "r"));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(PEM_read_bio_X509(TEST_BIO, NULL, NULL, NULL)).SetReturn(set_return);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    *index = i;
}

static void test_helper_verify_certificate
(
    VERIFY_CERT_TEST_PARAMS *params,
    char *failed_function_list,
    size_t failed_function_size
)
{
    size_t i = 0;

    umock_c_reset_all_calls();

    EXPECTED_CALL(initialize_openssl());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(read_file_into_cstring(params->cert_file, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(read_file_into_cstring(params->issuer_cert_file, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_STORE_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(X509_LOOKUP_file());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_STORE_add_lookup(TEST_X509_STORE, TEST_X509_LOOKUP_METHOD_FILE)).SetReturn(TEST_X509_LOOKUP_LOAD_FILE);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_LOOKUP_ctrl(TEST_X509_LOOKUP_LOAD_FILE, IGNORED_NUM_ARG, params->issuer_cert_file, X509_FILETYPE_PEM, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    EXPECTED_CALL(X509_LOOKUP_hash_dir());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_STORE_add_lookup(TEST_X509_STORE, TEST_X509_LOOKUP_METHOD_HASH)).SetReturn(TEST_X509_LOOKUP_LOAD_HASH);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_LOOKUP_ctrl(TEST_X509_LOOKUP_LOAD_HASH, IGNORED_NUM_ARG, NULL, X509_FILETYPE_DEFAULT, NULL));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    test_helper_load_cert_file(TEST_CERT_FILE, TEST_X509, &i, failed_function_list, failed_function_size);

    STRICT_EXPECTED_CALL(X509_STORE_CTX_new());
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    unsigned long policy = X509_V_FLAG_X509_STRICT |
                           X509_V_FLAG_CHECK_SS_SIGNATURE |
                           X509_V_FLAG_POLICY_CHECK;

    STRICT_EXPECTED_CALL(X509_STORE_set_flags(TEST_X509_STORE, policy));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_STORE_CTX_init(TEST_STORE_CTXT, TEST_X509_STORE, TEST_X509, 0));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    ASN1_TIME *asn1_time = (params->force_set_asn1_time != NULL) ? params->force_set_asn1_time : &TEST_ASN1_TIME_AFTER;
    STRICT_EXPECTED_CALL(mocked_X509_get_notAfter(TEST_X509)).SetReturn(asn1_time);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(get_utc_time_from_asn_string(asn1_time->data, VALID_ASN1_TIME_STRING_UTC_LEN));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    int skid_nid_lookup = params->skid_set ? 1 : -1;
    STRICT_EXPECTED_CALL(X509_get_ext_by_NID(TEST_X509, NID_subject_key_identifier, -1)).SetReturn(skid_nid_lookup);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    int return_value = (params->force_set_verify_return_value)?1:0;
    STRICT_EXPECTED_CALL(X509_verify_cert(TEST_STORE_CTXT)).SetReturn(return_value);
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    if(!params->force_set_verify_return_value)
    {
        STRICT_EXPECTED_CALL(X509_STORE_CTX_get_error(TEST_STORE_CTXT));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;

        STRICT_EXPECTED_CALL(X509_verify_cert_error_string(TEST_ERROR_CODE));
        ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
        i++;
    }

    STRICT_EXPECTED_CALL(X509_STORE_CTX_free(TEST_STORE_CTXT));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_free(TEST_X509));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(X509_STORE_free(TEST_X509_STORE));
    ASSERT_IS_TRUE_WITH_MSG((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(edge_openssl_pki_unittests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);
        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types() );
        ASSERT_ARE_EQUAL(int, 0, umocktypes_stdint_register_types() );

        REGISTER_UMOCK_ALIAS_TYPE(KEY_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(CERT_PROPS_HANDLE, void*);
        REGISTER_UMOCK_ALIAS_TYPE(CERTIFICATE_TYPE, int);
        REGISTER_UMOCK_ALIAS_TYPE(MODE_T, int);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, test_hook_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_calloc, test_hook_gballoc_calloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_calloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_realloc, test_hook_gballoc_realloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_realloc, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, test_hook_gballoc_free);

        REGISTER_GLOBAL_MOCK_HOOK(read_file_into_cstring, test_hook_read_file_into_cstring);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(read_file_into_cstring, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(mocked_OPEN, test_hook_mocked_OPEN);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_OPEN, -1);
        REGISTER_GLOBAL_MOCK_HOOK(mocked_CLOSE, test_hook_mocked_CLOSE);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_CLOSE, -1);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_new, test_hook_EVP_PKEY_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_PKEY_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_free, test_hook_EVP_PKEY_free);

        REGISTER_GLOBAL_MOCK_HOOK(BN_new, test_hook_BN_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BN_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(BN_set_word, test_hook_BN_set_word);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BN_set_word, 0);
        REGISTER_GLOBAL_MOCK_HOOK(BN_free, test_hook_BN_free);

        REGISTER_GLOBAL_MOCK_HOOK(RSA_new, test_hook_RSA_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(RSA_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(RSA_free, test_hook_RSA_free);
        REGISTER_GLOBAL_MOCK_HOOK(RSA_generate_key_ex, test_hook_RSA_generate_key_ex);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(RSA_generate_key_ex, 0);

        REGISTER_GLOBAL_MOCK_HOOK(OBJ_txt2nid, test_hook_OBJ_txt2nid);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(OBJ_txt2nid, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EC_KEY_new_by_curve_name, test_hook_EC_KEY_new_by_curve_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EC_KEY_new_by_curve_name, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EC_KEY_set_asn1_flag, test_hook_EC_KEY_set_asn1_flag);

        REGISTER_GLOBAL_MOCK_HOOK(EC_KEY_generate_key, test_hook_EC_KEY_generate_key);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EC_KEY_generate_key, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_set1_EC_KEY, test_hook_EVP_PKEY_set1_EC_KEY);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_PKEY_set1_EC_KEY, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EC_KEY_free, test_hook_EC_KEY_free);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_pubkey, test_hook_X509_get_pubkey);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_get_pubkey, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_base_id, test_hook_EVP_PKEY_base_id);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_PKEY_base_id, EVP_PKEY_NONE);

        REGISTER_GLOBAL_MOCK_HOOK(RSA_generate_key, test_hook_RSA_generate_key);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(RSA_generate_key, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_set1_RSA, test_hook_EVP_PKEY_set1_RSA);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_PKEY_set1_RSA, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_get1_EC_KEY, test_hook_EVP_PKEY_get1_EC_KEY);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EVP_PKEY_get1_EC_KEY, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EC_KEY_get0_group, test_hook_EC_KEY_get0_group);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EC_KEY_get0_group, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(OBJ_nid2sn, test_hook_OBJ_nid2sn);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(OBJ_nid2sn, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(EC_GROUP_get_curve_name, test_hook_EC_GROUP_get_curve_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(EC_GROUP_get_curve_name, 0);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_PKEY_bits, test_hook_EVP_PKEY_bits);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_new_file, test_hook_BIO_new_file);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_new_file, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_new_fd, test_hook_BIO_new_fd);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_new_fd, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_X509_INFO_write_bio, test_hook_PEM_X509_INFO_write_bio);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_X509_INFO_write_bio, 0);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_write, test_hook_BIO_write);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_write, 0);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_free_all, test_hook_BIO_free_all);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_read_bio_PrivateKey, test_hook_PEM_read_bio_PrivateKey);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_read_bio_PrivateKey, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_set_version, test_hook_X509_set_version);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_set_version, 0);

        REGISTER_GLOBAL_MOCK_HOOK(ASN1_INTEGER_set, test_hook_ASN1_INTEGER_set);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(ASN1_INTEGER_set, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_set_pubkey, test_hook_X509_set_pubkey);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_set_pubkey, 0);

        REGISTER_GLOBAL_MOCK_HOOK(get_utc_time_from_asn_string, test_hook_get_utc_time_from_asn_string);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_utc_time_from_asn_string, 0);

        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notBefore, test_hook_X509_get_notBefore);
        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notAfter, test_hook_X509_get_notAfter);

        REGISTER_GLOBAL_MOCK_HOOK(X509_gmtime_adj, test_hook_X509_gmtime_adj);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_gmtime_adj, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(read_file_into_buffer, test_hook_read_file_into_buffer);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(read_file_into_buffer, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_write_bio_PrivateKey, test_hook_PEM_write_bio_PrivateKey);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_write_bio_PrivateKey, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_serialNumber, test_hook_X509_get_serialNumber);

        REGISTER_GLOBAL_MOCK_HOOK(BASIC_CONSTRAINTS_new, test_hook_BASIC_CONSTRAINTS_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BASIC_CONSTRAINTS_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(BASIC_CONSTRAINTS_free, test_hook_BASIC_CONSTRAINTS_free);

        REGISTER_GLOBAL_MOCK_HOOK(ASN1_INTEGER_new, test_hook_ASN1_INTEGER_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(ASN1_INTEGER_new, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_add1_ext_i2d, test_hook_X509_add1_ext_i2d);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_add1_ext_i2d, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_NAME_get_text_by_NID, test_hook_X509_NAME_get_text_by_NID);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_NAME_get_text_by_NID, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_NAME_add_entry_by_txt, test_hook_X509_NAME_add_entry_by_txt);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_NAME_add_entry_by_txt, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_subject_name, test_hook_X509_get_subject_name);

        REGISTER_GLOBAL_MOCK_HOOK(X509_set_issuer_name, test_hook_X509_set_issuer_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_set_issuer_name, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_new, test_hook_X509_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(X509_free, test_hook_X509_free);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_new, test_hook_X509_STORE_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_free, test_hook_X509_STORE_free);

        REGISTER_GLOBAL_MOCK_HOOK(EVP_sha256, test_hook_EVP_sha256);

        REGISTER_GLOBAL_MOCK_HOOK(X509_sign, test_hook_X509_sign);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_sign, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_verify, test_hook_X509_verify);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_verify, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_verify_cert, test_hook_X509_verify_cert);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_verify_cert, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_CTX_new, test_hook_X509_STORE_CTX_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_CTX_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_CTX_free, test_hook_X509_STORE_CTX_free);

        REGISTER_GLOBAL_MOCK_HOOK(X509_verify_cert_error_string, test_hook_X509_verify_cert_error_string);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_verify_cert_error_string, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_set_flags, test_hook_X509_STORE_set_flags);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_set_flags, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_CTX_get_error, test_hook_X509_STORE_CTX_get_error);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_CTX_get_error, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_LOOKUP_file, test_hook_X509_LOOKUP_file);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_LOOKUP_file, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_add_lookup, test_hook_X509_STORE_add_lookup);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_add_lookup, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_LOOKUP_ctrl, test_hook_X509_LOOKUP_ctrl);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_LOOKUP_ctrl, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_LOOKUP_hash_dir, test_hook_X509_LOOKUP_hash_dir);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_LOOKUP_hash_dir, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_read_bio_X509, test_hook_PEM_read_bio_X509);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_read_bio_X509, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_write_bio_X509, test_hook_PEM_write_bio_X509);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_write_bio_X509, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_STORE_CTX_init, test_hook_X509_STORE_CTX_init);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_STORE_CTX_init, 0);

        REGISTER_GLOBAL_MOCK_HOOK(get_validity_seconds, test_hook_get_validity_seconds);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_validity_seconds, 0);

        REGISTER_GLOBAL_MOCK_HOOK(get_common_name, test_hook_get_common_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_common_name, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_country_name, test_hook_get_country_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_country_name, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_state_name, test_hook_get_state_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_state_name, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_locality, test_hook_get_locality);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_locality, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_organization_name, test_hook_get_organization_name);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_organization_name, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_organization_unit, test_hook_get_organization_unit);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_organization_unit, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(get_certificate_type, test_hook_get_certificate_type);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(get_certificate_type, CERTIFICATE_TYPE_UNKNOWN);

        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509V3_EXT_conf_nid, test_hook_mocked_X509V3_EXT_conf_nid);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(mocked_X509V3_EXT_conf_nid, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(X509_add_ext, test_hook_X509_add_ext);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_add_ext, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_EXTENSION_free, test_hook_X509_EXTENSION_free);

        REGISTER_GLOBAL_MOCK_HOOK(get_san_entries, test_hook_get_san_entries);

        REGISTER_GLOBAL_MOCK_HOOK(X509V3_set_ctx, test_hook_X509V3_set_ctx);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_ext_by_NID, test_hook_X509_get_ext_by_NID);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        umock_c_deinit();

        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }

        umock_c_reset_all_calls();
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_invalid_params)
    {
        // arrange
        int status;

        // act, assert
        status = generate_pki_cert_and_key(NULL, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, NULL, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, NULL, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, NULL, TEST_ISSUER_CERT_FILE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, -1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_invalid_validity_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_validity_seconds(TEST_CERT_PROPS_HANDLE)).SetReturn(0);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_null_common_name_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_common_name(TEST_CERT_PROPS_HANDLE)).SetReturn(NULL);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_empty_common_name_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_common_name(TEST_CERT_PROPS_HANDLE)).SetReturn("");

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_non_zero_pathlen_for_non_ca_certtype_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_certificate_type(TEST_CERT_PROPS_HANDLE)).SetReturn(CERTIFICATE_TYPE_SERVER);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_ca_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, true, TEST_VALID_RSA_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_ca_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, true, TEST_VALID_RSA_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_server_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, true, TEST_VALID_RSA_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_server_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, true, TEST_VALID_RSA_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_client_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, true, TEST_VALID_RSA_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_rsa_client_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, true, TEST_VALID_RSA_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_ca_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, false, TEST_VALID_ECC_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_ca_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, false, TEST_VALID_ECC_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 1, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_server_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, false, TEST_VALID_ECC_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_server_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, false, TEST_VALID_ECC_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_client_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);

        test_helper_cert_create(false, false, TEST_VALID_ECC_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key
    */
    TEST_FUNCTION(generate_pki_cert_and_key_ecc_client_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(false, false, TEST_VALID_ECC_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, 0, TEST_KEY_FILE, TEST_CERT_FILE, TEST_ISSUER_KEY_FILE, TEST_ISSUER_CERT_FILE);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_invalid_params)
    {
        // arrange
        int status;
        PKI_KEY_PROPS INVALID_KEY_PROPS = { .key_type = -1, .ec_curve_name = NULL };

        // act, assert
        status = generate_pki_cert_and_key_with_props(NULL, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, NULL, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, NULL, &TEST_VALID_KEY_PROPS_RSA);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &INVALID_KEY_PROPS);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, -1, TEST_KEY_FILE, TEST_CERT_FILE, &INVALID_KEY_PROPS);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_invalid_validity_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_validity_seconds(TEST_CERT_PROPS_HANDLE)).SetReturn(0);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, -1, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_null_common_name_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_common_name(TEST_CERT_PROPS_HANDLE)).SetReturn(NULL);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_empty_common_name_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_common_name(TEST_CERT_PROPS_HANDLE)).SetReturn("");

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_non_zero_pathlen_for_non_ca_certtype_returns_errors)
    {
        // arrange
        int status;

        STRICT_EXPECTED_CALL(get_certificate_type(TEST_CERT_PROPS_HANDLE)).SetReturn(CERTIFICATE_TYPE_SERVER);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, -1, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_ca_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_ca_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_server_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_server_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_client_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_rsa_client_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, true, TEST_VALID_RSA_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_RSA);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_ca_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_ca_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_CA_CERT_KEY_LEN, CERTIFICATE_TYPE_CA, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_server_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_server_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_SERVER_KEY_LEN, CERTIFICATE_TYPE_SERVER, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_client_success)
    {
        // arrange
        int status;
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        // act
        status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   generate_pki_cert_and_key_with_props
    */
    TEST_FUNCTION(generate_pki_cert_and_key_with_props_ecc_client_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        test_helper_cert_create(true, false, TEST_VALID_ECC_CLIENT_KEY_LEN, CERTIFICATE_TYPE_CLIENT, failed_function_list, failed_function_size);

        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            if (failed_function_list[i] == 1)
            {
                // act
                int status = generate_pki_cert_and_key_with_props(TEST_CERT_PROPS_HANDLE, TEST_SERIAL_NUMBER, TEST_PATH_LEN_NON_CA, TEST_KEY_FILE, TEST_CERT_FILE, &TEST_VALID_KEY_PROPS_ECC);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_invalid_parameters_returns_error)
    {
        // arrange
        bool verify_status;
        int status;

        // act, assert
        verify_status = true;
        status = verify_certificate(NULL, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));

        verify_status = true;
        status = verify_certificate(TEST_CERT_FILE, NULL, TEST_ISSUER_CERT_FILE, &verify_status);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));

        verify_status = true;
        status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, NULL, &verify_status);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));

        status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, NULL);
        ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_verifies_true_and_returns_success)
    {
        // arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        VERIFY_CERT_TEST_PARAMS params;

        params.cert_file = TEST_CERT_FILE;
        params.key_file = TEST_KEY_FILE;
        params.issuer_cert_file = TEST_ISSUER_CERT_FILE;
        params.force_set_verify_return_value = true;
        params.force_set_asn1_time = NULL;
        params.skid_set = true;

        test_helper_verify_certificate(&params, failed_function_list, failed_function_size);
        bool verify_status = true;

        // act
        int status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_TRUE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(invalid_chain_cert_data_verifies_false_and_returns_success)
    {
        // arrange
        bool verify_status = false;

        EXPECTED_CALL(initialize_openssl());
        STRICT_EXPECTED_CALL(read_file_into_cstring(TEST_BAD_CHAIN_CERT_FILE, NULL));
        STRICT_EXPECTED_CALL(read_file_into_cstring(TEST_ISSUER_CERT_FILE, NULL));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        // act
        int status = verify_certificate(TEST_BAD_CHAIN_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_verifies_false_and_returns_success)
    {
        // arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        VERIFY_CERT_TEST_PARAMS params;

        params.cert_file = TEST_CERT_FILE;
        params.key_file = TEST_KEY_FILE;
        params.issuer_cert_file = TEST_ISSUER_CERT_FILE;
        params.force_set_verify_return_value = false;
        params.force_set_asn1_time = NULL;
        params.skid_set = true;

        test_helper_verify_certificate(&params, failed_function_list, failed_function_size);
        bool verify_status = false;

        // act
        int status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL_WITH_MSG(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls(), "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_expired_certificate_verifies_false_and_returns_success)
    {
        // arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        VERIFY_CERT_TEST_PARAMS params;

        params.cert_file = TEST_CERT_FILE;
        params.key_file = TEST_KEY_FILE;
        params.issuer_cert_file = TEST_ISSUER_CERT_FILE;
        params.force_set_verify_return_value = false;
        params.force_set_asn1_time = &TEST_ASN1_TIME_AFTER_EXPIRED;
        params.skid_set = true;

        test_helper_verify_certificate(&params, failed_function_list, failed_function_size);
        bool verify_status = true;

        // act
        int status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_without_subj_keyid_verifies_false_and_returns_success)
    {
        // arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        VERIFY_CERT_TEST_PARAMS params;

        params.cert_file = TEST_CERT_FILE;
        params.key_file = TEST_KEY_FILE;
        params.issuer_cert_file = TEST_ISSUER_CERT_FILE;
        params.force_set_verify_return_value = false;
        params.force_set_asn1_time = NULL;
        params.skid_set = false;

        test_helper_verify_certificate(&params, failed_function_list, failed_function_size);
        bool verify_status = true;

        // act
        int status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

        // assert
        ASSERT_ARE_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
        ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));

        // cleanup
    }

    /**
     * Test function for API
     *   verify_certificate
    */
    TEST_FUNCTION(verify_certificate_negative)
    {
        // arrange
        int test_result = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, test_result);

        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];
        memset(failed_function_list, 0, failed_function_size);
        VERIFY_CERT_TEST_PARAMS params;

        params.cert_file = TEST_CERT_FILE;
        params.key_file = TEST_KEY_FILE;
        params.issuer_cert_file = TEST_ISSUER_CERT_FILE;
        params.force_set_verify_return_value = true;
        params.force_set_asn1_time = NULL;
        params.skid_set = true;

        test_helper_verify_certificate(&params, failed_function_list, failed_function_size);
        umock_c_negative_tests_snapshot();

        for (size_t i = 0; i < umock_c_negative_tests_call_count(); i++)
        {
            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(i);

            bool verify_status;
            if (failed_function_list[i] == 1)
            {
                // act
                int status = verify_certificate(TEST_CERT_FILE, TEST_KEY_FILE, TEST_ISSUER_CERT_FILE, &verify_status);

                // assert
                ASSERT_ARE_NOT_EQUAL_WITH_MSG(int, 0, status, "Line:" TOSTRING(__LINE__));
                ASSERT_IS_FALSE_WITH_MSG(verify_status, "Line:" TOSTRING(__LINE__));
            }
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

END_TEST_SUITE(edge_openssl_pki_unittests)
