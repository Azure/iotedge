#include <stdbool.h>
#include <openssl/pem.h>
#include <openssl/x509.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/hmacsha256.h"

#include "hsm_key.h"
#include "hsm_log.h"

struct CERT_KEY_TAG
{
    HSM_CLIENT_KEY_INTERFACE interface;
    EVP_PKEY* evp_key;
};
typedef struct CERT_KEY_TAG CERT_KEY;

#define RSA_KEY_LEN_CA 4096
#define RSA_KEY_LEN_NON_CA RSA_KEY_LEN_CA >> 1

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

static EVP_PKEY* generate_rsa_key(size_t key_len)
{
    int status;
    BIGNUM *bne;
    EVP_PKEY *pkey;
    RSA *rsa;

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

static void destroy_rsa_key(EVP_PKEY *pkey)
{
    if (pkey != NULL)
    {
        EVP_PKEY_free(pkey);
    }
}

static int cert_key_verify(KEY_HANDLE key_handle,
                           const unsigned char* data_to_be_signed,
                           size_t data_to_be_signed_size,
                           const unsigned char* signature_to_verify,
                           size_t signature_to_verify_size,
                           bool* verification_status)
{
    LOG_ERROR("Cert key verify operation not supported");
    *verification_status = false;
    return 1;
}

static int cert_key_derive_and_verify(KEY_HANDLE key_handle,
                                      const unsigned char* data_to_be_signed,
                                      size_t data_to_be_signed_size,
                                      const unsigned char* identity,
                                      size_t identity_size,
                                      const unsigned char* signature_to_verify,
                                      size_t signature_to_verify_size,
                                      bool* verification_status)
{
    LOG_ERROR("Cert key derive and verify operation not supported");
    *verification_status = false;
    return 1;
}

static int cert_key_encrypt(KEY_HANDLE key_handle,
                            const SIZED_BUFFER *identity,
                            const SIZED_BUFFER *plaintext,
                            const SIZED_BUFFER *passphrase,
                            const SIZED_BUFFER *initialization_vector,
                            SIZED_BUFFER *ciphertext)
{
    LOG_ERROR("Cert key encrypt operation not supported");
    ciphertext->buffer = NULL;
    ciphertext->size = 0;
    return 1;
}

static int cert_key_decrypt(KEY_HANDLE key_handle,
                            const SIZED_BUFFER *identity,
                            const SIZED_BUFFER *ciphertext,
                            const SIZED_BUFFER *passphrase,
                            const SIZED_BUFFER *initialization_vector,
                            SIZED_BUFFER *plaintext)
{
    LOG_ERROR("Cert key decrypt operation not supported");
    plaintext->buffer = NULL;
    plaintext->size = 0;
    return 1;
}

int generate_cert_key(CERTIFICATE_TYPE type, const char* key_file_name)
{
    int result;
    EVP_PKEY* evp_key;
    if ((type != CERTIFICATE_TYPE_CLIENT) &&
        (type != CERTIFICATE_TYPE_SERVER) &&
        (type != CERTIFICATE_TYPE_CA))
    {
        LOG_ERROR("Error invalid certificate type", type);
        result = 1;
    }
    else
    {
        size_t rsa_key_len = (type == CERTIFICATE_TYPE_CA) ? RSA_KEY_LEN_CA : RSA_KEY_LEN_NON_CA;
        if ((evp_key = generate_rsa_key(rsa_key_len)) == NULL)
        {
            LOG_ERROR("Error openining \"%s\" for writing.", key_file_name);
            result = 1;
        }
        else
        {
            BIO* key_file;
            if ((key_file = BIO_new_file(key_file_name, "w")) == NULL)
            {
                LOG_ERROR("Error openining \"%s\" for writing.", key_file_name);
                result = 1;
            }
            else
            {
                if (!PEM_write_bio_PrivateKey(key_file, evp_key, NULL, NULL, 0, NULL, NULL))
                {
                    LOG_ERROR("Failure PEM_write_bio_PrivateKey\r\n");
                    result = 1;
                }
                else
                {
                    result = 0;
                }
                BIO_free_all(key_file);
            }
            destroy_rsa_key(evp_key);
        }
    }
    return result;
}

KEY_HANDLE create_cert_key(const char* key_file_name)
{
    KEY_HANDLE result;
    BIO* key_file;

    if ((key_file = BIO_new_file(key_file_name, "r")) == NULL)
    {
        LOG_ERROR("BIO_new_file returned NULL");
        result = NULL;
    }
    else
    {
        CERT_KEY *cert_key;
        EVP_PKEY* evp_key = PEM_read_bio_PrivateKey(key_file, NULL, NULL, NULL);
        if (evp_key == NULL)
        {
            LOG_ERROR("Failed to load private key using PEM_read_bio_PrivateKey");
            result = NULL;
        }
        else if ((cert_key = (CERT_KEY*)malloc(sizeof(CERT_KEY))) == NULL)
        {
            LOG_ERROR("Could not allocate memory for SAS_KEY");
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
        BIO_free_all(key_file);
    }
    return result;
}

void destroy_cert_key(KEY_HANDLE key_handle)
{
    CERT_KEY *cert_key = (CERT_KEY*)key_handle;
    if (cert_key != NULL)
    {
        destroy_rsa_key(cert_key->evp_key);
        free(cert_key);
    }
}
