#include <openssl/err.h>
#include <openssl/x509.h>
#include "azure_c_shared_utility/gballoc.h"
#include "edge_openssl_common.h"

void initialize_openssl(void)
{
    static bool is_openssl_initialized = false;

    if (!is_openssl_initialized)
    {
        OpenSSL_add_all_algorithms();
        ERR_load_BIO_strings();
        ERR_load_crypto_strings();
        is_openssl_initialized = true;
    }
}
