// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <openssl/x509.h>
#include <openssl/pem.h>

#include "azure_c_shared_utility/gballoc.h"
#include "umock_c/umock_c.h"

MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notBefore, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notAfter, X509*, x509_cert);

#undef X509_get_notBefore
#undef X509_get_notAfter
#define X509_get_notBefore mocked_X509_get_notBefore
#define X509_get_notAfter  mocked_X509_get_notAfter

#include "../../src/certificate_info.c"
