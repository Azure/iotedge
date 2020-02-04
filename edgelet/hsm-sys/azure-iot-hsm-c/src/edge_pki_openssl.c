#include <fcntl.h>
#include <inttypes.h>
#include <limits.h>
#include <stdbool.h>
#include <stdlib.h>
#include <stdint.h>
#include <string.h>
#include <sys/stat.h>
#include <time.h>

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    #include <io.h>
#else
    #include <unistd.h>
#endif

#include <openssl/asn1.h>
#include <openssl/bio.h>
#include <openssl/err.h>
#include <openssl/ec.h>
#include <openssl/pem.h>
#include <openssl/rand.h>
#include <openssl/x509.h>
#include <openssl/x509v3.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/hmacsha256.h"
#include "edge_openssl_common.h"

#include "hsm_err.h"
#include "hsm_key.h"
#include "hsm_log.h"
#include "hsm_utils.h"

//#################################################################################################
// Data type definations
//#################################################################################################
// all X.509 certificates created will be v3 for which the version value is 2
#define X509_VERSION 0x2

// RSA key length for CA certificates
#define RSA_KEY_LEN_CA 4096
// RSA key length for server and client certificates
#define RSA_KEY_LEN_NON_CA RSA_KEY_LEN_CA >> 1

#define MAX_SUBJECT_FIELD_SIZE 3
// per RFC3280 state and locality have lengths of 128, +1 for null term
#define MAX_SUBJECT_VALUE_SIZE 129

#define DEFAULT_EC_CURVE_NAME "secp256k1"

// openssl ASN1 time format defines
#define ASN1_TIME_STRING_UTC_FORMAT 0x17
#define ASN1_TIME_STRING_UTC_LEN 13

// DER encoding defines BOOLEAN TRUE as 0xff
#define ASN1_DER_TRUE 0xff
#define ASN1_DER_FALSE 0x00

struct SUBJECT_FIELD_OFFSET_TAG
{
    char field[MAX_SUBJECT_FIELD_SIZE];
    int offset;
};
typedef struct SUBJECT_FIELD_OFFSET_TAG SUBJECT_FIELD_OFFSET;

static const SUBJECT_FIELD_OFFSET subj_offsets[] =
{
    { "CN", NID_commonName },
    { "C", NID_countryName },
    { "L", NID_localityName },
    { "ST", NID_stateOrProvinceName },
    { "O", NID_organizationName },
    { "OU", NID_organizationalUnitName }
};

struct CERT_KEY_TAG
{
    HSM_CLIENT_KEY_INTERFACE intf;
    EVP_PKEY* evp_key;
};
typedef struct CERT_KEY_TAG CERT_KEY;

//#################################################################################################
// Forward Declarations
//#################################################################################################

static void destroy_evp_key(EVP_PKEY *evp_key);

//#################################################################################################
// Utilities
//#################################################################################################
#if !defined(OPEN_HELPER) && !defined(CLOSE_HELPER)
    #if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
        #define OPEN_HELPER(fname) _open((fname), _O_CREAT|_O_WRONLY|_O_TRUNC, _S_IREAD|_S_IWRITE)
        #define CLOSE_HELPER(fd) _close(fd)
    #else
        #define OPEN_HELPER(fname) open((fname), O_CREAT|O_WRONLY|O_TRUNC, S_IRUSR|S_IWUSR)
        #define CLOSE_HELPER(fd) close(fd)
    #endif
#endif

#if !defined(X509V3_EXT_conf_nid_HELPER)
    #define X509V3_EXT_conf_nid_HELPER(conf, ctx, nid, value) \
        X509V3_EXT_conf_nid((conf), (ctx), (nid), (value))
#endif

extern time_t get_utc_time_from_asn_string(const unsigned char *time_value, size_t length);

//#################################################################################################
// PKI key operations
//#################################################################################################

// implementation from https://wiki.openssl.org/index.php/EVP_Signing_and_Verifying
static int caluclate_hmac_sha256
(
    EVP_PKEY *evp_key,
    const unsigned char *tbs,
    size_t tbs_size,
    unsigned char **digest,
    size_t *digest_size
)
{
    int result, status;
    size_t sign_size = 0;
    unsigned char *signature;
    EVP_MD_CTX* ctx;
    const EVP_MD *md;

    if ((ctx = EVP_MD_CTX_create()) == NULL)
    {
        LOG_ERROR("EVP_MD_CTX_create failed");
        result = __FAILURE__;
    }
    else if ((md = EVP_get_digestbyname("SHA256")) == NULL)
    {
        LOG_ERROR("EVP_get_digestbyname failed for SHA256");
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if ((status = EVP_DigestInit_ex(ctx, md, NULL)) != 1)
    {
        LOG_ERROR("EVP_DigestInit_ex failed with error code %d", status);
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if ((status = EVP_DigestSignInit(ctx, NULL, md, NULL, evp_key)) != 1)
    {
        LOG_ERROR("EVP_DigestSignInit failed with error code %d", status);
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if ((status = EVP_DigestSignUpdate(ctx, tbs, tbs_size)) != 1)
    {
        LOG_ERROR("EVP_DigestSignUpdate failed with error code %d", status);
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if ((status = EVP_DigestSignFinal(ctx, NULL, &sign_size)) != 1)
    {
        LOG_ERROR("EVP_DigestSignFinal failed with error code %d", status);
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if (sign_size == 0)
    {
        LOG_ERROR("EVP_DigestSignFinal returned digest size as zero");
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else if ((signature = malloc(sign_size)) == NULL)
    {
        LOG_ERROR("Failed to allocate memory for digest");
        EVP_MD_CTX_destroy(ctx);
        result = __FAILURE__;
    }
    else
    {
        size_t temp_size = sign_size;
        if ((status = EVP_DigestSignFinal(ctx, signature, &temp_size)) != 1)
        {
            LOG_ERROR("EVP_DigestSignFinal failed with error code %d", status);
            free(signature);
            EVP_MD_CTX_destroy(ctx);
            result = __FAILURE__;
        }
        else if (sign_size != temp_size)
        {
            LOG_ERROR("Mismatched signature lengths. Expected %zu Got %zu", sign_size, temp_size);
            free(signature);
            EVP_MD_CTX_destroy(ctx);
            result = __FAILURE__;
        }
        else
        {
            *digest = signature;
            *digest_size = sign_size;
            EVP_MD_CTX_destroy(ctx);
            result = 0;
        }
    }

    return result;
}

static int cert_key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char *data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char **digest,
    size_t *digest_size
)
{
    int result;
    CERT_KEY *cert_key = (CERT_KEY*)key_handle;

    bool digest_params_invalid = (digest == NULL) || (digest_size == NULL);
    // make sure sane values are always returned
    if (digest != NULL)
    {
        *digest = NULL;
    }
    if (digest_size != NULL)
    {
        *digest_size = 0;
    }
    if (digest_params_invalid)
    {
        LOG_ERROR("Invalid digest and or digest_size value");
        result = __FAILURE__;
    }
    else if (cert_key == NULL)
    {
        LOG_ERROR("Invalid key handle");
        result = __FAILURE__;
    }
    else if ((data_to_be_signed == NULL) || (data_to_be_signed_size == 0))
    {
        LOG_ERROR("Invalid data and or data size value");
        result = __FAILURE__;
    }
    else
    {
        result = caluclate_hmac_sha256(cert_key->evp_key,
                                       data_to_be_signed,
                                       data_to_be_signed_size,
                                       digest,
                                       digest_size);
    }

    return result;
}

int cert_key_derive_and_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    (void)key_handle;
    (void)data_to_be_signed;
    (void)data_to_be_signed_size;
    (void)identity;
    (void)identity_size;

    LOG_ERROR("Derive and sign for cert keys is not supported");
    if (digest != NULL)
    {
        *digest = NULL;
    }
    if (digest_size != NULL)
    {
        *digest_size = 0;
    }
    return __FAILURE__;
}

static int cert_key_encrypt
(
    KEY_HANDLE key_handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *plaintext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *ciphertext
)
{
    (void)key_handle;
    (void)identity;
    (void)plaintext;
    (void)initialization_vector;

    LOG_ERROR("Cert key encrypt operation not supported");
    ciphertext->buffer = NULL;
    ciphertext->size = 0;
    return __FAILURE__;
}

static int cert_key_decrypt
(
    KEY_HANDLE key_handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *ciphertext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *plaintext
)
{
    (void)key_handle;
    (void)identity;
    (void)ciphertext;
    (void)initialization_vector;

    LOG_ERROR("Cert key decrypt operation not supported");
    plaintext->buffer = NULL;
    plaintext->size = 0;
    return __FAILURE__;
}

static void cert_key_destroy(KEY_HANDLE key_handle)
{
    CERT_KEY *cert_key = (CERT_KEY*)key_handle;
    if (cert_key != NULL)
    {
        destroy_evp_key(cert_key->evp_key);
        free(cert_key);
    }
}

//#################################################################################################
// PKI key generation
//#################################################################################################
static EVP_PKEY* generate_rsa_key(CERTIFICATE_TYPE cert_type)
{
    int status;
    BIGNUM *bne;
    EVP_PKEY *pkey;
    RSA *rsa;

    size_t key_len = (cert_type == CERTIFICATE_TYPE_CA) ? RSA_KEY_LEN_CA : RSA_KEY_LEN_NON_CA;
    LOG_INFO("Generating RSA key of length %zu", key_len);
    if ((pkey = EVP_PKEY_new()) == NULL)
    {
        LOG_ERROR("Unable to create EVP_PKEY structure");
    }
    else if ((bne = BN_new()) == NULL)
    {
        LOG_ERROR("Could not allocate new big num object");
        EVP_PKEY_free(pkey);
        pkey = NULL;
    }
    else if ((status = BN_set_word(bne, RSA_F4)) != 1)
    {
        LOG_ERROR("Unable to set big num word");
        BN_free(bne);
        EVP_PKEY_free(pkey);
        pkey = NULL;
    }
    else if ((rsa = RSA_new()) == NULL)
    {
        LOG_ERROR("Could not allocate new RSA object");
        BN_free(bne);
        EVP_PKEY_free(pkey);
        pkey = NULL;
    }
    else if ((status = RSA_generate_key_ex(rsa, (int)key_len, bne, NULL)) != 1)
    {
        LOG_ERROR("Unable to generate RSA key");
        RSA_free(rsa);
        BN_free(bne);
        EVP_PKEY_free(pkey);
        pkey = NULL;
    }
    else if ((status = EVP_PKEY_set1_RSA(pkey, rsa)) != 1)
    {
        LOG_ERROR("Unable to assign RSA key.");
        RSA_free(rsa);
        BN_free(bne);
        EVP_PKEY_free(pkey);
        pkey = NULL;
    }
    else
    {
        RSA_free(rsa);
        BN_free(bne);
    }

    return pkey;
}

static EVP_PKEY* generate_ecc_key(const char *ecc_type)
{
    EC_KEY* ecc_key;
    EVP_PKEY *evp_key;

    int ecc_group = OBJ_txt2nid(ecc_type);

    if ((ecc_key = EC_KEY_new_by_curve_name(ecc_group)) == NULL)
    {
        LOG_ERROR("Failure getting curve name");
        evp_key = NULL;
    }
    else
    {
        EC_KEY_set_asn1_flag(ecc_key, OPENSSL_EC_NAMED_CURVE);
        if (!EC_KEY_generate_key(ecc_key))
        {
            LOG_ERROR("Error generating ECC key");
            evp_key = NULL;
        }
        else if ((evp_key = EVP_PKEY_new()) == NULL)
        {
            LOG_ERROR("Unable to create EVP_PKEY structure");
        }
        else if (!EVP_PKEY_set1_EC_KEY(evp_key, ecc_key))
        {
            LOG_ERROR("Error assigning ECC key to EVP_PKEY structure");
            EVP_PKEY_free(evp_key);
            evp_key = NULL;
        }
        EC_KEY_free(ecc_key);
    }

    return evp_key;
}

static EVP_PKEY* generate_evp_key
(
    CERTIFICATE_TYPE cert_type,
    X509* issuer_cert,
    const PKI_KEY_PROPS *key_props
)
{
    EVP_PKEY *evp_key;

    if (issuer_cert == NULL)
    {
        if ((key_props != NULL) && (key_props->key_type == HSM_PKI_KEY_EC))
        {
            const char *curve = (key_props->ec_curve_name != NULL) ? key_props->ec_curve_name :
                                                                     DEFAULT_EC_CURVE_NAME;
            evp_key = generate_ecc_key(curve);
        }
        else
        {
            // by default use RSA keys if no issuer cert or key properties was provided
            evp_key = generate_rsa_key(cert_type);
        }
    }
    else
    {
        EVP_PKEY *evp_pub_key = NULL;
        // read the public key from the issuer certificate and determine the type
        // of key used and then generate the appropriate type of key
        if ((evp_pub_key = X509_get_pubkey(issuer_cert)) == NULL)
        {
            LOG_ERROR("Error getting public key from issuer certificate");
            evp_key = NULL;
        }
        else
        {
            int key_type = EVP_PKEY_base_id(evp_pub_key);
            switch (key_type)
            {
                case EVP_PKEY_RSA:
                {
                    evp_key = generate_rsa_key(cert_type);
                }
                break;

                case EVP_PKEY_EC:
                {
                    EC_KEY *ecc_key = EVP_PKEY_get1_EC_KEY(evp_pub_key);
                    const EC_GROUP* ecgrp = EC_KEY_get0_group(ecc_key);
                    const char *curve_name = OBJ_nid2sn(EC_GROUP_get_curve_name(ecgrp));
                    LOG_INFO("Generating ECC Key size: %d bits. ECC Key type: %s",
                             EVP_PKEY_bits(evp_pub_key), curve_name);
                    evp_key = generate_ecc_key(curve_name);
                    EC_KEY_free(ecc_key);
                }
                break;

                default:
                    LOG_ERROR("Unsupported key type %d", key_type);
                    evp_key = NULL;
            };
            EVP_PKEY_free(evp_pub_key);
        }
    }

    return evp_key;
}

static void destroy_evp_key(EVP_PKEY *evp_key)
{
    if (evp_key != NULL)
    {
        EVP_PKEY_free(evp_key);
    }
}

//#################################################################################################
// PKI file IO
//#################################################################################################
static X509* load_certificate_file(const char* cert_file_name)
{
    X509* x509_cert;
    BIO* cert_file = BIO_new_file(cert_file_name, "rb");
    if (cert_file == NULL)
    {
        LOG_ERROR("Failure to open certificate file %s", cert_file_name);
        x509_cert = NULL;
    }
    else
    {
        x509_cert = PEM_read_bio_X509(cert_file, NULL, NULL, NULL);
        if (x509_cert == NULL)
        {
            LOG_ERROR("Failure PEM_read_bio_X509 for cert %s", cert_file_name);
        }
        BIO_free_all(cert_file);
    }

    return x509_cert;
}

static int bio_chain_cert_helper(BIO *cert_file, const char *issuer_cert_file_name)
{
    int result;
    size_t issuer_buf_size = 0;

    void *issuer_cert = read_file_into_buffer(issuer_cert_file_name, &issuer_buf_size);
    if (issuer_cert == NULL)
    {
        LOG_ERROR("Could not read issuer certificate file %s", issuer_cert_file_name);
        result = __FAILURE__;
    }
    else
    {
        if (issuer_buf_size == 0)
        {
            LOG_ERROR("Read zero bytes from issuer certificate file %s", issuer_cert_file_name);
            result = __FAILURE__;
        }
        else if (issuer_buf_size > INT_MAX)
        {
            LOG_ERROR("Issuer certificate file too large %s", issuer_cert_file_name);
            result = __FAILURE__;
        }
        else
        {
            int len = BIO_write(cert_file, issuer_cert, (int)issuer_buf_size);
            if (len != (int)issuer_buf_size)
            {
                LOG_ERROR("BIO_write returned %d expected %zu", len, issuer_buf_size);
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
        }
        free(issuer_cert);
    }

    return result;
}

static int write_certificate_file
(
    X509 *x509_cert,
    const char *cert_file_name,
    const char *issuer_certificate_file
)
{
    int result;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    BIO* cert_file = BIO_new_file(cert_file_name, "wb");
    if (cert_file == NULL)
    {
        LOG_ERROR("Failure opening cert file for writing for %s", cert_file_name);
        result = __FAILURE__;
    }
    else
    {
        if (!PEM_write_bio_X509(cert_file, x509_cert))
        {
            LOG_ERROR("Unable to write certificate to file %s", cert_file_name);
            result = __FAILURE__;
        }
        else if ((issuer_certificate_file != NULL) &&
                 (bio_chain_cert_helper(cert_file, issuer_certificate_file) != 0))
        {
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        BIO_free_all(cert_file);
    }
#else
    int fd = OPEN_HELPER(cert_file_name);
    if (fd == -1)
    {
        LOG_ERROR("Failure opening cert file for writing for %s", cert_file_name);
        result = __FAILURE__;
    }
    else
    {
        BIO *cert_file;
        if ((cert_file = BIO_new_fd(fd, 0)) == NULL)
        {
            LOG_ERROR("Failure creating new BIO handle for %s", cert_file_name);
            result = __FAILURE__;
        }
        else
        {
            if (!PEM_write_bio_X509(cert_file, x509_cert))
            {
                LOG_ERROR("Unable to write certificate to file %s", cert_file_name);
                result = __FAILURE__;
            }
            else if ((issuer_certificate_file != NULL) &&
                     (bio_chain_cert_helper(cert_file, issuer_certificate_file) != 0))
            {
                result = __FAILURE__;
            }
            else
            {
                result = 0;
            }
            BIO_free_all(cert_file);
        }
        (void)CLOSE_HELPER(fd);
    }
#endif

    return result;
}

static EVP_PKEY* load_private_key_file(const char* key_file_name)
{
    EVP_PKEY* evp_key;
    BIO* key_file = BIO_new_file(key_file_name, "rb");
    if (key_file == NULL)
    {
        LOG_ERROR("Failure to open key file %s", key_file_name);
        evp_key = NULL;
    }
    else
    {
        evp_key = PEM_read_bio_PrivateKey(key_file, NULL, NULL, NULL);
        if (evp_key == NULL)
        {
            LOG_ERROR("Failure PEM_read_bio_PrivateKey for %s", key_file_name);
        }
        BIO_free_all(key_file);
    }

    return evp_key;
}

static int write_private_key_file(EVP_PKEY* evp_key, const char* key_file_name)
{
    int result;

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    BIO* key_file = BIO_new_file(key_file_name, "wb");
    if (key_file == NULL)
    {
        LOG_ERROR("Failure opening key file for writing for %s", key_file_name);
        result = __FAILURE__;
    }
    else
    {
        if (!PEM_write_bio_PrivateKey(key_file, evp_key, NULL, NULL, 0, NULL, NULL))
        {
            LOG_ERROR("Unable to write private key to file %s", key_file_name);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        BIO_free_all(key_file);
    }
#else
    int fd = OPEN_HELPER(key_file_name);
    if (fd == -1)
    {
        LOG_ERROR("Failure opening key file for writing for %s. Errno %d, %s",
                  key_file_name, errno, strerror(errno));
        result = __FAILURE__;
    }
    else
    {
        BIO *key_file;
        if ((key_file = BIO_new_fd(fd, 0)) == NULL)
        {
            LOG_ERROR("Failure creating new BIO handle for %s", key_file_name);
            result = __FAILURE__;
        }
        else if (!PEM_write_bio_PrivateKey(key_file, evp_key, NULL, NULL, 0, NULL, NULL))
        {
            LOG_ERROR("Unable to write private key to file %s", key_file_name);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        BIO_free_all(key_file);
        (void)CLOSE_HELPER(fd);
    }
#endif

    return result;
}

//#################################################################################################
// PKI certificate generation
//#################################################################################################
static int cert_set_core_properties
(
    X509* x509_cert,
    KEY_HANDLE key,
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num
)
{
    (void)cert_props_handle;
    int result;

    if (!X509_set_version(x509_cert, X509_VERSION))
    {
        LOG_ERROR("Failure setting the certificate version");
        result = __FAILURE__;
    }
    else if (!ASN1_INTEGER_set(X509_get_serialNumber(x509_cert), serial_num))
    {
        LOG_ERROR("Failure setting serial number");
        result = __FAILURE__;
    }
    else if (!X509_set_pubkey(x509_cert, key))
    {
        LOG_ERROR("Failure setting public key");
        result = __FAILURE__;
    }
    else
    {
        LOG_DEBUG("Core certificate properties set");
        result = 0;
    }
    return result;
}

static int validate_subject_keyid(X509 *x509_cert)
{
    int result;

    if (X509_get_ext_by_NID(x509_cert, NID_subject_key_identifier, -1) == -1)
    {
        LOG_ERROR("X.509 V3 extension NID_subject_key_identifier does not exist");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int validate_certificate_expiration
(
    X509* x509_cert,
    double *exp_seconds_left,
    bool *is_expired
)
{
    int result;
    time_t exp_time;
    double seconds_left = 0;

    *is_expired = true;
    time_t now = time(NULL);
    ASN1_TIME *exp_asn1 = X509_get_notAfter(x509_cert);
    if ((exp_asn1->type != ASN1_TIME_STRING_UTC_FORMAT) &&
        (exp_asn1->length != ASN1_TIME_STRING_UTC_LEN))
    {
        LOG_ERROR("Unsupported time format in certificate");
        result = __FAILURE__;
    }
    else if ((exp_time = get_utc_time_from_asn_string(exp_asn1->data, exp_asn1->length)) == 0)
    {
        LOG_ERROR("Could not parse expiration date from certificate");
        result = __FAILURE__;
    }
    else
    {
        if ((seconds_left = difftime(exp_time, now)) <= 0)
        {
            LOG_ERROR("Certificate has expired");
        }
        else
        {
            *is_expired = false;
        }
        result = 0;
    }

    *exp_seconds_left = seconds_left;
    return result;
}

static int cert_set_expiration
(
    X509* x509_cert,
    uint64_t requested_validity,
    X509* issuer_cert
)
{
    int result;

    if (!X509_gmtime_adj(X509_get_notBefore(x509_cert), 0))
    {
        LOG_ERROR("Failure setting not before time");
        result = __FAILURE__;
    }
    else
    {
        // compute the MIN of seconds between:
        //    - UTC now() and the issuer certificate expiration timestamp and
        //    - Requested certificate expiration time expressed in UTC seconds from now()
        if (issuer_cert != NULL)
        {
            // determine max validity in seconds of issuer
            double exp_seconds_left_from_now = 0;
            bool is_expired = true;
            int status = validate_certificate_expiration(issuer_cert,
                                                         &exp_seconds_left_from_now,
                                                         &is_expired);
            if ((status != 0) || (is_expired))
            {
                LOG_ERROR("Issuer certificate expiration failure. Status %d, verify status: %d",
                          status, is_expired);
                result = __FAILURE__;
            }
            else
            {
                uint64_t number_seconds_left = (uint64_t)exp_seconds_left_from_now;
                LOG_DEBUG("Issuer expiration seconds left: %" PRIu64 ", Request validity:%" PRIu64,
                          number_seconds_left, requested_validity);
                requested_validity = (requested_validity == 0) ?
                                        number_seconds_left :
                                        ((requested_validity < number_seconds_left) ?
                                            requested_validity :
                                            number_seconds_left);
                result = 0;
            }
        }
        else
        {
            result = 0;
        }

        if (result == 0)
        {
            if (requested_validity == 0)
            {
                LOG_ERROR("Invalid expiration time in seconds %" PRIu64, requested_validity);
                result = __FAILURE__;
            }
            else if (!X509_gmtime_adj(X509_get_notAfter(x509_cert), (long)requested_validity))
            {
                LOG_ERROR("Failure setting not after time %" PRIu64, requested_validity);
                result = __FAILURE__;
            }
        }
    }

    return result;
}

static int set_basic_constraints(X509 *x509_cert, CERTIFICATE_TYPE cert_type, int ca_path_len)
{
    int result;
    BASIC_CONSTRAINTS *bc;

    if ((bc = BASIC_CONSTRAINTS_new()) == NULL)
    {
        LOG_ERROR("Could not allocate basic constraint");
        result = __FAILURE__;
    }
    else
    {
        bool is_pathlen_failure;
        int is_critical = 0;
        bc->ca = ASN1_DER_FALSE;
        if (cert_type == CERTIFICATE_TYPE_CA)
        {
            is_critical = 1;
            bc->ca = ASN1_DER_TRUE;
            bc->pathlen = ASN1_INTEGER_new();
            if (bc->pathlen == NULL)
            {
                LOG_ERROR("Could not path length integer");
                is_pathlen_failure = true;
            }
            else if (ASN1_INTEGER_set(bc->pathlen, ca_path_len) != 1)
            {
                LOG_ERROR("Setting path len failed");
                is_pathlen_failure = true;
            }
            else
            {
                is_pathlen_failure = false;
            }
        }
        else
        {
            is_pathlen_failure = false;
        }


        if (is_pathlen_failure)
        {
            result = __FAILURE__;
        }
        else if (X509_add1_ext_i2d(x509_cert, NID_basic_constraints,
                                   bc, is_critical, X509V3_ADD_DEFAULT) != 1)
        {
            LOG_ERROR("Could not add basic constraint extension to certificate");
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        BASIC_CONSTRAINTS_free(bc);
    }

    return result;
}

static int add_ext
(
    X509 *x509_cert,
    X509V3_CTX *ctx,
    int nid,
    const char *value,
    const char* nid_diagnostic
)
{
    int result;
    X509_EXTENSION *ex;

    // openssl API requires a non const value be passed in
    if ((ex = X509V3_EXT_conf_nid_HELPER(NULL, ctx, nid, (char*)value)) == NULL)
    {
        LOG_ERROR("Could not obtain V3 extension by NID %#x, %s", nid, nid_diagnostic);
        result = __FAILURE__;
    }
    else
    {
        if (X509_add_ext(x509_cert, ex, -1) == 0)
        {
            LOG_ERROR("Could not add V3 extension by NID %#x, %s. Value %s",
                      nid, nid_diagnostic, value);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
        X509_EXTENSION_free(ex);
    }

    return result;
}

static int set_key_usage
(
    X509 *x509_cert,
    CERTIFICATE_TYPE cert_type
)
{
    int result;
    char *usage, *ext_usage;

    if (cert_type == CERTIFICATE_TYPE_CA)
    {
        usage = "critical, digitalSignature, keyCertSign";
        ext_usage = NULL;
    }
    else if (cert_type == CERTIFICATE_TYPE_CLIENT)
    {
        usage = "critical, nonRepudiation, digitalSignature, keyEncipherment, dataEncipherment";
        ext_usage = "clientAuth";
    }
    else
    {
        usage = "critical, nonRepudiation, digitalSignature, keyEncipherment, dataEncipherment, "
                "keyAgreement";
        ext_usage = "serverAuth";
    }

    if (add_ext(x509_cert, NULL, NID_key_usage, usage, "NID_key_usage") != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        if ((ext_usage != NULL) &&
            (add_ext(x509_cert, NULL, NID_ext_key_usage, ext_usage, "NID_ext_key_usage") != 0))
        {
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int set_key_identifier_extension
(
    X509 *x509_cert,
    X509 *issuer_cert,
    int nid,
    const char *nid_diagnostic,
    char *value
)
{
    int result;
    X509V3_CTX ctx;
    X509V3_set_ctx(&ctx, issuer_cert, x509_cert, NULL, NULL, 0);

    if (add_ext(x509_cert, &ctx, nid, value, nid_diagnostic) != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int cert_set_key_id_extensions
(
    X509 *x509_cert,
    X509 *issuer_cert
)
{
    int result;

    if (set_key_identifier_extension(x509_cert, NULL, NID_subject_key_identifier,
                                     "NID_subject_key_identifier", "hash") != 0)
    {
        result = __FAILURE__;
    }
    else
    {
        char *auth_value = "issuer:always,keyid:always";
        issuer_cert = (issuer_cert != NULL) ? issuer_cert : x509_cert;
        if (set_key_identifier_extension(x509_cert, issuer_cert, NID_authority_key_identifier,
                                         "NID_authority_key_identifier", auth_value) != 0)
        {
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}

static int set_san
(
    X509 *x509_cert,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    size_t num_entries = 0, idx;
    bool fail_flag = false;
    const char * const* sans = get_san_entries(cert_props_handle, &num_entries);

    if (sans != NULL)
    {
        for (idx = 0; idx < num_entries; idx++)
        {
            if ((sans[idx] != NULL) &&
                (add_ext(x509_cert, NULL, NID_subject_alt_name,
                         sans[idx], "NID_subject_alt_name") != 0))
            {
                fail_flag = true;
                break;
            }
        }
    }

    return (fail_flag) ? __FAILURE__ : 0;
}

static int cert_set_extensions
(
    X509 *x509_cert,
    CERTIFICATE_TYPE cert_type,
    X509* issuer_cert,
    int ca_path_len,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    (void)issuer_cert;
    int result;

    if ((set_basic_constraints(x509_cert, cert_type, ca_path_len) != 0) ||
        (set_key_usage(x509_cert, cert_type) != 0) ||
        (set_san(x509_cert, cert_props_handle) != 0))
    {
        LOG_ERROR("Failure setting certificate extensions");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int cert_set_subject_field
(
    X509_NAME* name,
    X509_NAME* issuer_name,
    const char* field,
    const char* value
)
{
    static char issuer_name_field[MAX_SUBJECT_VALUE_SIZE];
    const char* value_to_set = NULL;
    int result = 0;

    if (value != NULL)
    {
        value_to_set = value;
    }
    else
    {
        if (issuer_name != NULL)
        {
            size_t index, found = 0;
            for (index = 0; index < sizeof(subj_offsets)/sizeof(subj_offsets[0]); index++)
            {
                if (strcmp(field, subj_offsets[index].field) == 0)
                {
                    found = 1;
                    break;
                }
            }
            if (found == 1)
            {
                int status;
                memset(issuer_name_field, 0 , sizeof(issuer_name_field));
                status = X509_NAME_get_text_by_NID(issuer_name,
                                                   subj_offsets[index].offset,
                                                   issuer_name_field,
                                                   sizeof(issuer_name_field));
                if (status == -1)
                {
                    LOG_DEBUG("Failure X509_NAME_get_text_by_NID for field: %s", field);
                }
                else
                {
                    value_to_set = issuer_name_field;
                    LOG_DEBUG("From issuer cert for field: %s got value: %s", field, value_to_set);
                }
            }
        }
    }

    if (value_to_set != NULL)
    {
        if (X509_NAME_add_entry_by_txt(name, field, MBSTRING_ASC,
                                       (unsigned char *)value_to_set, -1, -1, 0) != 1)
        {
            LOG_ERROR("Failure X509_NAME_add_entry_by_txt for field: %s using value: %s",
                      field, value_to_set);
            result = __FAILURE__;
        }
    }

    return result;
}

static int cert_set_subject_fields_and_issuer
(
    X509* x509_cert,
    const char *common_name,
    X509* issuer_certificate,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    int result;
    /*const*/ X509_NAME* issuer_subj_name = NULL;
    if ((issuer_certificate != NULL) &&
        ((issuer_subj_name = X509_get_subject_name(issuer_certificate)) == NULL))
    {
        LOG_ERROR("Failure obtaining issuer subject name");
        result = __FAILURE__;
    }
    else
    {
        X509_NAME* name = X509_get_subject_name(x509_cert);
        if (name == NULL)
        {
            LOG_ERROR("Failure get subject name");
            result = __FAILURE__;
        }
        else
        {
            const char *value;
            result = 0;
            value = get_country_name(cert_props_handle);
            result = cert_set_subject_field(name, issuer_subj_name, "C", value);
            if (result == 0)
            {
                value = get_state_name(cert_props_handle);
                result = cert_set_subject_field(name, issuer_subj_name, "ST", value);
            }
            if (result == 0)
            {
                value = get_locality(cert_props_handle);
                result = cert_set_subject_field(name, issuer_subj_name, "L", value);
            }
            if (result == 0)
            {
                value = get_organization_name(cert_props_handle);
                result = cert_set_subject_field(name, issuer_subj_name, "O", value);
            }
            if (result == 0)
            {
                value = get_organization_unit(cert_props_handle);
                result = cert_set_subject_field(name, issuer_subj_name, "OU", value);
            }
            if (result == 0)
            {
                //always use value provided in cert_props_handle
                result = cert_set_subject_field(name, NULL, "CN", common_name);
            }
            if (result == 0)
            {
                LOG_DEBUG("Certificate subject fields set");
                issuer_subj_name = (issuer_subj_name == NULL) ? name : issuer_subj_name;
                if (!X509_set_issuer_name(x509_cert, issuer_subj_name))
                {
                    LOG_ERROR("Failure setting issuer name");
                    result = __FAILURE__;
                }
                else
                {
                    LOG_DEBUG("Certificate issuer set successfully");
                }
            }
        }
    }
    return result;
}

static int generate_cert_key
(
    CERTIFICATE_TYPE cert_type,
    X509* issuer_certificate,
    const char *key_file_name,
    EVP_PKEY **result_evp_key,
    const PKI_KEY_PROPS *key_props
)
{
    int result;
    EVP_PKEY* evp_key;
    *result_evp_key = NULL;

    if ((evp_key = generate_evp_key(cert_type, issuer_certificate, key_props)) == NULL)
    {
        LOG_ERROR("Error generating EVP key in %s", key_file_name);
        result = __FAILURE__;
    }
    else if (write_private_key_file(evp_key, key_file_name) != 0)
    {
        LOG_ERROR("Error writing private key to file %s", key_file_name);
        result = __FAILURE__;
        destroy_evp_key(evp_key);
    }
    else
    {
        LOG_DEBUG("Generated private key at file %s", key_file_name);
        result = 0;
        *result_evp_key = evp_key;
    }

    return result;
}

static int generate_evp_certificate
(
    EVP_PKEY* evp_key,
    CERTIFICATE_TYPE cert_type,
    const char *common_name,
    uint64_t requested_validity,
    EVP_PKEY* issuer_evp_key,
    X509* issuer_certificate,
    const char *issuer_certificate_file,
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    int ca_path_len,
    const char *cert_file_name,
    X509** result_cert
)
{
    int result;
    X509* x509_cert;
    *result_cert = NULL;
    if ((x509_cert = X509_new()) == NULL)
    {
        LOG_ERROR("Failure creating the x509 cert");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
        if (cert_set_core_properties(x509_cert, evp_key, cert_props_handle, serial_num) != 0)
        {
            LOG_ERROR("Failure setting core certificate properties");
            result = __FAILURE__;
        }
        else if (cert_set_expiration(x509_cert, requested_validity, issuer_certificate) != 0)
        {
            LOG_ERROR("Failure setting certificate validity period");
            result = __FAILURE__;
        }
        else if (cert_set_extensions(x509_cert,
                                     cert_type,
                                     issuer_certificate,
                                     ca_path_len,
                                     cert_props_handle) != 0)
        {
            LOG_ERROR("Failure setting certificate extensions");
            result = __FAILURE__;
        }
        else if (cert_set_subject_fields_and_issuer(x509_cert,
                                                    common_name,
                                                    issuer_certificate,
                                                    cert_props_handle) != 0)
        {
            LOG_ERROR("Failure setting certificate subject fields");
            result = __FAILURE__;
        }
        else if (cert_set_key_id_extensions(x509_cert, issuer_certificate) != 0)
        {
            LOG_ERROR("Failure setting certificate subject auth key id extensions");
            result = __FAILURE__;
        }
        else
        {
            issuer_evp_key = (issuer_evp_key == NULL) ? evp_key : issuer_evp_key;
            if (!X509_sign(x509_cert, issuer_evp_key, EVP_sha256()))
            {
                LOG_ERROR("Failure signing x509");
                result = __FAILURE__;
            }
            else if (write_certificate_file(x509_cert, cert_file_name,
                                            issuer_certificate_file) != 0)
            {
                LOG_ERROR("Failure saving x509 certificate");
                result = __FAILURE__;
            }
            else
            {
                *result_cert = x509_cert;
            }
        }
        if (result != 0)
        {
            X509_free(x509_cert);
        }
    }

    return result;
}

static int generate_pki_cert_and_key_helper
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_number,
    int ca_path_len,
    const char* key_file_name,
    const char* cert_file_name,
    const char* issuer_key_file,
    const char* issuer_certificate_file,
    const PKI_KEY_PROPS *key_props
)
{
    int result;
    uint64_t requested_validity;
    const char* common_name_prop_value;
    X509* issuer_certificate = NULL;
    EVP_PKEY* issuer_evp_key = NULL;

    initialize_openssl();
    if (cert_props_handle == NULL)
    {
        LOG_ERROR("Failure saving x509 certificate");
        result = __FAILURE__;
    }
    else if (key_file_name == NULL)
    {
        LOG_ERROR("Invalid key file path");
        result = __FAILURE__;
    }
    else if (cert_file_name == NULL)
    {
        LOG_ERROR("Invalid key file path");
        result = __FAILURE__;
    }
    else if (((issuer_certificate_file == NULL) && (issuer_key_file != NULL)) ||
             ((issuer_certificate_file != NULL) && (issuer_key_file == NULL)))
    {
        LOG_ERROR("Invalid issuer certificate and key file provided");
        result = __FAILURE__;
    }
    else if (ca_path_len < 0)
    {
        LOG_ERROR("Invalid CA path len %d", ca_path_len);
        result = __FAILURE__;
    }
    else if ((requested_validity = get_validity_seconds(cert_props_handle)) == 0)
    {
        LOG_ERROR("Validity in seconds cannot be 0");
        result = __FAILURE__;
    }
    else if (requested_validity > LONG_MAX)
    {
        LOG_ERROR("Number of seconds too large %" PRIu64, requested_validity);
        result = __FAILURE__;
    }
    else if ((common_name_prop_value = get_common_name(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Common name value cannot be NULL");
        result = __FAILURE__;
    }
    else if (strlen(common_name_prop_value) == 0)
    {
        LOG_ERROR("Common name value cannot be empty");
        result = __FAILURE__;
    }
    else
    {
        CERTIFICATE_TYPE cert_type = get_certificate_type(cert_props_handle);
        if ((cert_type != CERTIFICATE_TYPE_CLIENT) &&
            (cert_type != CERTIFICATE_TYPE_SERVER) &&
            (cert_type != CERTIFICATE_TYPE_CA))
        {
            LOG_ERROR("Error invalid certificate type %d", cert_type);
            result = __FAILURE__;
        }
        else if ((cert_type != CERTIFICATE_TYPE_CA) && (ca_path_len != 0))
        {
            LOG_ERROR("Invalid path len argument provided for a non CA certificate request");
            result = __FAILURE__;
        }
        else
        {
            bool perform_cert_gen;
            if (issuer_certificate_file)
            {
                if ((issuer_certificate = load_certificate_file(issuer_certificate_file)) == NULL)
                {
                    LOG_ERROR("Could not load issuer certificate file");
                    perform_cert_gen = false;
                }
                else if ((issuer_evp_key = load_private_key_file(issuer_key_file)) == NULL)
                {
                    LOG_ERROR("Could not load issuer private key file");
                    perform_cert_gen = false;
                }
                else
                {
                    perform_cert_gen = true;
                }
            }
            else
            {
                perform_cert_gen = true;
            }

            if (!perform_cert_gen)
            {
                result = __FAILURE__;
            }
            else
            {
                X509* x509_cert = NULL;
                EVP_PKEY* evp_key = NULL;
                if (generate_cert_key(cert_type, issuer_certificate,
                                      key_file_name, &evp_key, key_props) != 0)
                {
                    LOG_ERROR("Could not generate private key for certificate create request");
                    result = __FAILURE__;
                }
                else if (generate_evp_certificate(evp_key, cert_type, common_name_prop_value,
                                                  requested_validity, issuer_evp_key,
                                                  issuer_certificate, issuer_certificate_file,
                                                  cert_props_handle, serial_number, ca_path_len,
                                                  cert_file_name, &x509_cert) != 0)
                {
                    LOG_ERROR("Could not generate certificate create request");
                    result = __FAILURE__;
                }
                else
                {
                    result = 0;
                }

                if (x509_cert != NULL)
                {
                    X509_free(x509_cert);
                }
                if (evp_key != NULL)
                {
                    destroy_evp_key(evp_key);
                }
            }
        }
    }

    if (issuer_certificate != NULL)
    {
        X509_free(issuer_certificate);
    }
    if (issuer_evp_key != NULL)
    {
        destroy_evp_key(issuer_evp_key);
    }

    return result;
}

int generate_pki_cert_and_key_with_props
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_number,
    int ca_path_len,
    const char* key_file_name,
    const char* cert_file_name,
    const PKI_KEY_PROPS *key_props
)
{
    int result;

    if ((key_props == NULL) ||
        ((key_props->key_type != HSM_PKI_KEY_EC) &&
         (key_props->key_type != HSM_PKI_KEY_RSA)))
    {
        LOG_ERROR("Invalid PKI key properties");
        result = __FAILURE__;
    }
    else
    {
        result = generate_pki_cert_and_key_helper(cert_props_handle,
                                                  serial_number,
                                                  ca_path_len,
                                                  key_file_name,
                                                  cert_file_name,
                                                  NULL,
                                                  NULL,
                                                  key_props);
    }

    return result;
}

int generate_pki_cert_and_key
(
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_number,
    int ca_path_len,
    const char* key_file_name,
    const char* cert_file_name,
    const char* issuer_key_file,
    const char* issuer_certificate_file
)
{
    return generate_pki_cert_and_key_helper(cert_props_handle,
                                            serial_number,
                                            ca_path_len,
                                            key_file_name,
                                            cert_file_name,
                                            issuer_key_file,
                                            issuer_certificate_file,
                                            NULL);
}

KEY_HANDLE create_cert_key(const char* key_file_name)
{
    KEY_HANDLE result;
    EVP_PKEY* evp_key;
    CERT_KEY *cert_key;

    initialize_openssl();
    if (key_file_name == NULL)
    {
        LOG_ERROR("Key file name cannot be NULL");
        result = NULL;
    }
    else if ((evp_key = load_private_key_file(key_file_name)) == NULL)
    {
        LOG_ERROR("Could not load private key file %s", key_file_name);
        result = NULL;
    }
    else if ((cert_key = (CERT_KEY*)malloc(sizeof(CERT_KEY))) == NULL)
    {
        LOG_ERROR("Could not allocate memory to create CERT_KEY");
        destroy_evp_key(evp_key);
        result = NULL;
    }
    else
    {
        cert_key->intf.hsm_client_key_sign = cert_key_sign;
        cert_key->intf.hsm_client_key_derive_and_sign = cert_key_derive_and_sign;
        cert_key->intf.hsm_client_key_encrypt = cert_key_encrypt;
        cert_key->intf.hsm_client_key_decrypt = cert_key_decrypt;
        cert_key->intf.hsm_client_key_destroy = cert_key_destroy;
        cert_key->evp_key = evp_key;
        result = (KEY_HANDLE)cert_key;
    }
    return result;
}

static int validate_cert_chain
(
    const char *cert_file,
    const char *issuer_cert_file,
    bool *verify_status
)
{
    int result;
    char *cert_data = NULL;
    char *issuer_data = NULL;

    *verify_status = false;
    if ((cert_data = read_file_into_cstring(cert_file, NULL)) == NULL)
    {
        LOG_ERROR("Could not read certificate %s", cert_file);
        result = __FAILURE__;
    }
    else if ((issuer_data = read_file_into_cstring(issuer_cert_file, NULL)) == NULL)
    {
        LOG_ERROR("Could not read issuer certificate %s", issuer_cert_file);
        result = __FAILURE__;
    }
    else
    {
        if (strstr(cert_data, issuer_data) == NULL)
        {
            LOG_ERROR("Did not find issuer certificate in certificate %s", cert_file);
        }
        else
        {
            *verify_status = true;
        }
        result = 0;
    }

    if (cert_data != NULL)
    {
        free(cert_data);
    }

    if (issuer_data != NULL)
    {
        free(issuer_data);
    }

    return result;
}

static int check_certificates
(
    X509_STORE *store,
    const char *cert_file,
    const char *issuer_cert_file,
    bool *verify_status
)
{
    int result;
    X509_STORE_CTX *store_ctxt = NULL;
    X509* x509_cert = NULL;

    if ((x509_cert = load_certificate_file(cert_file)) == NULL)
    {
        LOG_ERROR("Could not create X509 to verify certificate %s", cert_file);
        result = __FAILURE__;
    }
    else if ((store_ctxt = X509_STORE_CTX_new()) == NULL)
    {
        LOG_ERROR("Could not create X509 store context");
        X509_free(x509_cert);
        result = __FAILURE__;
    }
    else
    {
        X509_STORE_set_flags(store, X509_V_FLAG_X509_STRICT |
                                    X509_V_FLAG_CHECK_SS_SIGNATURE |
                                    X509_V_FLAG_POLICY_CHECK);
        if(!X509_STORE_CTX_init(store_ctxt, store, x509_cert, 0))
        {
            LOG_ERROR("Could not initialize X509 store context");
            result = __FAILURE__;
        }
        else
        {
            double exp_seconds = 0;
            int status;
            bool is_expired = true;

            status = validate_certificate_expiration(x509_cert, &exp_seconds, &is_expired);
            if (status != 0)
            {
                LOG_ERROR("Verifying certificate expiration failed for %s", cert_file);
                result = __FAILURE__;
            }
            else
            {
                if (is_expired)
                {
                    LOG_INFO("Certificate file has expired %s", cert_file);
                }
                else if (validate_subject_keyid(x509_cert) != 0)
                {
                    // This check was added to ensure that all certificates and in particular CA
                    // certificates contain the X509 V3 extension "Subject Key Identifier" (SKID).
                    // As part of cert hardening, we ensure that all certificates when created have
                    // the X509 V3 ext Authority Key Identifier (AKID) added. AKID requires
                    // the SKID to be present or cert generation will fail.
                    // This check essentially would fail any CA certs generated via quickstart
                    // or transparent gateway that do not have a SKID set.
                    LOG_ERROR("Certificate should contain a Subject Key Identifier extension %s",
                              cert_file);
                }
                else if ((status = X509_verify_cert(store_ctxt)) <= 0)
                {
                    const char *msg;
                    int err_code = X509_STORE_CTX_get_error(store_ctxt);
                    msg = X509_verify_cert_error_string(err_code);
                    if (msg == NULL)
                    {
                        msg = "";
                    }
                    LOG_ERROR("Could not verify certificate %s using issuer certificate %s.",
                              cert_file, issuer_cert_file);
                    LOG_ERROR("Verification status: %d, Error: %d, Msg: '%s'",
                              status, err_code, msg);
                }
                else
                {
                    LOG_DEBUG("Certificate validated %s", cert_file);
                    *verify_status = true;
                }
                result = 0;
            }
        }
        X509_STORE_CTX_free(store_ctxt);
        X509_free(x509_cert);
    }

    return result;
}

static int verify_certificate_internal
(
    const char *certificate,
    const char *issuer_certificate,
    bool *verify_status
)
{
    int result;
    X509_LOOKUP *lookup = NULL;
    X509_STORE *store = NULL;
    bool check_chain = false;

    initialize_openssl();

    if (validate_cert_chain(certificate, issuer_certificate, &check_chain) != 0)
    {
        LOG_ERROR("Failed verifying if issuer is contained in certificate file %s", certificate);
        result = __FAILURE__;
    }
    else if (!check_chain)
    {
        LOG_ERROR("Certificate file does not contain issuer certificate %s", certificate);
        result = 0;
    }
    else if ((store = X509_STORE_new()) == NULL)
    {
        LOG_ERROR("API X509_STORE_new failed");
        result = __FAILURE__;
    }
    else if ((lookup = X509_STORE_add_lookup(store, X509_LOOKUP_file())) == NULL)
    {
        LOG_ERROR("X509 add lookup failed");
        result = __FAILURE__;
    }
    else if (!X509_LOOKUP_load_file(lookup, issuer_certificate, X509_FILETYPE_PEM))
    {
        LOG_ERROR("Loading issuer certificate failed");
        result = __FAILURE__;
    }
    else if ((lookup = X509_STORE_add_lookup(store, X509_LOOKUP_hash_dir())) == NULL)
    {
        LOG_ERROR("Setting up store lookup failed");
        result = __FAILURE__;
    }
    else if (!X509_LOOKUP_add_dir(lookup, NULL, X509_FILETYPE_DEFAULT))
    {
        LOG_ERROR("Setting up store lookup failed");
        result = __FAILURE__;
    }
    else
    {
        LOG_DEBUG("Verifying %s using %s", certificate, issuer_certificate);
        result = check_certificates(store, certificate, issuer_certificate, verify_status);
    }

    if (store != NULL)
    {
        X509_STORE_free(store);
    }

    return result;
}

int verify_certificate
(
    const char *certificate_file_path,
    const char *key_file_path,
    const char *issuer_certificate_file_path,
    bool *verify_status
)
{
    int result;

    if (verify_status == NULL)
    {
        LOG_ERROR("Invalid verify_status parameter");
        result = __FAILURE__;
    }
    else
    {
        *verify_status = false;
        if ((certificate_file_path == NULL) ||
            (key_file_path == NULL) ||
            (issuer_certificate_file_path == NULL))
        {
            LOG_ERROR("Invalid parameters");
            result = __FAILURE__;
        }
        else
        {
            result = verify_certificate_internal(certificate_file_path,
                                                 issuer_certificate_file_path,
                                                 verify_status);
        }
    }

    return result;
}

int generate_rand_buffer(unsigned char *buffer, size_t num_bytes)
{
    int result;

    initialize_openssl();

    if ((buffer == NULL) || (num_bytes == 0) || (num_bytes > INT_MAX))
    {
        LOG_ERROR("Invalid parameters");
        result = __FAILURE__;
    }
    else
    {
        // note RAND_bytes will seed the random number generator
        // if not initialized once per process or unless cleaned up
        // In other works we don't have to worry about seeding for
        // the default implementation.
        // https://wiki.openssl.org/index.php/Random_Numbers
        int rc = RAND_bytes(buffer, (int)num_bytes);

        if (rc != 1)
        {
            unsigned long err = ERR_get_error();
            LOG_ERROR("Generating a random number failed. Error code %ld", err);
            result = __FAILURE__;
        }
        else
        {
            result = 0;
        }
    }

    return result;
}
