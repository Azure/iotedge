#include <stdlib.h>

#include <openssl/evp.h>
#include <openssl/rand.h>

#include "azure_c_shared_utility/gballoc.h"
#include "hsm_client_store.h"
#include "hsm_log.h"
#include "edge_openssl_common.h"

//#################################################################################################
// Data types and defines
//#################################################################################################

//   V1 ciphertext layout
//   0      1           16   OFFSET
//   +--------------------+
//   | VER |     TAG      |  HEADER
//   +--------------------+
//   |    CIPHERTEXT      |  PAYLOAD
//   |                    |
//   |       ...          |
//   +--------------------+

#define CIPHER_VERSION_SIZE 1
#define ENCRYPTION_KEY_SIZE_IN_BYTES_V1 32
#define CIPHER_TAG_SIZE_V1 16
#define CIPHER_VERSION_V1 1
#define CIPHER_HEADER_SIZE_V1 ((CIPHER_VERSION_SIZE) + (CIPHER_TAG_SIZE_V1))

struct ENC_KEY_TAG
{
    HSM_CLIENT_KEY_INTERFACE intf;
    unsigned char *key;
    size_t key_size;
};
typedef struct ENC_KEY_TAG ENC_KEY;

//#################################################################################################
// PKI key operations
//#################################################################################################
static int enc_key_sign
(
    KEY_HANDLE key_handle,
    const unsigned char *data_to_be_signed,
    size_t data_to_be_signed_size,
    unsigned char **digest,
    size_t *digest_size
)
{
    (void)key_handle;
    (void)data_to_be_signed;
    (void)data_to_be_signed_size;

    LOG_ERROR("Sign for encryption keys is not supported");
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

int enc_key_derive_and_sign
(
    KEY_HANDLE key_handle,
    const unsigned char *data_to_be_signed,
    size_t data_to_be_signed_size,
    const unsigned char *identity,
    size_t identity_size,
    unsigned char **digest,
    size_t *digest_size
)
{
    (void)key_handle;
    (void)data_to_be_signed;
    (void)data_to_be_signed_size;
    (void)identity;
    (void)identity_size;

    LOG_ERROR("Derive and sign for encryption keys is not supported");
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

static int encrypt_v1
(
    const unsigned char *plaintext,
    int plaintext_len,
    const unsigned char *aad,
	int aad_len,
    const unsigned char *key,
    const unsigned char *iv,
    int iv_len,
    unsigned char **output_buffer,
    size_t *output_size
)
{
    size_t ciphertext_size = plaintext_len + CIPHER_HEADER_SIZE_V1;
    EVP_CIPHER_CTX *ctx;
	unsigned char *ciphertext_buffer;
    int result;

    *output_size = 0;
    *output_buffer = NULL;
    if ((ciphertext_buffer = (unsigned char*)malloc(ciphertext_size)) == NULL)
    {
        LOG_ERROR("Could not allocate memory to encrypt data");
        result = __FAILURE__;
    }
	else if ((ctx = EVP_CIPHER_CTX_new()) == NULL)
    {
        LOG_ERROR("Could not create cipher context");
        result = __FAILURE__;
    }
    else
    {
        int len;
        unsigned char *version = ciphertext_buffer;
        unsigned char *tag = ciphertext_buffer + CIPHER_VERSION_SIZE;
        unsigned char *ciphertext = tag + CIPHER_TAG_SIZE_V1;

        memset(ciphertext_buffer, 0, ciphertext_size);
        *version = CIPHER_VERSION_V1;
        if (EVP_EncryptInit_ex(ctx, EVP_aes_256_gcm(), NULL, NULL, NULL) != 1)
        {
            LOG_ERROR("Could not initialize encrypt operation");
            result = __FAILURE__;
        }
        else if(EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, iv_len, NULL) != 1) // set IV length EVP_CTRL_GCM_SET_IVLEN
        {
            LOG_ERROR("Could not initialize IV length %d", iv_len);
            result = __FAILURE__;
        }
        else if(EVP_EncryptInit_ex(ctx, NULL, NULL, key, iv) != 1) // Initialise key and IV
        {
            LOG_ERROR("Could not initialize key and IV");
            result = __FAILURE__;
        }
        else if (EVP_EncryptUpdate(ctx, NULL, &len, aad, aad_len) != 1) //Provide any AAD data.
        {
            LOG_ERROR("Could not associate AAD information to encrypt operation");
            result = __FAILURE__;
        }
        else if(EVP_EncryptUpdate(ctx, ciphertext, &len, plaintext, plaintext_len) != 1) //Provide the message to be encrypted, and obtain the encrypted output.
        {
            LOG_ERROR("Could not encrypt plaintext");
            result = __FAILURE__;
        }
        else
        {
            int ciphertext_len = len;

            if (EVP_EncryptFinal_ex(ctx, ciphertext + len, &len) != 1)
            {
                LOG_ERROR("Could not encrypt plaintext");
                result = __FAILURE__;
            }
            else
            {
                ciphertext_len += len;

                if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_GET_TAG, CIPHER_TAG_SIZE_V1, tag) != 1)
                {
                    LOG_ERROR("Could not obtain tag");
                    result = __FAILURE__;
                }
                else
                {
                    *output_size = ciphertext_len + CIPHER_HEADER_SIZE_V1;
                    *output_buffer = ciphertext_buffer;
                    result = 0;
                }
            }
        }
    	EVP_CIPHER_CTX_free(ctx);
    }

    if ((result != 0) && (ciphertext_buffer != NULL))
    {
        free(ciphertext_buffer);
        ciphertext_buffer = NULL;
    }

	return result;
}

static bool validate_key_v1(unsigned char *key, size_t key_size)
{
    bool result;

    if ((key == NULL) || (key_size != ENCRYPTION_KEY_SIZE_IN_BYTES_V1))
    {
        result = false;
    }
    else
    {
        result = true;
    }

    return result;
}

static int encrypt
(
    unsigned char version,
    unsigned char *key,
    size_t key_size,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *plaintext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *ciphertext
)
{
    int result;

    initialize_openssl();
    if (version == CIPHER_VERSION_V1)
    {
        if (!validate_key_v1(key, key_size))
        {
            LOG_ERROR("Encryption key is invalid");
            result = __FAILURE__;
        }
        else if (plaintext->size > (INT_MAX - CIPHER_HEADER_SIZE_V1))
        {
            LOG_ERROR("Plaintext buffer size too large %lu", plaintext->size);
            result = __FAILURE__;
        }
        else
        {
            // default encryption implementation
            result = encrypt_v1(plaintext->buffer,
                                (int)plaintext->size,
                                identity->buffer,
                                (int)identity->size,
                                key,
                                initialization_vector->buffer,
                                (int)initialization_vector->size,
                                &ciphertext->buffer,
                                &ciphertext->size);
        }
    }
    else
    {
        LOG_ERROR("Unknown version %d", version);
        result = __FAILURE__;
    }

    return result;
}

static int decrypt_v1
(
    const unsigned char *ciphertext_buffer,
    int ciphertext_buffer_size,
    const unsigned char *aad,
	int aad_len,
    const unsigned char *key,
    const unsigned char *iv,
    int iv_len,
    unsigned char **output_buffer,
    size_t *output_size
)
{
	unsigned char *plaintext_buffer;
    int result;
    EVP_CIPHER_CTX *ctx;
    size_t plaintext_buffer_size = ciphertext_buffer_size;

    *output_size = 0;
    *output_buffer = NULL;
    if ((plaintext_buffer = (unsigned char*)malloc(plaintext_buffer_size)) == NULL)
    {
        LOG_ERROR("Could not allocate memory to decrypt data");
        result = __FAILURE__;
    }
	else if ((ctx = EVP_CIPHER_CTX_new()) == NULL)
    {
        LOG_ERROR("Could not create cipher context");
        result = __FAILURE__;
    }
    else
    {
        int len;
        unsigned char tag[CIPHER_TAG_SIZE_V1];
        const unsigned char *tag_start = ciphertext_buffer + CIPHER_VERSION_SIZE;
        const unsigned char *ciphertext = ciphertext_buffer + CIPHER_HEADER_SIZE_V1;
        int ciphertext_len = ciphertext_buffer_size - CIPHER_HEADER_SIZE_V1;

        memset(plaintext_buffer, 0, plaintext_buffer_size);
        memcpy(tag, tag_start, CIPHER_TAG_SIZE_V1);
        if (EVP_DecryptInit_ex(ctx, EVP_aes_256_gcm(), NULL, NULL, NULL) != 1)
        {
            LOG_ERROR("Could not initialize decrypt operation");
            result = __FAILURE__;
        }
        else if(EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_IVLEN, iv_len, NULL) != 1) // set IV length EVP_CTRL_GCM_SET_IVLEN
        {
            LOG_ERROR("Could not initialize IV length %d", iv_len);
            result = __FAILURE__;
        }
        else if(EVP_DecryptInit_ex(ctx, NULL, NULL, key, iv) != 1) // Initialise key and IV
        {
            LOG_ERROR("Could not initialize key and IV");
            result = __FAILURE__;
        }
        else if (EVP_DecryptUpdate(ctx, NULL, &len, aad, aad_len) != 1) //Provide any AAD data.
        {
            LOG_ERROR("Could not associate AAD information to decrypt operation");
            result = __FAILURE__;
        }
        else if(EVP_DecryptUpdate(ctx, plaintext_buffer, &len, ciphertext, ciphertext_len) != 1) //Provide the message to be encrypted, and obtain the encrypted output.
        {
            LOG_ERROR("Could not decrypt ciphertext");
            result = __FAILURE__;
        }
        else
        {
            int plaintext_len = len;
	        if (EVP_CIPHER_CTX_ctrl(ctx, EVP_CTRL_GCM_SET_TAG, CIPHER_TAG_SIZE_V1, tag) != 1)
            {
                LOG_ERROR("Could not set verification tag");
                result = __FAILURE__;
            }
            else
            {
                if (EVP_DecryptFinal_ex(ctx, plaintext_buffer + len, &len) <= 0)
                {
                    LOG_ERROR("Verification of plain text failed. Plain text is not trustworthy.");
                    result = __FAILURE__;
                }
                else
                {
                    plaintext_len += len;
                    *output_size = plaintext_len;
                    *output_buffer = plaintext_buffer;
                    result = 0;
                }
            }
        }
    	EVP_CIPHER_CTX_free(ctx);
    }

    if ((result != 0) && (plaintext_buffer != NULL))
    {
        free(plaintext_buffer);
        plaintext_buffer = NULL;
    }

	return result;
}

static int decrypt
(
    unsigned char version,
    unsigned char *key,
    size_t key_size,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *ciphertext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *plaintext
)
{
    int result;
    initialize_openssl();
    if (version == CIPHER_VERSION_V1)
    {
        if (!validate_key_v1(key, key_size))
        {
            LOG_ERROR("Encryption key is invalid");
            result = __FAILURE__;
        }
        else if (ciphertext->size <= CIPHER_HEADER_SIZE_V1)
        {
            LOG_ERROR("Ciphertext buffer incorrect size %lu", ciphertext->size);
            result = __FAILURE__;
        }
        else
        {
            // default decrypt version
            result = decrypt_v1(ciphertext->buffer,
                                (int)ciphertext->size,
                                identity->buffer,
                                (int)identity->size,
                                key,
                                initialization_vector->buffer,
                                (int)initialization_vector->size,
                                &plaintext->buffer,
                                &plaintext->size);
        }
    }
    else
    {
        LOG_ERROR("Unknown version %d", version);
        result = __FAILURE__;
    }

    return result;
}

static bool validate_input_param_buffer(const SIZED_BUFFER *sb, const char *name)
{
    bool result = true;

    if ((sb == NULL) || (sb->buffer == NULL))
    {
        LOG_ERROR("Invalid buffer for %s", name);
        result = false;
    }
    else if ((sb->size == 0) || (sb->size > INT_MAX))
    {
        LOG_ERROR("Parameter %s has invalid size %lu", name, sb->size);
        result = false;
    }

    return result;
}

static int enc_key_encrypt
(
    KEY_HANDLE key_handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *plaintext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *ciphertext
)
{
    int result;

    if (ciphertext == NULL)
    {
        LOG_ERROR("Input ciphertext buffer is invalid");
        result = __FAILURE__;
    }
    else
    {
        ciphertext->buffer = NULL;
        ciphertext->size = 0;
        if ((!validate_input_param_buffer(plaintext, "plaintext")) ||
            (!validate_input_param_buffer(identity, "identity")) ||
            (!validate_input_param_buffer(initialization_vector, "initialization_vector")))
        {
            LOG_ERROR("Input data is invalid");
            result = __FAILURE__;
        }
        else
        {
            ENC_KEY *enc_key = (ENC_KEY*)key_handle;
            // default encryption impl version 1
            result = encrypt(CIPHER_VERSION_V1,
                             enc_key->key,
                             enc_key->key_size,
                             identity,
                             plaintext,
                             initialization_vector,
                             ciphertext);
        }
    }

    return result;
}

static bool validate_input_ciphertext_buffer(const SIZED_BUFFER *sb, unsigned char *version)
{
    bool result;

    if ((sb == NULL) || (sb->buffer == NULL))
    {
        LOG_ERROR("Invalid ciphertext buffer");
        result = false;
    }
    else if ((sb->size == 0) || (sb->size > INT_MAX))
    {
        LOG_ERROR("Ciphertext has invalid size %lu", sb->size);
        result = false;
    }
    else if (sb->buffer[0] != CIPHER_VERSION_V1)
    {
        LOG_ERROR("Unsupported encryption version %c", sb->buffer[0]);
        result = false;
    }
    else
    {
        *version = sb->buffer[0];
        result = true;
    }

    return result;
}

static int enc_key_decrypt
(
    KEY_HANDLE key_handle,
    const SIZED_BUFFER *identity,
    const SIZED_BUFFER *ciphertext,
    const SIZED_BUFFER *initialization_vector,
    SIZED_BUFFER *plaintext
)
{
    int result;

    if (plaintext == NULL)
    {
        LOG_ERROR("Input plaintext buffer is invalid");
        result = __FAILURE__;
    }
    else
    {
        unsigned char version = 0;
        plaintext->buffer = NULL;
        plaintext->size = 0;
        if ((!validate_input_ciphertext_buffer(ciphertext, &version)) ||
            (!validate_input_param_buffer(identity, "identity")) ||
            (!validate_input_param_buffer(initialization_vector, "initialization_vector")))
        {
            LOG_ERROR("Input data is invalid");
            result = __FAILURE__;
        }
        else
        {
            ENC_KEY *enc_key = (ENC_KEY*)key_handle;
            result = decrypt(version,
                             enc_key->key,
                             enc_key->key_size,
                             identity,
                             ciphertext,
                             initialization_vector,
                             plaintext);
        }
    }

    return result;
}

static void enc_key_destroy(KEY_HANDLE key_handle)
{
    ENC_KEY *enc_key = (ENC_KEY*)key_handle;

    if (enc_key != NULL)
    {
        if (enc_key->key != NULL)
        {
            free(enc_key->key);
        }
        free(enc_key);
    }
}

KEY_HANDLE create_encryption_key(const unsigned char *key, size_t key_size)
{
    ENC_KEY* enc_key;

    if ((key == NULL) || (key_size != ENCRYPTION_KEY_SIZE_IN_BYTES_V1))
    {
        LOG_ERROR("Invalid encryption key create parameters");
        enc_key = NULL;
    }
    else
    {
        enc_key = (ENC_KEY*)malloc(sizeof(ENC_KEY));
        if (enc_key == NULL)
        {
            LOG_ERROR("Could not allocate memory for ENC_KEY");
        }
        else if ((enc_key->key = (unsigned char*)malloc(key_size)) == NULL)
        {
            LOG_ERROR("Could not allocate memory for encryption key creation");
            free(enc_key);
            enc_key = NULL;
        }
        else
        {
            enc_key->intf.hsm_client_key_sign = enc_key_sign;
            enc_key->intf.hsm_client_key_derive_and_sign = enc_key_derive_and_sign;
            enc_key->intf.hsm_client_key_encrypt = enc_key_encrypt;
            enc_key->intf.hsm_client_key_decrypt = enc_key_decrypt;
            enc_key->intf.hsm_client_key_destroy = enc_key_destroy;
            memcpy(enc_key->key, key, key_size);
            enc_key->key_size = key_size;
        }
    }

    return (KEY_HANDLE)enc_key;
}

int generate_encryption_key(unsigned char **key, size_t *key_size)
{
    int result = 0;

    initialize_openssl();

    if (key == NULL)
    {
        LOG_ERROR("Invalid parameter key");
        result = __FAILURE__;
    }
    else
    {
        *key = NULL;
    }

    if (key_size == NULL)
    {
        LOG_ERROR("Invalid parameter key size");
        result = __FAILURE__;
    }
    else
    {
        *key_size = 0;
    }

    if (result == 0)
    {
        unsigned char *random_bytes;

        if ((random_bytes = (unsigned char*)malloc(ENCRYPTION_KEY_SIZE_IN_BYTES_V1)) == NULL)
        {
            LOG_ERROR("Could not allocate memory to hold key");
            result = __FAILURE__;
        }
        else if (RAND_bytes(random_bytes, ENCRYPTION_KEY_SIZE_IN_BYTES_V1) != 1)
        {
            LOG_ERROR("Could not generate random bytes for key");
            free(random_bytes);
            random_bytes = NULL;
            result = __FAILURE__;
        }
        else
        {
            *key = random_bytes;
            *key_size = ENCRYPTION_KEY_SIZE_IN_BYTES_V1;
        }
    }

    return result;
}
