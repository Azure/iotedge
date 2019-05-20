// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifdef __cplusplus
#include <cstdlib>
#include <cstdint>
#include <cstddef>
#include <ctime>
#else
#include <stdlib.h>
#include <stdint.h>
#include <stddef.h>
#include <time.h>
#endif

static void* my_gballoc_malloc(size_t size)
{
    return malloc(size);
}

static void my_gballoc_free(void* ptr)
{
    free(ptr);
}

static void* my_gballoc_realloc(void* ptr, size_t size)
{
    return realloc(ptr, size);
}

#include "testrunnerswitcher.h"
#include "umock_c.h"
#include "umocktypes_charptr.h"
#include "umocktypes_stdint.h"
#include "umock_c_negative_tests.h"
#include "azure_c_shared_utility/macro_utils.h"

#include <openssl/x509.h>
#include <openssl/pem.h>

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/umock_c_prod.h"

MOCKABLE_FUNCTION(, int, BIO_write, BIO*, b, const void*, in, int, inl);
MOCKABLE_FUNCTION(, X509*, PEM_read_bio_X509, BIO*, bp, X509**, x, pem_password_cb*, cb, void*, u);
MOCKABLE_FUNCTION(, void, BIO_free_all, BIO*, bio);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notBefore, X509*, x509_cert);
MOCKABLE_FUNCTION(, ASN1_TIME*, mocked_X509_get_notAfter, X509*, x509_cert);
//https://www.openssl.org/docs/man1.1.0/crypto/OPENSSL_VERSION_NUMBER.html
// this checks if openssl version major minor is greater than or equal to version # 1.1.0
#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, const X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, const X509*, x, int, nid, int, lastpos);
    MOCKABLE_FUNCTION(, const BIO_METHOD*, BIO_s_mem);
    MOCKABLE_FUNCTION(, BIO*, BIO_new, const BIO_METHOD*, type);
#else
    MOCKABLE_FUNCTION(, X509_NAME*, X509_get_subject_name, X509*, a);
    MOCKABLE_FUNCTION(, int, X509_get_ext_by_NID, X509*, x, int, nid, int, lastpos);
    MOCKABLE_FUNCTION(, BIO_METHOD*, BIO_s_mem);
    MOCKABLE_FUNCTION(, BIO*, BIO_new, BIO_METHOD*, type);
#endif
MOCKABLE_FUNCTION(, int, X509_NAME_get_text_by_NID, X509_NAME*, name, int, nid, char*, buf, int, len);
MOCKABLE_FUNCTION(, void, X509_free, X509*, a);

#undef ENABLE_MOCKS

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "certificate_info.h"

#ifdef __cplusplus
extern "C" {
#endif

extern time_t get_utc_time_from_asn_string(const unsigned char *time_value, size_t length);

#ifdef __cplusplus
}
#endif

//#############################################################################
// Test defines and data
//#############################################################################
static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

#define MAX_FAILED_FUNCTION_LIST_SIZE 64
#define TEST_BIO (BIO*)0x1000
#define TEST_BIO_METHOD (BIO_METHOD*)0x1001
#define TEST_X509 (X509*)0x1002
#define TEST_X509_SUBJECT_NAME (X509_NAME*)0x1003
#define TEST_COMMON_NAME "TEST_CN"
#define VALID_ASN1_TIME_STRING_UTC_FORMAT   0x17
#define VALID_ASN1_TIME_STRING_UTC_LEN      13
#define INVALID_ASN1_TIME_STRING_UTC_FORMAT 0
#define INVALID_ASN1_TIME_STRING_UTC_LEN    0
#define MAX_COMMON_NAME_SIZE 65

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


static const int64_t RSA_CERT_VALID_FROM_TIME = 1484940333;
static const int64_t RSA_CERT_VALID_TO_TIME = 1800300333;

static const char* TEST_RSA_CERT_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
"MIICpDCCAYwCCQCgAJQdOd6dNzANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwHhcNMTcwMTIwMTkyNTMzWhcNMjcwMTE4MTkyNTMzWjAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDlJ3fRNWm05BRAhgUY7cpzaxHZIORomZaOp2Uua5yv+psdkpv35ExLhKGrUIK1AJLZylnue0ohZfKPFTnoxMHOecnaaXZ9RA25M7XGQvw85ePlGOZKKf3zXw3Ds58GFY6Sr1SqtDopcDuMmDSg/afYVvGHDjb2Fc4hZFip350AADcmjH5SfWuxgptCY2Jl6ImJoOpxt+imWsJCJEmwZaXw+eZBb87e/9PH4DMXjIUFZebShowAfTh/sinfwRkaLVQ7uJI82Ka/icm6Hmr56j7U81gDaF0DhC03ds5lhN7nMp5aqaKeEJiSGdiyyHAescfxLO/SMunNc/eG7iAirY7BAgMBAAEwDQYJKoZIhvcNAQELBQADggEBACU7TRogb8sEbv+SGzxKSgWKKbw+FNgC4Zi6Fz59t+4jORZkoZ8W87NM946wvkIpxbLKuc4F+7nTGHHksyHIiGC3qPpi4vWpqVeNAP+kfQptFoWEOzxD7jQTWIcqYhvssKZGwDk06c/WtvVnhZOZW+zzJKXA7mbwJrfp8VekOnN5zPwrOCumDiRX7BnEtMjqFDgdMgs9ohR5aFsI7tsqp+dToLKaZqBLTvYwCgCJCxdg3QvMhVD8OxcEIFJtDEwm3h9WFFO3ocabCmcMDyXUL354yaZ7RphCBLd06XXdaUU/eV6fOjY6T5ka4ZRJcYDJtjxSG04XPtxswQfrPGGoFhk=""\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_RSA_CERT_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
"MIICpDCCAYwCCQCgAJQdOd6dNzANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwHhcNMTcwMTIwMTkyNTMzWhcNMjcwMTE4MTkyNTMzWjAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDlJ3fRNWm05BRAhgUY7cpzaxHZIORomZaOp2Uua5yv+psdkpv35ExLhKGrUIK1AJLZylnue0ohZfKPFTnoxMHOecnaaXZ9RA25M7XGQvw85ePlGOZKKf3zXw3Ds58GFY6Sr1SqtDopcDuMmDSg/afYVvGHDjb2Fc4hZFip350AADcmjH5SfWuxgptCY2Jl6ImJoOpxt+imWsJCJEmwZaXw+eZBb87e/9PH4DMXjIUFZebShowAfTh/sinfwRkaLVQ7uJI82Ka/icm6Hmr56j7U81gDaF0DhC03ds5lhN7nMp5aqaKeEJiSGdiyyHAescfxLO/SMunNc/eG7iAirY7BAgMBAAEwDQYJKoZIhvcNAQELBQADggEBACU7TRogb8sEbv+SGzxKSgWKKbw+FNgC4Zi6Fz59t+4jORZkoZ8W87NM946wvkIpxbLKuc4F+7nTGHHksyHIiGC3qPpi4vWpqVeNAP+kfQptFoWEOzxD7jQTWIcqYhvssKZGwDk06c/WtvVnhZOZW+zzJKXA7mbwJrfp8VekOnN5zPwrOCumDiRX7BnEtMjqFDgdMgs9ohR5aFsI7tsqp+dToLKaZqBLTvYwCgCJCxdg3QvMhVD8OxcEIFJtDEwm3h9WFFO3ocabCmcMDyXUL354yaZ7RphCBLd06XXdaUU/eV6fOjY6T5ka4ZRJcYDJtjxSG04XPtxswQfrPGGoFhk=""\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_ECC_CERT_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
"MIIBfTCCASSgAwIBAgIFGis8TV4wCgYIKoZIzj0EAwIwNDESMBAGA1UEAwwJcmlvdC1yb290MQswCQYDVQQGDAJVUzERMA8GA1UECgwITVNSX1RFU1QwHhcNMTcwMTAxMDAwMDAwWhcNMzcwMTAxMDAwMDAwWjA0MRIwEAYDVQQDDAlyaW90LXJvb3QxCzAJBgNVBAYMAlVTMREwDwYDVQQKDAhNU1JfVEVTVDBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABGmrWiahUg/J7F2llfSXSLn+0j0JxZ0fp1DTlEnI/Jzr3x5bsP2eRppj0jflBPvU+qJwT7EFnq2a1Tz4OWKxzn2jIzAhMAsGA1UdDwQEAwIABDASBgNVHRMBAf8ECDAGAQH/AgEBMAoGCCqGSM49BAMCA0cAMEQCIFFcPW6545a5BNP+yn9U/c0MwemXvzddylFa0KbDtANfAiB0rxBRLP1e7vZtzjJsLP6njjO6qWoArXRuTV2nDO3S9g==""\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_ECC_CERT_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
"MIIBfTCCASSgAwIBAgIFGis8TV4wCgYIKoZIzj0EAwIwNDESMBAGA1UEAwwJcmlvdC1yb290MQswCQYDVQQGDAJVUzERMA8GA1UECgwITVNSX1RFU1QwHhcNMTcwMTAxMDAwMDAwWhcNMzcwMTAxMDAwMDAwWjA0MRIwEAYDVQQDDAlyaW90LXJvb3QxCzAJBgNVBAYMAlVTMREwDwYDVQQKDAhNU1JfVEVTVDBZMBMGByqGSM49AgEGCCqGSM49AwEHA0IABGmrWiahUg/J7F2llfSXSLn+0j0JxZ0fp1DTlEnI/Jzr3x5bsP2eRppj0jflBPvU+qJwT7EFnq2a1Tz4OWKxzn2jIzAhMAsGA1UdDwQEAwIABDASBgNVHRMBAf8ECDAGAQH/AgEBMAoGCCqGSM49BAMCA0cAMEQCIFFcPW6545a5BNP+yn9U/c0MwemXvzddylFa0KbDtANfAiB0rxBRLP1e7vZtzjJsLP6njjO6qWoArXRuTV2nDO3S9g==""\n"
"-----END CERTIFICATE-----\n";

static const char* TEST_INVALID_CERT_WIN_EOL =
"-----BEGIN CERTIFICATE REQUEST-----""\r\n"
"MIIBIjCByAIBADBmMQswCQYDVQQGEwJVUzELMAkGA1UECAwCV0ExEDAOBgNVBAcMB1JlZG1vbmQxITAfBgNVBAoMGEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDEVMBMGA1UEAwwMUHJvdl9yZXF1ZXN0MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdgUgbY2fVlM1Xr6P6B/E+yfT539BCzd4jBuoIyUYncnO5K0Qxyz8zC/V7z+iGQzB7jF799pkJoLtVPUhXoaLjqAAMAoGCCqGSM49BAMCA0kAMEYCIQCVfcLe+lNdUZtGxe4ZcxNcmQylnFRH9/ZCbyWWruROiAIhAK2OF66q5mFzCtZ8OE7KgffB3cBUCf/xZdUda9dH9Onp""\r\n"
"-----END CERTIFICATE REQUEST-----\r\n";

static const char* TEST_INVALID_CERT_NIX_EOL =
"-----BEGIN CERTIFICATE REQUEST-----""\n"
"MIIBIjCByAIBADBmMQswCQYDVQQGEwJVUzELMAkGA1UECAwCV0ExEDAOBgNVBAcMB1JlZG1vbmQxITAfBgNVBAoMGEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDEVMBMGA1UEAwwMUHJvdl9yZXF1ZXN0MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdgUgbY2fVlM1Xr6P6B/E+yfT539BCzd4jBuoIyUYncnO5K0Qxyz8zC/V7z+iGQzB7jF799pkJoLtVPUhXoaLjqAAMAoGCCqGSM49BAMCA0kAMEYCIQCVfcLe+lNdUZtGxe4ZcxNcmQylnFRH9/ZCbyWWruROiAIhAK2OF66q5mFzCtZ8OE7KgffB3cBUCf/xZdUda9dH9Onp""\n"
"-----END CERTIFICATE REQUEST-----\n";

static const char* TEST_CERT_CHAIN_WIN_EOL =
"-----BEGIN CERTIFICATE-----""\r\n"
"MIIFvTCCA6WgAwIBAgICA+kwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGUMQswCQYDVQQGEwJVUzEWMBQGA1UEAwwNRWRnZSBBZ2VudCBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOr+S7kLLzqhhw1U6O7xGc6tf82EjvUVBZdXR8t61j8G3JwgtyfDdGk0M8pcG3hOmfZwAbHqEUZ8i78uJchvYzilJQcINxUuwS1bl7MWiFkThsql/XLyYtCSnKrhqRVPi2hxwbN4v39HmazEmUXazFSgF13E+Si2/lfJ86JHfnnQCMZmDP02EtcPc1Rw3LmS7pg3h2mRv769Vj11Wtsr7nNTssbGc3yhAhXdt3sMWQihr4yBnhk931uyQiQgeQ69eK5L8g3KjRsOFvMJEXAsIk/lmuYquTaUZfaanuzRezzNVDMwZF9oiVXXClutGj/MlRYl+23gFChx+QAmFg1T7oCb2a1FdXIM5koycRtWKRAbBh+q20Asn6DcEhZE+yyiMQYaiPvUENpPKi5zX5q7lxzIhHb/LrQH9yzVxYGb4bj1t64SnOscwiDc02zRNrInqud8vkVITu/HUskaZWVX1ArwMPyurdNBgKM+ZocWN7niw1txzISjZIyYooYmKmFh6rc3D0TSLlno2WVvTcaxmnw4q9CNIRIH/9uH7mlDxprg4TRBHGx9Bvrh1YJpllTBHv6nhI78r5YTr0ofZ1fr3mHIRcxMdFTVwRtVbKCRmU30broaCOlNJewtTZB27nQnjJBu7CbOKWlyADJlvc5tD8EYiH8HP162XCqKYg4zwDkNAgMBAAGjFjAUMBIGA1UdEwEB/wQIMAYBAf8CAQAwDQYJKoZIhvcNAQELBQADggIBAHR9AU3JtlJdeWB1cndjZRKJ+YCMHThGKvV9AbVuPUogCnkVRuz2JBh34xeulT90Ihh8LvXA6qE8swQc39+lxXijHAOKKVPgXKF4Z4EEztyK83E4fyxLnBl+x8diFWasVSAI3XLBX72gVno5LyAdwK9B6IqTGUvXt24/Gfd1PRrb7S4gYhwq96Lb7KpPnqElbs9yCeewjqImjzW4tWZrAug1fa4i7sGZX9l1BtpmRmov84JZPQKW5O4ocFuEpobiV1ESER8o4OxBKCCiwPyuZuGbnQrROF45C0qa67nF+R96OtcHraNKpqGkdsROST51Es5ISCLaBKyXzV8cgfzfzK7rap/DoYytbz2igInsHc1Gp+DHmkDKzDinNH0AGcSuA8FzR5W4Vzt+UVB9HTjAr5rgfrtiSAkrb4vXI/wE0iyKxMbdP0WVnY++im1mxjpywv6oeuwigx4aDiCBg/qD/JFdK4Db5J4TeRE60s/evigsrrhPjNrKXOjZQxVJU0d1xDoYJfk7bZumZPP0eSKvNRNmFARPVTZtR3geZjul8BZllBXbwCuxE2Ibg7uyqHsUVmJxF8dedKiBPaMWXkhmN3nBcTbopBsay9VrSn4L8EOXiXf36UrKL+IrDm5RzlPxA6vIafjsuHEJWnX1ec1qRiWLcU7SRkEbt8Dre+ktIMO3""\r\n"
"-----END CERTIFICATE-----""\r\n"
"-----BEGIN CERTIFICATE-----""\r\n"
"MIIFuzCCA6OgAwIBAgICA+gwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGVMQswCQYDVQQGEwJVUzEXMBUGA1UEAwwORWRnZSBEZXZpY2UgQ0ExEDAOBgNVBAcMB1JlZG1vbmQxIjAgBgNVBAoMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24xEzARBgNVBAgMCldhc2hpbmd0b24xIjAgBgNVBAsMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24wggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQCxqFOTRC1in4Kjhgba62GYYTZnDLsFk/Y9YqyhHr0+VMLEyZrwLRMyKS5V2nmt7lFMZsMDuoU+uISo+i+Wvx8aNjyalF8vQfVwQtRfFbSAVEzmEZMfff80SMdo31uN9KcmjTqrn1ULLHBEhmiOgW+V+gizAkcmCpCHWEv1MexlQ2t5RSM0BF2AIwA4I3DyT0OuVyAtC3UUxPDQb5KqUChBGexej/Y1JxcLDo7evxEH5eZtepXeVIO/yzn2a7PaplxEh2vStLsZVUuso1e8bghjREVp4OzHmce2Fss46XFTlah7gCTlCe7f03OVQOBS7IOxrPnm1xizmI4aNECa+HqkPoM83/fLUzjAYi3DFzwY+Y8kzt5tIq1jt5oXSAu+W/K3t1w9EMDn0BcKjvEMoJKiX2ZAD/PhLT+0GgGzyYenqwXLv9a0oh245rv/dD3Q+uL5sSuS9U+UF4j8NYVqXxRmU340/WQdfDyrL/IiRDrp+oelm3ddKX6qQ9ZqrlK31H1FAJrJH/6mf0auOdkumAHoGwL+vIzaezW52CuQDtNmRi3IoDoObdzSfW0aTeKoljr9/fq3jri7BI5GwWAhDBM+tiYPaMCaSxBI547SAFlla1xScI22a04L5ec3KHZleb6Rsfvd1ybWlSOjXOGqHcnGz9uUCwM/cYHcLQpnsroHxQIDAQABoxMwETAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBCwUAA4ICAQBkNRKg/xeJ2/n/KckHxCXv9QsPnnEFQu0Z2w2nw5GPi0Y9cSQHgwL1EwPvAsjQ7WBbe2e44DkwssbGnLO4kE0CkLgbTVbBPybrWeOcl3Ei173CBSwPOQxJZ14voquSFxglaYoVABaLpmsME4ZYn9W1occhoLKaZ7jGZAbLo/ZsigO1u/mSf6ZgaBSd1GdBeTfzLxu1IdnorYlKWudi9pQ/6TW/yT+mNq3iuMWNeqUJps2sgWkaaaqzvHx4dAOb6rzBC/4vuxIc2X2z6NgSjdddr1V3yCyjpX54TgM/q/00BhSaRluqQAn/QHqIrDbeExUbGSFfb9Ma1aiUMNuxgYGiF/v72P7Nq+WhOLa9mucoO293abq0SOAup4RdqOj9QnyJ91s1Lwe07bn3huF1ScYkOAQxmzA3rS8JZ2z6snJigI/Kb70Ba2rVdFjVDRuNEC5xhK6hFkLsk+quPKubNpHOQLSkXHf7sVGFT714j0JSoBa8OKMY3HErWGP1qBdp8HtfV1rtrYzesWvfPj4sAqLpvgq9cd2GXhoDlxKjZam9RkbdkdIVi59125y/qhqMpQF5uRKyDFx6GWkY+MgOMk0BbvUSVjH9bSdZZzupUvYpRodI92fYZWnlKNavPxi0bbJ/WcFDb/rbn83UtaFt3xnejuutm6RjKPSbQGLceR7O4A==""\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* TEST_CERT_CHAIN_NIX_EOL =
"-----BEGIN CERTIFICATE-----""\n"
"MIIFvTCCA6WgAwIBAgICA+kwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGUMQswCQYDVQQGEwJVUzEWMBQGA1UEAwwNRWRnZSBBZ2VudCBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOr+S7kLLzqhhw1U6O7xGc6tf82EjvUVBZdXR8t61j8G3JwgtyfDdGk0M8pcG3hOmfZwAbHqEUZ8i78uJchvYzilJQcINxUuwS1bl7MWiFkThsql/XLyYtCSnKrhqRVPi2hxwbN4v39HmazEmUXazFSgF13E+Si2/lfJ86JHfnnQCMZmDP02EtcPc1Rw3LmS7pg3h2mRv769Vj11Wtsr7nNTssbGc3yhAhXdt3sMWQihr4yBnhk931uyQiQgeQ69eK5L8g3KjRsOFvMJEXAsIk/lmuYquTaUZfaanuzRezzNVDMwZF9oiVXXClutGj/MlRYl+23gFChx+QAmFg1T7oCb2a1FdXIM5koycRtWKRAbBh+q20Asn6DcEhZE+yyiMQYaiPvUENpPKi5zX5q7lxzIhHb/LrQH9yzVxYGb4bj1t64SnOscwiDc02zRNrInqud8vkVITu/HUskaZWVX1ArwMPyurdNBgKM+ZocWN7niw1txzISjZIyYooYmKmFh6rc3D0TSLlno2WVvTcaxmnw4q9CNIRIH/9uH7mlDxprg4TRBHGx9Bvrh1YJpllTBHv6nhI78r5YTr0ofZ1fr3mHIRcxMdFTVwRtVbKCRmU30broaCOlNJewtTZB27nQnjJBu7CbOKWlyADJlvc5tD8EYiH8HP162XCqKYg4zwDkNAgMBAAGjFjAUMBIGA1UdEwEB/wQIMAYBAf8CAQAwDQYJKoZIhvcNAQELBQADggIBAHR9AU3JtlJdeWB1cndjZRKJ+YCMHThGKvV9AbVuPUogCnkVRuz2JBh34xeulT90Ihh8LvXA6qE8swQc39+lxXijHAOKKVPgXKF4Z4EEztyK83E4fyxLnBl+x8diFWasVSAI3XLBX72gVno5LyAdwK9B6IqTGUvXt24/Gfd1PRrb7S4gYhwq96Lb7KpPnqElbs9yCeewjqImjzW4tWZrAug1fa4i7sGZX9l1BtpmRmov84JZPQKW5O4ocFuEpobiV1ESER8o4OxBKCCiwPyuZuGbnQrROF45C0qa67nF+R96OtcHraNKpqGkdsROST51Es5ISCLaBKyXzV8cgfzfzK7rap/DoYytbz2igInsHc1Gp+DHmkDKzDinNH0AGcSuA8FzR5W4Vzt+UVB9HTjAr5rgfrtiSAkrb4vXI/wE0iyKxMbdP0WVnY++im1mxjpywv6oeuwigx4aDiCBg/qD/JFdK4Db5J4TeRE60s/evigsrrhPjNrKXOjZQxVJU0d1xDoYJfk7bZumZPP0eSKvNRNmFARPVTZtR3geZjul8BZllBXbwCuxE2Ibg7uyqHsUVmJxF8dedKiBPaMWXkhmN3nBcTbopBsay9VrSn4L8EOXiXf36UrKL+IrDm5RzlPxA6vIafjsuHEJWnX1ec1qRiWLcU7SRkEbt8Dre+ktIMO3""\n"
"-----END CERTIFICATE-----""\n"
"-----BEGIN CERTIFICATE-----""\n"
"MIIFuzCCA6OgAwIBAgICA+gwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGVMQswCQYDVQQGEwJVUzEXMBUGA1UEAwwORWRnZSBEZXZpY2UgQ0ExEDAOBgNVBAcMB1JlZG1vbmQxIjAgBgNVBAoMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24xEzARBgNVBAgMCldhc2hpbmd0b24xIjAgBgNVBAsMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24wggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQCxqFOTRC1in4Kjhgba62GYYTZnDLsFk/Y9YqyhHr0+VMLEyZrwLRMyKS5V2nmt7lFMZsMDuoU+uISo+i+Wvx8aNjyalF8vQfVwQtRfFbSAVEzmEZMfff80SMdo31uN9KcmjTqrn1ULLHBEhmiOgW+V+gizAkcmCpCHWEv1MexlQ2t5RSM0BF2AIwA4I3DyT0OuVyAtC3UUxPDQb5KqUChBGexej/Y1JxcLDo7evxEH5eZtepXeVIO/yzn2a7PaplxEh2vStLsZVUuso1e8bghjREVp4OzHmce2Fss46XFTlah7gCTlCe7f03OVQOBS7IOxrPnm1xizmI4aNECa+HqkPoM83/fLUzjAYi3DFzwY+Y8kzt5tIq1jt5oXSAu+W/K3t1w9EMDn0BcKjvEMoJKiX2ZAD/PhLT+0GgGzyYenqwXLv9a0oh245rv/dD3Q+uL5sSuS9U+UF4j8NYVqXxRmU340/WQdfDyrL/IiRDrp+oelm3ddKX6qQ9ZqrlK31H1FAJrJH/6mf0auOdkumAHoGwL+vIzaezW52CuQDtNmRi3IoDoObdzSfW0aTeKoljr9/fq3jri7BI5GwWAhDBM+tiYPaMCaSxBI547SAFlla1xScI22a04L5ec3KHZleb6Rsfvd1ybWlSOjXOGqHcnGz9uUCwM/cYHcLQpnsroHxQIDAQABoxMwETAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBCwUAA4ICAQBkNRKg/xeJ2/n/KckHxCXv9QsPnnEFQu0Z2w2nw5GPi0Y9cSQHgwL1EwPvAsjQ7WBbe2e44DkwssbGnLO4kE0CkLgbTVbBPybrWeOcl3Ei173CBSwPOQxJZ14voquSFxglaYoVABaLpmsME4ZYn9W1occhoLKaZ7jGZAbLo/ZsigO1u/mSf6ZgaBSd1GdBeTfzLxu1IdnorYlKWudi9pQ/6TW/yT+mNq3iuMWNeqUJps2sgWkaaaqzvHx4dAOb6rzBC/4vuxIc2X2z6NgSjdddr1V3yCyjpX54TgM/q/00BhSaRluqQAn/QHqIrDbeExUbGSFfb9Ma1aiUMNuxgYGiF/v72P7Nq+WhOLa9mucoO293abq0SOAup4RdqOj9QnyJ91s1Lwe07bn3huF1ScYkOAQxmzA3rS8JZ2z6snJigI/Kb70Ba2rVdFjVDRuNEC5xhK6hFkLsk+quPKubNpHOQLSkXHf7sVGFT714j0JSoBa8OKMY3HErWGP1qBdp8HtfV1rtrYzesWvfPj4sAqLpvgq9cd2GXhoDlxKjZam9RkbdkdIVi59125y/qhqMpQF5uRKyDFx6GWkY+MgOMk0BbvUSVjH9bSdZZzupUvYpRodI92fYZWnlKNavPxi0bbJ/WcFDb/rbn83UtaFt3xnejuutm6RjKPSbQGLceR7O4A==""\n"
"-----END CERTIFICATE-----\n";

static const char* EXPECTED_TEST_CERT_CHAIN_WIN_EOL =
"-----BEGIN CERTIFICATE-----\r\n"
"MIIFuzCCA6OgAwIBAgICA+gwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGVMQswCQYDVQQGEwJVUzEXMBUGA1UEAwwORWRnZSBEZXZpY2UgQ0ExEDAOBgNVBAcMB1JlZG1vbmQxIjAgBgNVBAoMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24xEzARBgNVBAgMCldhc2hpbmd0b24xIjAgBgNVBAsMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24wggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQCxqFOTRC1in4Kjhgba62GYYTZnDLsFk/Y9YqyhHr0+VMLEyZrwLRMyKS5V2nmt7lFMZsMDuoU+uISo+i+Wvx8aNjyalF8vQfVwQtRfFbSAVEzmEZMfff80SMdo31uN9KcmjTqrn1ULLHBEhmiOgW+V+gizAkcmCpCHWEv1MexlQ2t5RSM0BF2AIwA4I3DyT0OuVyAtC3UUxPDQb5KqUChBGexej/Y1JxcLDo7evxEH5eZtepXeVIO/yzn2a7PaplxEh2vStLsZVUuso1e8bghjREVp4OzHmce2Fss46XFTlah7gCTlCe7f03OVQOBS7IOxrPnm1xizmI4aNECa+HqkPoM83/fLUzjAYi3DFzwY+Y8kzt5tIq1jt5oXSAu+W/K3t1w9EMDn0BcKjvEMoJKiX2ZAD/PhLT+0GgGzyYenqwXLv9a0oh245rv/dD3Q+uL5sSuS9U+UF4j8NYVqXxRmU340/WQdfDyrL/IiRDrp+oelm3ddKX6qQ9ZqrlK31H1FAJrJH/6mf0auOdkumAHoGwL+vIzaezW52CuQDtNmRi3IoDoObdzSfW0aTeKoljr9/fq3jri7BI5GwWAhDBM+tiYPaMCaSxBI547SAFlla1xScI22a04L5ec3KHZleb6Rsfvd1ybWlSOjXOGqHcnGz9uUCwM/cYHcLQpnsroHxQIDAQABoxMwETAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBCwUAA4ICAQBkNRKg/xeJ2/n/KckHxCXv9QsPnnEFQu0Z2w2nw5GPi0Y9cSQHgwL1EwPvAsjQ7WBbe2e44DkwssbGnLO4kE0CkLgbTVbBPybrWeOcl3Ei173CBSwPOQxJZ14voquSFxglaYoVABaLpmsME4ZYn9W1occhoLKaZ7jGZAbLo/ZsigO1u/mSf6ZgaBSd1GdBeTfzLxu1IdnorYlKWudi9pQ/6TW/yT+mNq3iuMWNeqUJps2sgWkaaaqzvHx4dAOb6rzBC/4vuxIc2X2z6NgSjdddr1V3yCyjpX54TgM/q/00BhSaRluqQAn/QHqIrDbeExUbGSFfb9Ma1aiUMNuxgYGiF/v72P7Nq+WhOLa9mucoO293abq0SOAup4RdqOj9QnyJ91s1Lwe07bn3huF1ScYkOAQxmzA3rS8JZ2z6snJigI/Kb70Ba2rVdFjVDRuNEC5xhK6hFkLsk+quPKubNpHOQLSkXHf7sVGFT714j0JSoBa8OKMY3HErWGP1qBdp8HtfV1rtrYzesWvfPj4sAqLpvgq9cd2GXhoDlxKjZam9RkbdkdIVi59125y/qhqMpQF5uRKyDFx6GWkY+MgOMk0BbvUSVjH9bSdZZzupUvYpRodI92fYZWnlKNavPxi0bbJ/WcFDb/rbn83UtaFt3xnejuutm6RjKPSbQGLceR7O4A==\r\n"
"-----END CERTIFICATE-----\r\n";

static const char* EXPECTED_TEST_CERT_CHAIN_NIX_EOL =
"-----BEGIN CERTIFICATE-----\n"
"MIIFuzCCA6OgAwIBAgICA+gwDQYJKoZIhvcNAQELBQAwgZUxCzAJBgNVBAYTAlVTMRcwFQYDVQQDDA5FZGdlIERldmljZSBDQTEQMA4GA1UEBwwHUmVkbW9uZDEiMCAGA1UECgwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjETMBEGA1UECAwKV2FzaGluZ3RvbjEiMCAGA1UECwwZRGVmYXVsdCBFZGdlIE9yZ2FuaXphdGlvbjAeFw0xODA0MjQwMzU1NTdaFw0xOTA0MjQwMzU1NTdaMIGVMQswCQYDVQQGEwJVUzEXMBUGA1UEAwwORWRnZSBEZXZpY2UgQ0ExEDAOBgNVBAcMB1JlZG1vbmQxIjAgBgNVBAoMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24xEzARBgNVBAgMCldhc2hpbmd0b24xIjAgBgNVBAsMGURlZmF1bHQgRWRnZSBPcmdhbml6YXRpb24wggIiMA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQCxqFOTRC1in4Kjhgba62GYYTZnDLsFk/Y9YqyhHr0+VMLEyZrwLRMyKS5V2nmt7lFMZsMDuoU+uISo+i+Wvx8aNjyalF8vQfVwQtRfFbSAVEzmEZMfff80SMdo31uN9KcmjTqrn1ULLHBEhmiOgW+V+gizAkcmCpCHWEv1MexlQ2t5RSM0BF2AIwA4I3DyT0OuVyAtC3UUxPDQb5KqUChBGexej/Y1JxcLDo7evxEH5eZtepXeVIO/yzn2a7PaplxEh2vStLsZVUuso1e8bghjREVp4OzHmce2Fss46XFTlah7gCTlCe7f03OVQOBS7IOxrPnm1xizmI4aNECa+HqkPoM83/fLUzjAYi3DFzwY+Y8kzt5tIq1jt5oXSAu+W/K3t1w9EMDn0BcKjvEMoJKiX2ZAD/PhLT+0GgGzyYenqwXLv9a0oh245rv/dD3Q+uL5sSuS9U+UF4j8NYVqXxRmU340/WQdfDyrL/IiRDrp+oelm3ddKX6qQ9ZqrlK31H1FAJrJH/6mf0auOdkumAHoGwL+vIzaezW52CuQDtNmRi3IoDoObdzSfW0aTeKoljr9/fq3jri7BI5GwWAhDBM+tiYPaMCaSxBI547SAFlla1xScI22a04L5ec3KHZleb6Rsfvd1ybWlSOjXOGqHcnGz9uUCwM/cYHcLQpnsroHxQIDAQABoxMwETAPBgNVHRMBAf8EBTADAQH/MA0GCSqGSIb3DQEBCwUAA4ICAQBkNRKg/xeJ2/n/KckHxCXv9QsPnnEFQu0Z2w2nw5GPi0Y9cSQHgwL1EwPvAsjQ7WBbe2e44DkwssbGnLO4kE0CkLgbTVbBPybrWeOcl3Ei173CBSwPOQxJZ14voquSFxglaYoVABaLpmsME4ZYn9W1occhoLKaZ7jGZAbLo/ZsigO1u/mSf6ZgaBSd1GdBeTfzLxu1IdnorYlKWudi9pQ/6TW/yT+mNq3iuMWNeqUJps2sgWkaaaqzvHx4dAOb6rzBC/4vuxIc2X2z6NgSjdddr1V3yCyjpX54TgM/q/00BhSaRluqQAn/QHqIrDbeExUbGSFfb9Ma1aiUMNuxgYGiF/v72P7Nq+WhOLa9mucoO293abq0SOAup4RdqOj9QnyJ91s1Lwe07bn3huF1ScYkOAQxmzA3rS8JZ2z6snJigI/Kb70Ba2rVdFjVDRuNEC5xhK6hFkLsk+quPKubNpHOQLSkXHf7sVGFT714j0JSoBa8OKMY3HErWGP1qBdp8HtfV1rtrYzesWvfPj4sAqLpvgq9cd2GXhoDlxKjZam9RkbdkdIVi59125y/qhqMpQF5uRKyDFx6GWkY+MgOMk0BbvUSVjH9bSdZZzupUvYpRodI92fYZWnlKNavPxi0bbJ/WcFDb/rbn83UtaFt3xnejuutm6RjKPSbQGLceR7O4A==\n"
"-----END CERTIFICATE-----\n";

// generated using the following commands
// openssl genrsa -out private.pem 2048
// openssl req -new -x509 -key private.pem -subj "/C=US/ST=WA/O=Test Org/OU=Test Org Unit/L=Redmond" -days 365 -sha256 -out cert.pem
// cert.pem contents were copied into TEST_CERT_WITH_NO_COMMON_NAME below
static const char* TEST_CERT_WITH_NO_COMMON_NAME =
"-----BEGIN CERTIFICATE-----\n"
"MIIDgTCCAmmgAwIBAgIJAMokilkMeYECMA0GCSqGSIb3DQEBCwUAMFcxCzAJBgNV\n"
"BAYTAlVTMQswCQYDVQQIDAJXQTERMA8GA1UECgwIVGVzdCBPcmcxFjAUBgNVBAsM\n"
"DVRlc3QgT3JnIFVuaXQxEDAOBgNVBAcMB1JlZG1vbmQwHhcNMTkwNDE4MjMzOTI1\n"
"WhcNMjAwNDE3MjMzOTI1WjBXMQswCQYDVQQGEwJVUzELMAkGA1UECAwCV0ExETAP\n"
"BgNVBAoMCFRlc3QgT3JnMRYwFAYDVQQLDA1UZXN0IE9yZyBVbml0MRAwDgYDVQQH\n"
"DAdSZWRtb25kMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvg9wiGQH\n"
"Fl184YGTCiS1BWdeHJdAD8jGm2QBo1y3zcVSayb52RX2FoIECa3PXghPLBh4tpyL\n"
"7Yy7Fryl5ZTdm0Umhtancq+bE4kxl9CHU3YAXn2ywZ+Hk734w08wUiUOwsRAeUB5\n"
"ySJPtZ3pjEj9HYUuKEg59ugI472OVd/jjD96A8iFg0hSDTcVN3ufBjFCbJHVVXvD\n"
"ZUXvXAkKsDy1lBBiPygwHL19/yJlL5Fnq1SlCB3OWIBe4t8DOZmAhIkfFhurXuij\n"
"1ERsES2I15omw9wBM8Ry0VdDe6zmLVz2JOX9FluP1S/g+XumhD/5nQw2nWx+Y1VY\n"
"iM41T3J9QnIJLwIDAQABo1AwTjAdBgNVHQ4EFgQUJMGCz6rgXFrEqpKFKZ+8g+UJ\n"
"OuUwHwYDVR0jBBgwFoAUJMGCz6rgXFrEqpKFKZ+8g+UJOuUwDAYDVR0TBAUwAwEB\n"
"/zANBgkqhkiG9w0BAQsFAAOCAQEAGsR2HYikKJ/UMTFDvS52kT8hMqcZCi5/DIlC\n"
"HRmlANPbQzL4UzuHw9ZS6W6o89W3Kx2Ryacpyi0mRjkOyQwDaUwpP15nClV8wqVJ\n"
"IjHYjArU00x5YX2xaT1vL6sV5iUQpPDh3DWVdDZNfJBXl/dcDDn8FVRvEliJCK+2\n"
"hQqB8m219XaXqKNFfty3pdosEbpVbx326cP1mVOeDDVf9IZhBVPr/80W1WCHVhwl\n"
"IEAow9agavLMOitkBvHypZJSzfZ4M0r5vMqUOu9JydAYf7kiLbIFuFG547MfqADp\n"
"iqbY++jm7yI58llqAJXZ9ffktfslQxgXDw38QflZ3tKdsaakYQ==\n"
"-----END CERTIFICATE-----\n";

// generated using the following commands
// openssl genrsa -out private.pem 2048
// openssl req -new -x509 -key private.pem -subj "/C=US/ST=WA/O=Test Org/OU=Test Org Unit/L=Redmond/CN=localhost" -days 365 -sha256 -out cert.pem
// cert.pem contents were copied into TEST_RSA_CERT_WITH_ALL_SUBJECT_FIELDS below
static const char* TEST_RSA_CERT_WITH_ALL_SUBJECT_FIELDS =
"-----BEGIN CERTIFICATE-----\n"
"MIIDqTCCApGgAwIBAgIJAPM7Wcluwri1MA0GCSqGSIb3DQEBCwUAMGsxCzAJBgNV\n"
"BAYTAlVTMQswCQYDVQQIDAJXQTERMA8GA1UECgwIVGVzdCBPcmcxFjAUBgNVBAsM\n"
"DVRlc3QgT3JnIFVuaXQxEDAOBgNVBAcMB1JlZG1vbmQxEjAQBgNVBAMMCWxvY2Fs\n"
"aG9zdDAeFw0xOTA0MTgyMzMzMzRaFw0yMDA0MTcyMzMzMzRaMGsxCzAJBgNVBAYT\n"
"AlVTMQswCQYDVQQIDAJXQTERMA8GA1UECgwIVGVzdCBPcmcxFjAUBgNVBAsMDVRl\n"
"c3QgT3JnIFVuaXQxEDAOBgNVBAcMB1JlZG1vbmQxEjAQBgNVBAMMCWxvY2FsaG9z\n"
"dDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAN5ZCnVI/qsKo9/uSzLW\n"
"Qrzod0+Hk+UdKtz4SIlai5FNQySw6t+lfrWs+/xMSeW/z7ZfHvshGp2kKqXTfSvH\n"
"M3spXxYu7uIY8Bq+aVq84FgXpb+2wThmT1KghtX6VE4DK+5V/fmsjmvLZX+bQRhl\n"
"KtW3Jn5jl8OJijugJ9hp/5/GlMgGp14oIUzp1+ryeKhpMCKfzYRFAzN73HI813kQ\n"
"syV+9CRCUgF6nbVhcQ+NrCq4zE0tKJrhtDcspHvYhK1fLCiHU9LvJpqJgceDIXFL\n"
"ZrmNEjeK0DP00+9Pp+kqS3Rsj+HkCccWQDMaYcspH/2425g73hycS+ob+wdPP5+l\n"
"wJcCAwEAAaNQME4wHQYDVR0OBBYEFAh49ibGqLjdUnBKXq6WOG0c/mlyMB8GA1Ud\n"
"IwQYMBaAFAh49ibGqLjdUnBKXq6WOG0c/mlyMAwGA1UdEwQFMAMBAf8wDQYJKoZI\n"
"hvcNAQELBQADggEBABqBuy7ai3Js3t92y0IScsyhvPMzoT0nehHn9EpXwjYhDJlc\n"
"oP0vSL2hHEBIdM6A31XnvfSLR94RZbzRhXBx6+jLmCVeqDddLt/1lEoRnrZx+pft\n"
"S4NVEBkZlsa8m5Zx7Js/LmwBEX8DpUtXT9rEdtNxlvdPjaHaT/LJ14tTPOwOnUsV\n"
"dx4V2Qa6z5VaT8TRJnUW56eaSwLWBla0b2oQqNJbKj3S4kjceFFMQkJmt6KDYvBV\n"
"CY2A5WnhbEVFnaAfafgGsrBbpKFuYVSfXunXtAuzNq3ZCzPxQVVBsqsRTKaJul2z\n"
"eSP0FBVsGTINGmz1N2Oen7VvrmzPW5Q2OdsV1Og=\n"
"-----END CERTIFICATE-----\n";

static const unsigned char TEST_PRIVATE_KEY[] = { 0x32, 0x03, 0x33, 0x34, 0x35, 0x36 };
static size_t TEST_PRIVATE_KEY_LEN = sizeof(TEST_PRIVATE_KEY)/sizeof(TEST_PRIVATE_KEY[0]);

//#############################################################################
// Mocked functions test hooks
//#############################################################################

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static BIO* test_hook_BIO_new(const BIO_METHOD* type)
#else
static BIO* test_hook_BIO_new(BIO_METHOD* type)
#endif
{
    (void)type;
    return TEST_BIO;
}

#if ((OPENSSL_VERSION_NUMBER & 0xFFF00000L) >= 0x10100000L)
static const BIO_METHOD* test_hook_BIO_s_mem(void)
#else
static BIO_METHOD* test_hook_BIO_s_mem(void)
#endif
{
    return TEST_BIO_METHOD;
}

static int test_hook_BIO_write(BIO *b, const void *in, int inl)
{
    (void)b;
    (void)in;

    return inl;
}

static X509* test_hook_PEM_read_bio_X509(BIO *bp, X509 **x, pem_password_cb *cb, void *u)
{
    (void)bp;
    (void)x;
    (void)cb;
    (void)u;

    return TEST_X509;
}

static void test_hook_BIO_free_all(BIO *bio)
{
    (void)bio;
}

static ASN1_TIME* test_hook_X509_get_notAfter(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_AFTER;
}

static ASN1_TIME* test_hook_X509_get_notBefore(X509 *x509_cert)
{
    (void)x509_cert;

    return &TEST_ASN1_TIME_BEFORE;
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

static void test_hook_X509_free(X509 *a)
{
    (void)a;
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
            case NID_commonName:
            value = TEST_COMMON_NAME;
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

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

#ifdef CPP_UNITTEST
/*apparently CppUniTest.h does not define the below which is needed for int64_t asserts*/
template <> static std::wstring Microsoft::VisualStudio::CppUnitTestFramework::ToString < int64_t >(const int64_t& q)
{
    std::wstring result;
    std::wostringstream o;
    o << q;
    return o.str();
}
#endif

//#############################################################################
// Test helpers
//#############################################################################
static void test_helper_parse_cert_common_callstack
(
    const char* certificate,
    size_t certificate_size,
    bool private_key_set,
    char *failed_function_list,
    size_t failed_function_size
)
{
    uint64_t failed_function_bitmask = 0;
    size_t i = 0;
    size_t certificate_len = strlen(certificate);

    umock_c_reset_all_calls();

    EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(gballoc_malloc(certificate_size));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    // *************** Load and parse certificate **************
    EXPECTED_CALL(BIO_s_mem());
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(BIO_new(TEST_BIO_METHOD));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BIO_write(TEST_BIO, IGNORED_PTR_ARG, certificate_len));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(PEM_read_bio_X509(TEST_BIO, NULL, NULL, NULL));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(BIO_free_all(TEST_BIO));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Parse validity timestamps **************
    STRICT_EXPECTED_CALL(mocked_X509_get_notAfter(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;

    STRICT_EXPECTED_CALL(mocked_X509_get_notBefore(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Parse common name **************
    STRICT_EXPECTED_CALL(X509_get_subject_name(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(gballoc_malloc(MAX_COMMON_NAME_SIZE));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_NAME_get_text_by_NID(TEST_X509_SUBJECT_NAME, NID_commonName, IGNORED_PTR_ARG, MAX_COMMON_NAME_SIZE));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    failed_function_list[i++] = 1;

    STRICT_EXPECTED_CALL(X509_free(TEST_X509));
    ASSERT_IS_TRUE((i < failed_function_size), "Line:" TOSTRING(__LINE__));
    i++;
    // *************************************************

    // *************** Finalize certificate info object **************
    // allocator for the first certificate which includes /r/n ending
    STRICT_EXPECTED_CALL(gballoc_malloc(certificate_size));
    // allocator for the private key
    if (private_key_set)
    {
        STRICT_EXPECTED_CALL(gballoc_malloc(TEST_PRIVATE_KEY_LEN));
    }
    // *************************************************
}

static void test_helper_parse_cert_callstack
(
    const char* certificate,
    size_t certificate_size,
    char *failed_function_list,
    size_t failed_function_size
)
{
    test_helper_parse_cert_common_callstack(certificate, certificate_size, true, failed_function_list, failed_function_size);
}

//#############################################################################
// Test cases
//#############################################################################

BEGIN_TEST_SUITE(certificate_info_ut)

    TEST_SUITE_INITIALIZE(suite_init)
    {
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        umock_c_init(test_hook_on_umock_c_error);
        ASSERT_ARE_EQUAL(int, 0, umocktypes_charptr_register_types());
        ASSERT_ARE_EQUAL(int, 0, umocktypes_stdint_register_types());

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_s_mem, test_hook_BIO_s_mem);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_new, test_hook_BIO_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_new, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_write, test_hook_BIO_write);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BIO_write, 0);

        REGISTER_GLOBAL_MOCK_HOOK(PEM_read_bio_X509, test_hook_PEM_read_bio_X509);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(PEM_read_bio_X509, NULL);

        REGISTER_GLOBAL_MOCK_HOOK(BIO_free_all, test_hook_BIO_free_all);

        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notBefore, test_hook_X509_get_notBefore);
        REGISTER_GLOBAL_MOCK_HOOK(mocked_X509_get_notAfter, test_hook_X509_get_notAfter);

        REGISTER_GLOBAL_MOCK_HOOK(X509_get_subject_name, test_hook_X509_get_subject_name);

        REGISTER_GLOBAL_MOCK_HOOK(X509_NAME_get_text_by_NID, test_hook_X509_NAME_get_text_by_NID);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(X509_NAME_get_text_by_NID, 0);

        REGISTER_GLOBAL_MOCK_HOOK(X509_free, test_hook_X509_free);
    }

    TEST_SUITE_CLEANUP(suite_cleanup)
    {
        umock_c_deinit();

        TEST_MUTEX_DESTROY(g_testByTest);
        TEST_DEINITIALIZE_MEMORY_DEBUG(g_dllByDll);
    }

    TEST_FUNCTION_INITIALIZE(method_init)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Could not acquire test serialization mutex.");
        }
        umock_c_reset_all_calls();
    }

    TEST_FUNCTION_CLEANUP(method_cleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
    }

    TEST_FUNCTION(certificate_info_create_cert_NULL_fail)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(NULL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_cert_empty_string_fail)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create("", TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_type_unknown_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_type_invalid_fails)
    {
        //arrange
        int BAD_PRIVATE_KEY_TYPE = 50;

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, (PRIVATE_KEY_TYPE)BAD_PRIVATE_KEY_TYPE);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_type_payload_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_type_reference_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_REFERENCE);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_null_and_size_non_zero_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, NULL, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_non_null_zero_length_fails)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, 0, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_rsa_win_succeed)
    {
        //arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];

        test_helper_parse_cert_callstack(TEST_RSA_CERT_WIN_EOL, strlen(TEST_RSA_CERT_WIN_EOL) + 1, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_rsa_nix_succeed)
    {
        //arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];

        test_helper_parse_cert_callstack(TEST_RSA_CERT_NIX_EOL, strlen(TEST_RSA_CERT_NIX_EOL) + 1, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_no_private_key_succeed)
    {
        //arrange
        size_t failed_function_size = MAX_FAILED_FUNCTION_LIST_SIZE;
        char failed_function_list[MAX_FAILED_FUNCTION_LIST_SIZE];

        test_helper_parse_cert_common_callstack(TEST_RSA_CERT_NIX_EOL, strlen(TEST_RSA_CERT_NIX_EOL) + 1, false, failed_function_list, MAX_FAILED_FUNCTION_LIST_SIZE);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_NIX_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

#if 0
    TEST_FUNCTION(certificate_info_create_fail)
    {
        //arrange
        setup_parse_cert(strlen(TEST_RSA_CERT_WIN_EOL) + 1);

        int negativeTestsInitResult = umock_c_negative_tests_init();
        ASSERT_ARE_EQUAL(int, 0, negativeTestsInitResult);

        umock_c_negative_tests_snapshot();

        size_t calls_cannot_fail[] = { 6, 7, 8, 9, 11 };

        //act
        size_t count = umock_c_negative_tests_call_count();
        for (size_t index = 0; index < count; index++)
        {
            if (should_skip_index(index, calls_cannot_fail, sizeof(calls_cannot_fail) / sizeof(calls_cannot_fail[0])) != 0)
            {
                continue;
            }

            umock_c_negative_tests_reset();
            umock_c_negative_tests_fail_call(index);

            char tmp_msg[64];
            sprintf(tmp_msg, "certificate_info_create failure in test %zu/%zu", index, count);

            CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

            //assert
            ASSERT_IS_NULL(cert_handle, tmp_msg);
        }

        //cleanup
        umock_c_negative_tests_deinit();
    }

    TEST_FUNCTION(certificate_info_destroy_with_private_key_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        //act
        certificate_info_destroy(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(certificate_info_destroy_without_private_key_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);
        umock_c_reset_all_calls();
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));

        //act
        certificate_info_destroy(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
    }

    TEST_FUNCTION(certificate_info_destroy_handle_NULL_fail)
    {
        //arrange

        //act
        certificate_info_destroy(NULL);

        //assert

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_certificate_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_RSA_CERT_WIN_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_certificate_fail)
    {
        //arrange

        //act
        const char* certificate = certificate_info_get_certificate(NULL);

        //assert
        ASSERT_IS_NULL(certificate);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_certificate_leaf_fail)
    {
        //arrange

        //act
        const char* certificate = certificate_info_get_leaf_certificate(NULL);

        //assert
        ASSERT_IS_NULL(certificate);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_private_key_succeed)
    {
        //arrange
        size_t pk_len;
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(cert_handle, &pk_len);

        //assert
        ASSERT_IS_NOT_NULL(priv_key);
        ASSERT_ARE_EQUAL(int, 0, memcmp(priv_key, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN));
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_len);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_private_key_handle_NULL_fail)
    {
        //arrange
        size_t pk_len = 123;

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(NULL, &pk_len);

        //assert
        ASSERT_IS_NULL(priv_key);
        ASSERT_ARE_EQUAL(size_t, 123, pk_len);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_private_key_length_NULL_fail)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const unsigned char* priv_key = (const unsigned char*)certificate_info_get_private_key(cert_handle, NULL);

        //assert
        ASSERT_IS_NULL(priv_key);

        //cleanup
        certificate_info_destroy(cert_handle);
    }


    TEST_FUNCTION(certificate_info_get_valid_from_handle_NULL_fail)
    {
        //arrange

        //act
        int64_t valid_from = certificate_info_get_valid_from(NULL);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, valid_from);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_valid_to_handle_NULL_fail)
    {
        //arrange

        //act
        int64_t valid_to = certificate_info_get_valid_to(NULL);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, valid_to);

        //cleanup
    }

    TEST_FUNCTION(certificate_info_private_key_type_handle_NULL_fail)
    {
        //arrange

        //act
        (void)certificate_info_private_key_type(NULL);

        //assert

        //cleanup
    }


    TEST_FUNCTION(certificate_info_get_chain_handle_NULL_fail)
    {
        //arrange

        //act
        (void)certificate_info_get_chain(NULL);

        //assert

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_invalid_smaller_len_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 12);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_invalid_larger_len_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 14);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 0, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_utc_time_from_asn_string_success_test)
    {
        //arrange
        time_t test_time;

        //act
        test_time = get_utc_time_from_asn_string((unsigned char*)"180101010101Z", 13);

        //assert
        ASSERT_ARE_EQUAL(int64_t, 1514768461, test_time);

        //cleanup
    }

    TEST_FUNCTION(get_common_name_NULL_param_fails)
    {
        //arrange

        // act
        const char* result = certificate_info_get_common_name(NULL);

        // assert
        ASSERT_IS_NULL(result);
    }
#endif

END_TEST_SUITE(certificate_info_ut)
