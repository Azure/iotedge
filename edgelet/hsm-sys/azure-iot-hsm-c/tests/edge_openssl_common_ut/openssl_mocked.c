// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <openssl/err.h>
#include <openssl/evp.h>
#include <openssl/x509.h>

#include "umock_c/umock_c.h"

MOCKABLE_FUNCTION(, void, mocked_OpenSSL_add_all_algorithms);
MOCKABLE_FUNCTION(, void, mocked_ERR_load_crypto_strings);

#undef OpenSSL_add_all_algorithms
#define OpenSSL_add_all_algorithms mocked_OpenSSL_add_all_algorithms

#undef ERR_load_crypto_strings
#define ERR_load_crypto_strings mocked_ERR_load_crypto_strings

#include "../../src/edge_openssl_common.c"

