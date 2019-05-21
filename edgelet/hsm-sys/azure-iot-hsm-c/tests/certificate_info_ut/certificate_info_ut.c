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
#include "umock_c/umock_c.h"
#include "umock_c/umocktypes_charptr.h"
#include "umock_c/umocktypes_stdint.h"
#include "umock_c/umock_c_negative_tests.h"
#include "azure_macro_utils/macro_utils.h"

#define ENABLE_MOCKS
#include "azure_c_shared_utility/gballoc.h"
#include "umock_c/umock_c_prod.h"

#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/azure_base64.h"
#undef ENABLE_MOCKS

#include "certificate_info.h"


#ifdef __cplusplus
extern "C" {
#endif

 extern time_t get_utc_time_from_asn_string(const unsigned char *time_value, size_t length);
 extern STRING_HANDLE real_Azure_Base64_Encode(BUFFER_HANDLE input);
 extern STRING_HANDLE real_Base64_Encode_Bytes(const unsigned char* source, size_t size);
 extern BUFFER_HANDLE real_Azure_Base64_Decode(const char* source);

 extern BUFFER_HANDLE real_BUFFER_new(void);
 extern void real_BUFFER_delete(BUFFER_HANDLE handle);
 extern unsigned char* real_BUFFER_u_char(BUFFER_HANDLE handle);
 extern size_t real_BUFFER_length(BUFFER_HANDLE handle);
 extern int real_BUFFER_build(BUFFER_HANDLE handle, const unsigned char* source, size_t size);
 extern BUFFER_HANDLE real_BUFFER_create(const unsigned char* source, size_t size);
 extern int real_BUFFER_pre_build(BUFFER_HANDLE handle, size_t size);

#ifdef __cplusplus
}
#endif

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

static TEST_MUTEX_HANDLE g_testByTest;
static TEST_MUTEX_HANDLE g_dllByDll;

MU_DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

static void on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", MU_ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
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

BEGIN_TEST_SUITE(certificate_info_ut)

    TEST_SUITE_INITIALIZE(suite_init)
    {
        //int result;
        TEST_INITIALIZE_MEMORY_DEBUG(g_dllByDll);
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);

        (void)umock_c_init(on_umock_c_error);
        (void)umocktypes_stdint_register_types();
        (void)umocktypes_charptr_register_types();

        REGISTER_UMOCK_ALIAS_TYPE(BUFFER_HANDLE, void*);
        //REGISTER_UMOCK_ALIAS_TYPE(int64_t, int);

        REGISTER_GLOBAL_MOCK_HOOK(gballoc_malloc, my_gballoc_malloc);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(gballoc_malloc, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(gballoc_free, my_gballoc_free);

        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_create, real_BUFFER_create);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_create, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_delete, real_BUFFER_delete);
        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_u_char, real_BUFFER_u_char);
        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_length, real_BUFFER_length);
        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_new, real_BUFFER_new);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_new, NULL);
        REGISTER_GLOBAL_MOCK_HOOK(BUFFER_pre_build, real_BUFFER_pre_build);
        REGISTER_GLOBAL_MOCK_FAIL_RETURN(BUFFER_pre_build, __LINE__);

        REGISTER_GLOBAL_MOCK_HOOK(Azure_Base64_Decode, real_Azure_Base64_Decode);
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

    static int should_skip_index(size_t current_index, const size_t skip_array[], size_t length)
    {
        int result = 0;
        for (size_t index = 0; index < length; index++)
        {
            if (current_index == skip_array[index])
            {
                result = __LINE__;
                break;
            }
        }
        return result;
    }

    static void setup_parse_cert_common(size_t cert_len, bool private_key_set)
    {
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(gballoc_malloc(cert_len));
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(Azure_Base64_Decode(IGNORED_PTR_ARG));
        // *************** Happens in Decoder **************
        STRICT_EXPECTED_CALL(BUFFER_new());
        STRICT_EXPECTED_CALL(BUFFER_pre_build(IGNORED_PTR_ARG, IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(BUFFER_u_char(IGNORED_PTR_ARG));
        // *************************************************
        STRICT_EXPECTED_CALL(gballoc_free(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(BUFFER_u_char(IGNORED_PTR_ARG));
        STRICT_EXPECTED_CALL(BUFFER_length(IGNORED_PTR_ARG));
        // allocator for common name
        STRICT_EXPECTED_CALL(gballoc_malloc(IGNORED_NUM_ARG));
        STRICT_EXPECTED_CALL(BUFFER_delete(IGNORED_PTR_ARG));
        // allocator for the first certificate which includes /r/n ending
        STRICT_EXPECTED_CALL(gballoc_malloc(cert_len));
        // allocator for the private key
        if (private_key_set)
        {
            STRICT_EXPECTED_CALL(gballoc_malloc(TEST_PRIVATE_KEY_LEN));
        }
    }

    static void setup_parse_cert(size_t cert_len)
    {
        setup_parse_cert_common(cert_len, true);
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

    TEST_FUNCTION(certificate_info_create_pk_NULL_pass)
    {
        //arrange

        //act
        size_t pk_size = 100;
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);
        const void* pk = certificate_info_get_private_key(cert_handle, &pk_size);
        PRIVATE_KEY_TYPE pk_type = certificate_info_private_key_type(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_IS_NULL(pk);
        ASSERT_ARE_EQUAL(size_t, 0, pk_size, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_UNKNOWN, pk_type, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_payload_pass)
    {
        //arrange

        //act
        size_t pk_size = 100;
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        const void* pk = certificate_info_get_private_key(cert_handle, &pk_size);
        PRIVATE_KEY_TYPE pk_type = certificate_info_private_key_type(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_size, "Line:" TOSTRING(__LINE__));
        int cmp = memcmp(pk, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_PAYLOAD, pk_type, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_pk_payload_reference_pass)
    {
        //arrange

        //act
        size_t pk_size = 100;
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_REFERENCE);
        const void* pk = certificate_info_get_private_key(cert_handle, &pk_size);
        PRIVATE_KEY_TYPE pk_type = certificate_info_private_key_type(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_size, "Line:" TOSTRING(__LINE__));
        int cmp = memcmp(pk, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_REFERENCE, pk_type, "Line:" TOSTRING(__LINE__));

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_rsa_win_succeed)
    {
        //arrange
        setup_parse_cert(strlen(TEST_RSA_CERT_WIN_EOL) + 1);

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
        setup_parse_cert(strlen(TEST_RSA_CERT_NIX_EOL) + 1);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_ecc_win_succeed)
    {
        //arrange
        setup_parse_cert(strlen(TEST_ECC_CERT_WIN_EOL) + 1);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_ECC_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_ecc_nix_succeed)
    {
        //arrange
        setup_parse_cert(strlen(TEST_ECC_CERT_NIX_EOL) + 1);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_ECC_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_no_private_key_succeed)
    {
        //arrange
        setup_parse_cert_common(strlen(TEST_ECC_CERT_WIN_EOL) + 1, false);

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_ECC_CERT_WIN_EOL, NULL, 0, PRIVATE_KEY_UNKNOWN);

        //assert
        ASSERT_IS_NOT_NULL(cert_handle);
        ASSERT_ARE_EQUAL(char_ptr, umock_c_get_expected_calls(), umock_c_get_actual_calls());

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_invalid_cert_win_succeed)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_INVALID_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_create_invalid_cert_nix_succeed)
    {
        //arrange

        //act
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_INVALID_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //assert
        ASSERT_IS_NULL(cert_handle);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

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

    TEST_FUNCTION(certificate_info_get_certificate_leaf_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_RSA_CERT_WIN_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
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

    TEST_FUNCTION(certificate_info_get_valid_from_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        int64_t valid_from = certificate_info_get_valid_from(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int64_t, RSA_CERT_VALID_FROM_TIME, valid_from);

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

    TEST_FUNCTION(certificate_info_get_valid_to_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        int64_t valid_to = certificate_info_get_valid_to(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int64_t, RSA_CERT_VALID_TO_TIME, valid_to);

        //cleanup
        certificate_info_destroy(cert_handle);
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

    TEST_FUNCTION(certificate_info_private_key_type_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        PRIVATE_KEY_TYPE type = certificate_info_private_key_type(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_PAYLOAD, type);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_private_key_type_handle_NULL_fail)
    {
        //arrange

        //act
        (void)certificate_info_private_key_type(NULL);

        //assert

        //cleanup
    }

    TEST_FUNCTION(certificate_info_get_chain_no_chain_win_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* cert_chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NULL(cert_chain);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_chain_no_chain_nix_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* cert_chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NULL(cert_chain);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_chain_win_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_CHAIN_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* cert_chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(cert_chain);
        const char *expected_chain = EXPECTED_TEST_CERT_CHAIN_WIN_EOL;
        size_t expected_len = strlen(expected_chain);
        ASSERT_ARE_EQUAL(size_t, expected_len, strlen(cert_chain));
        ASSERT_ARE_EQUAL(int, 0, strcmp(cert_chain, expected_chain));

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_chain_nix_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_CHAIN_NIX_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);
        umock_c_reset_all_calls();

        //act
        const char* cert_chain = certificate_info_get_chain(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(cert_chain);
        const char *expected_chain = EXPECTED_TEST_CERT_CHAIN_NIX_EOL;
        size_t expected_len = strlen(expected_chain);
        ASSERT_ARE_EQUAL(size_t, expected_len, strlen(cert_chain));
        ASSERT_ARE_EQUAL(int, 0, strcmp(cert_chain, expected_chain));

        //cleanup
        certificate_info_destroy(cert_handle);
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

    TEST_FUNCTION(get_common_name_test_mulitple_rsa_success)
    {
        //arrange
        const char* cert_list[] = {
            TEST_RSA_CERT_WIN_EOL,
            TEST_RSA_CERT_NIX_EOL,
            TEST_RSA_CERT_WITH_ALL_SUBJECT_FIELDS
        };

        for (size_t i = 0; i < sizeof(cert_list)/sizeof(cert_list[0]); i++)
        {
            CERT_INFO_HANDLE cert_handle = certificate_info_create(cert_list[i],
                                                                   TEST_PRIVATE_KEY,
                                                                   TEST_PRIVATE_KEY_LEN,
                                                                   PRIVATE_KEY_PAYLOAD);
            ASSERT_IS_NOT_NULL(cert_handle);

            // act
            const char* result = certificate_info_get_common_name(cert_handle);

            // assert
            ASSERT_IS_NOT_NULL(result);
            int cmp = strcmp("localhost", result);
            ASSERT_ARE_EQUAL(int, 0, cmp);

            //cleanup
            certificate_info_destroy(cert_handle);
        }
    }

    TEST_FUNCTION(get_common_name_test_mulitple_ecc_success)
    {
        //arrange
        const char* cert_list[] = {
            TEST_ECC_CERT_WIN_EOL,
            TEST_ECC_CERT_NIX_EOL
        };

        for (size_t i = 0; i < sizeof(cert_list)/sizeof(cert_list[0]); i++)
        {
            CERT_INFO_HANDLE cert_handle = certificate_info_create(cert_list[i],
                                                                   TEST_PRIVATE_KEY,
                                                                   TEST_PRIVATE_KEY_LEN,
                                                                   PRIVATE_KEY_PAYLOAD);
            ASSERT_IS_NOT_NULL(cert_handle);

            // act
            const char* result = certificate_info_get_common_name(cert_handle);

            // assert
            ASSERT_IS_NOT_NULL(result);
            int cmp = strcmp("riot-root", result);
            ASSERT_ARE_EQUAL(int, 0, cmp);

            //cleanup
            certificate_info_destroy(cert_handle);
        }
    }

    TEST_FUNCTION(get_common_name_test_mulitple_chain_success)
    {
        //arrange
        const char* cert_list[] = {
            TEST_CERT_CHAIN_WIN_EOL,
            TEST_CERT_CHAIN_NIX_EOL
        };

        for (size_t i = 0; i < sizeof(cert_list)/sizeof(cert_list[0]); i++)
        {
            CERT_INFO_HANDLE cert_handle = certificate_info_create(cert_list[i],
                                                                   TEST_PRIVATE_KEY,
                                                                   TEST_PRIVATE_KEY_LEN,
                                                                   PRIVATE_KEY_PAYLOAD);
            ASSERT_IS_NOT_NULL(cert_handle);

            // act
            const char* result = certificate_info_get_common_name(cert_handle);

            // assert
            ASSERT_IS_NOT_NULL(result);
            int cmp = strcmp("Edge Agent CA", result);
            ASSERT_ARE_EQUAL(int, 0, cmp);

            //cleanup
            certificate_info_destroy(cert_handle);
        }
    }

    TEST_FUNCTION(get_common_name_test_failed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WITH_NO_COMMON_NAME, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        // act
        const char* result = certificate_info_get_common_name(cert_handle);

        // assert
        ASSERT_IS_NULL(result);
    }

    END_TEST_SUITE(certificate_info_ut)
