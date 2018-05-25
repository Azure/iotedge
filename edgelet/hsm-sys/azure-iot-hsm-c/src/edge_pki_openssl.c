#include <stdbool.h>
#include <openssl/asn1.h>
#include <openssl/bio.h>
#include <openssl/err.h>
#include <openssl/ec.h>
#include <openssl/pem.h>
#include <openssl/x509.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/hmacsha256.h"

#include "hsm_key.h"
#include "hsm_log.h"

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
    HSM_CLIENT_KEY_INTERFACE interface;
    EVP_PKEY* evp_key;
};
typedef struct CERT_KEY_TAG CERT_KEY;

//#################################################################################################
// PKI key operations
//#################################################################################################
static int cert_key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char** digest,
    size_t* digest_size
)
{
    LOG_ERROR("Sign for cert keys is not supported");
    return 1;
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
    LOG_ERROR("Derive and sign for cert keys is not supported");
    return 1;
}

static int cert_key_verify
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* signature_to_verify,
    size_t signature_to_verify_size,
    bool* verification_status
)
{
    LOG_ERROR("Cert key verify operation not supported");
    *verification_status = false;
    return 1;
}

static int cert_key_derive_and_verify
(
    KEY_HANDLE key_handle,
    const unsigned char* data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char* identity,
    size_t identity_size,
    const unsigned char* signature_to_verify,
    size_t signature_to_verify_size,
    bool* verification_status
)
{
    LOG_ERROR("Cert key derive and verify operation not supported");
    *verification_status = false;
    return 1;
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
    LOG_ERROR("Cert key encrypt operation not supported");
    ciphertext->buffer = NULL;
    ciphertext->size = 0;
    return 1;
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
    LOG_ERROR("Cert key decrypt operation not supported");
    plaintext->buffer = NULL;
    plaintext->size = 0;
    return 1;
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
    LOG_INFO("Generating RSA key of length %lu", key_len);
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
    else if ((status = RSA_generate_key_ex(rsa, key_len, bne, NULL)) != 1)
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
    BIO* cert_file = BIO_new_file(cert_file_name, "r");
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

static int write_certificate_file(X509* x509_cert, const char* cert_file_name)
{
    int result;
    BIO* cert_file = BIO_new_file(cert_file_name, "w");
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
        else
        {
            result = 0;
        }
        BIO_free_all(cert_file);
    }
    return result;
}

static EVP_PKEY* load_private_key_file(const char* key_file_name)
{
    EVP_PKEY* evp_key;
    BIO* key_file = BIO_new_file(key_file_name, "r");
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
    BIO* key_file = BIO_new_file(key_file_name, "w");
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
// @todo the following logic is disabled because API ASN1_TIME_diff is not
// available for libcrypto versions 1.0.0 and 1.0.1. This API is available since
// 1.0.2+. Until we can figure out a way to solve across all version this code
// is disabled.
#if 0
static int cert_set_expiration
(
    X509* x509_cert,
    X509* issuer_cert,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    int result = 0;
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
        uint64_t requested_validity = get_validity_seconds(cert_props_handle);
        if (issuer_cert != NULL)
        {
            // determine max validity in seconds of issuer
            int diff_days = 0, diff_seconds = 0;
            ASN1_TIME *issuer_expiration_asn1 = X509_get_notAfter(issuer_cert);
            if (!ASN1_TIME_diff(&diff_days, &diff_seconds, NULL, issuer_expiration_asn1))
            {
                LOG_ERROR("Invalid time format in issuer certificate");
                result = __FAILURE__;
            }
			// check if issuer certificate is expired
            else if ((diff_days < 0) || ((diff_days == 0) && (diff_seconds <= 0)))
            {
                LOG_ERROR("Issuer certificate has expired. Diff days: %d, secs %d",
                          diff_days, diff_seconds);
                result = __FAILURE__;
            }
            else
            {
                uint64_t number_seconds_left;
                number_seconds_left = (diff_days * 60 * 60 * 24) + diff_seconds;
                requested_validity = (requested_validity == 0) ?
                                        number_seconds_left :
                                        (requested_validity < number_seconds_left) ?
                                            requested_validity :
                                            number_seconds_left;
            }
        }
        if (result == 0)
        {
            if (requested_validity == 0)
            {
                LOG_ERROR("Invalid expiration time in seconds %lu", requested_validity);
                result = __FAILURE__;
            }
            else if (!X509_gmtime_adj(X509_get_notAfter(x509_cert), requested_validity))
            {
                LOG_ERROR("Failure setting not after time %lu", requested_validity);
                result = __FAILURE__;
            }
        }
    }

    return result;
}
#else
static int cert_set_expiration
(
    X509* x509_cert,
    X509* issuer_cert,
    CERT_PROPS_HANDLE cert_props_handle
)
{
    int result;
    uint64_t requested_validity = get_validity_seconds(cert_props_handle);
    if (!X509_gmtime_adj(X509_get_notBefore(x509_cert), 0))
    {
        LOG_ERROR("Failure setting not before time");
        result = __FAILURE__;
    }
    else if (!X509_gmtime_adj(X509_get_notAfter(x509_cert), requested_validity))
    {
        LOG_ERROR("Failure setting not after time %lu", requested_validity);
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}
#endif

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
    else if (issuer_name != NULL)
    {
        int index, found = 0;
        for (index = 0; index < sizeof(subj_offsets)/sizeof(subj_offsets[0]); index++)
        {
            if (strcmp(field, subj_offsets[index].field) == 0)
            {
                found = 1;
                break;
            }
        }
        if (found)
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
    if (value_to_set != NULL)
    {
        if (X509_NAME_add_entry_by_txt(name, field, MBSTRING_ASC, (unsigned char *)value_to_set, -1, -1, 0) != 1)
        {
            LOG_ERROR("Failure X509_NAME_add_entry_by_txt for field: %s using value: %s", field, value_to_set);
            result = __FAILURE__;
        }
    }

    return result;
}

static int cert_set_subject_fields_and_issuer
(
    X509* x509_cert,
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
                value = get_common_name(cert_props_handle);
                //always use value provided in cert_props_handle
                result = cert_set_subject_field(name, NULL, "CN", value);
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
                    LOG_DEBUG("Certificate issuer set", result);
                }
            }
        }
    }
    return result;
}

static int generate_cert_key
(
    CERTIFICATE_TYPE type,
    X509* issuer_certificate,
    const char *key_file_name,
    EVP_PKEY **result_evp_key,
    const PKI_KEY_PROPS *key_props
)
{
    int result;
    EVP_PKEY* evp_key;
    *result_evp_key = NULL;
    if ((type != CERTIFICATE_TYPE_CLIENT) &&
        (type != CERTIFICATE_TYPE_SERVER) &&
        (type != CERTIFICATE_TYPE_CA))
    {
        LOG_ERROR("Error invalid certificate type", type);
        result = __FAILURE__;
    }
    else if ((evp_key = generate_evp_key(type, issuer_certificate, key_props)) == NULL)
    {
        LOG_ERROR("Error opening \"%s\" for writing.", key_file_name);
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
    EVP_PKEY* issuer_evp_key,
    X509* issuer_certificate,
    CERT_PROPS_HANDLE cert_props_handle,
    int serial_num,
    const char* cert_file_name,
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
        else if (cert_set_expiration(x509_cert, issuer_certificate, cert_props_handle) != 0)
        {
            LOG_ERROR("Failure setting certificate validity period");
            result = __FAILURE__;
        }
        else if (cert_set_subject_fields_and_issuer(x509_cert,
                                                    issuer_certificate,
                                                    cert_props_handle) != 0)
        {
            LOG_ERROR("Failure setting certificate subject fields");
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
            else if (write_certificate_file(x509_cert, cert_file_name) != 0)
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
    const char* key_file_name,
    const char* cert_file_name,
    const char* issuer_key_file,
    const char* issuer_certificate_file,
    const PKI_KEY_PROPS *key_props
)
{
    int result;
    uint64_t requested_validity;
    const char* prop_value;
    X509* issuer_certificate = NULL;
    EVP_PKEY* issuer_evp_key = NULL;
    static bool is_openssl_initialized = false;

    if (!is_openssl_initialized)
    {
        OpenSSL_add_all_algorithms();
        ERR_load_BIO_strings();
        ERR_load_crypto_strings();
        is_openssl_initialized = true;
    }
    if (cert_props_handle == NULL)
    {
        LOG_ERROR("Failure saving x509 certificate");
        result = __FAILURE__;
    }
    else if ((requested_validity = get_validity_seconds(cert_props_handle)) == 0)
    {
        LOG_ERROR("Validity in seconds cannot be 0");
        result = __FAILURE__;
    }
    else if ((prop_value = get_common_name(cert_props_handle)) == NULL)
    {
        LOG_ERROR("Common name value cannot be NULL");
        result = __FAILURE__;
    }
    else if (strlen(prop_value) == 0)
    {
        LOG_ERROR("Common name value cannot be empty");
        result = __FAILURE__;
    }
    else if (((issuer_certificate_file == NULL) && (issuer_key_file != NULL)) ||
             ((issuer_certificate_file != NULL) && (issuer_key_file == NULL)))
    {
        LOG_ERROR("Invalid issuer certificate and key file provided");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
        if (issuer_certificate_file)
        {
            if ((issuer_certificate = load_certificate_file(issuer_certificate_file)) == NULL)
            {
                LOG_ERROR("Could not load issuer certificate file");
                result = __FAILURE__;
            }
            else if ((issuer_evp_key = load_private_key_file(issuer_key_file)) == NULL)
            {
                LOG_ERROR("Could not load issuer private key file");
                result = __FAILURE__;
            }
        }
        if (result == 0)
        {
            X509* x509_cert = NULL;
            EVP_PKEY* evp_key = NULL;
            CERTIFICATE_TYPE type = get_certificate_type(cert_props_handle);

            if (generate_cert_key(type, issuer_certificate, key_file_name, &evp_key, key_props) != 0)
            {
                LOG_ERROR("Could not generate private key for certificate create request");
                result = __FAILURE__;
            }
            else if (generate_evp_certificate(evp_key, issuer_evp_key, issuer_certificate,
                                              cert_props_handle, serial_number, cert_file_name,
                                              &x509_cert) != 0)
            {
                LOG_ERROR("Could not generate certificate create request");
                result = __FAILURE__;
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
    const char* key_file_name,
    const char* cert_file_name,
    const PKI_KEY_PROPS *key_props
)
{
    int result;

    if ((key_props != NULL) &&
        (key_props->key_type != HSM_PKI_KEY_EC) &&
        (key_props->key_type != HSM_PKI_KEY_RSA))
    {
        LOG_ERROR("Invalid PKI key properties");
        result = __FAILURE__;
    }
    else
    {
        result = generate_pki_cert_and_key_helper(cert_props_handle,
                                                  serial_number,
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
    const char* key_file_name,
    const char* cert_file_name,
    const char* issuer_key_file,
    const char* issuer_certificate_file
)
{
    return generate_pki_cert_and_key_helper(cert_props_handle,
                                            serial_number,
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
        LOG_ERROR("Could not allocate memory for SAS_KEY");
        destroy_evp_key(evp_key);
        result = NULL;
    }
    else
    {
        cert_key->interface.hsm_client_key_sign = cert_key_sign;
        cert_key->interface.hsm_client_key_derive_and_sign = cert_key_derive_and_sign;
        cert_key->interface.hsm_client_key_verify = cert_key_verify;
        cert_key->interface.hsm_client_key_derive_and_verify = cert_key_derive_and_verify;
        cert_key->interface.hsm_client_key_encrypt = cert_key_encrypt;
        cert_key->interface.hsm_client_key_decrypt = cert_key_decrypt;
        cert_key->evp_key = evp_key;
        result = (KEY_HANDLE)cert_key;
    }
    return result;
}

void destroy_cert_key(KEY_HANDLE key_handle)
{
    CERT_KEY *cert_key = (CERT_KEY*)key_handle;
    if (cert_key != NULL)
    {
        destroy_evp_key(cert_key->evp_key);
        free(cert_key);
    }
}
