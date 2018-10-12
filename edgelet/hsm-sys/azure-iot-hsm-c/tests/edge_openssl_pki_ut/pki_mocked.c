// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <sys/stat.h>
#include <fcntl.h>
#include <sys/types.h>
#include <openssl/x509.h>

#include "azure_c_shared_utility/gballoc.h"
#include "umock_c.h"

#define ASN1_TIME_STRING_UTC_FORMAT 0x17
#define ASN1_TIME_STRING_UTC_LEN    13

#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    #include <io.h>
    typedef int MODE_T;
#else
    #include <unistd.h>
    typedef mode_t MODE_T;
#endif

MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notBefore, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notAfter, X509*, x509_cert);
MOCKABLE_FUNCTION(, int, mocked_OPEN, const char*, path, int, flags, MODE_T, mode);
MOCKABLE_FUNCTION(, int, mocked_CLOSE, int, fd);

struct lhash_st_CONF_VALUE;
MOCKABLE_FUNCTION(, X509_EXTENSION*, mocked_X509V3_EXT_conf_nid, struct lhash_st_CONF_VALUE*, conf, X509V3_CTX*, ctx, int, ext_nid, char*, value);

#define X509V3_EXT_conf_nid_HELPER(conf, ctx, nid, value) mocked_X509V3_EXT_conf_nid((conf), (ctx), (nid), (value))

#undef X509_get_notBefore
#undef X509_get_notAfter
#define X509_get_notBefore mocked_X509_get_notBefore
#define X509_get_notAfter  mocked_X509_get_notAfter

#undef CLOSE_HELPER
#if defined __WINDOWS__ || defined _WIN32 || defined _WIN64 || defined _Windows
    #define OPEN_HELPER(fname) mocked_OPEN((fname), _O_CREAT|_O_WRONLY|_O_TRUNC, _S_IREAD|_S_IWRITE)
#else
    #define OPEN_HELPER(fname) mocked_OPEN((fname), O_CREAT|O_WRONLY|O_TRUNC, S_IRUSR|S_IWUSR)
#endif

#define CLOSE_HELPER(fd) mocked_CLOSE(fd)

#include "../../src/edge_pki_openssl.c"
