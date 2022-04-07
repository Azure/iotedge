// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/crt_abstractions.h"
#include "azure_macro_utils/macro_utils.h"
#include "testrunnerswitcher.h"
#include "umock_c/umock_c.h"

//#############################################################################
// Interface(s) under test
//#############################################################################
#include "certificate_info.h"

//#############################################################################
// Test defines and data
//#############################################################################

static TEST_MUTEX_HANDLE g_testByTest;

MU_DEFINE_ENUM_STRINGS(UMOCK_C_ERROR_CODE, UMOCK_C_ERROR_CODE_VALUES)

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

// this is invalid because this is not a certificate, rather a CSR
static const char* TEST_INVALID_CERT_WIN_EOL =
"-----BEGIN CERTIFICATE REQUEST-----""\r\n"
"MIIBIjCByAIBADBmMQswCQYDVQQGEwJVUzELMAkGA1UECAwCV0ExEDAOBgNVBAcMB1JlZG1vbmQxITAfBgNVBAoMGEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDEVMBMGA1UEAwwMUHJvdl9yZXF1ZXN0MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEdgUgbY2fVlM1Xr6P6B/E+yfT539BCzd4jBuoIyUYncnO5K0Qxyz8zC/V7z+iGQzB7jF799pkJoLtVPUhXoaLjqAAMAoGCCqGSM49BAMCA0kAMEYCIQCVfcLe+lNdUZtGxe4ZcxNcmQylnFRH9/ZCbyWWruROiAIhAK2OF66q5mFzCtZ8OE7KgffB3cBUCf/xZdUda9dH9Onp""\r\n"
"-----END CERTIFICATE REQUEST-----\r\n";

// this is invalid because this is not a certificate, rather a CSR
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
// Test hooks
//#############################################################################

static void test_hook_on_umock_c_error(UMOCK_C_ERROR_CODE error_code)
{
    char temp_str[256];
    (void)snprintf(temp_str, sizeof(temp_str), "umock_c reported error :%s", MU_ENUM_TO_STRING(UMOCK_C_ERROR_CODE, error_code));
    ASSERT_FAIL(temp_str);
}

//#############################################################################
// Test cases
//#############################################################################
BEGIN_TEST_SUITE(certificate_info_func_tests)

    TEST_SUITE_INITIALIZE(TestClassInitialize)
    {
        g_testByTest = TEST_MUTEX_CREATE();
        ASSERT_IS_NOT_NULL(g_testByTest);
        umock_c_init(test_hook_on_umock_c_error);
    }

    TEST_SUITE_CLEANUP(TestClassCleanup)
    {
        TEST_MUTEX_DESTROY(g_testByTest);
    }

    TEST_FUNCTION_INITIALIZE(TestMethodInitialize)
    {
        if (TEST_MUTEX_ACQUIRE(g_testByTest))
        {
            ASSERT_FAIL("Mutex is ABANDONED. Failure in test framework.");
        }
    }

    TEST_FUNCTION_CLEANUP(TestMethodCleanup)
    {
        TEST_MUTEX_RELEASE(g_testByTest);
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
        ASSERT_ARE_EQUAL(size_t, 0, pk_size, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_UNKNOWN, pk_type, "Line:" MU_TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_size, "Line:" MU_TOSTRING(__LINE__));
        int cmp = memcmp(pk, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_PAYLOAD, pk_type, "Line:" MU_TOSTRING(__LINE__));

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
        ASSERT_ARE_EQUAL(size_t, TEST_PRIVATE_KEY_LEN, pk_size, "Line:" MU_TOSTRING(__LINE__));
        int cmp = memcmp(pk, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN);
        ASSERT_ARE_EQUAL(int, 0, cmp, "Line:" MU_TOSTRING(__LINE__));
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_REFERENCE, pk_type, "Line:" MU_TOSTRING(__LINE__));

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

    TEST_FUNCTION(certificate_info_get_certificate_leaf_succeed)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //act
        const char* certificate = certificate_info_get_leaf_certificate(cert_handle);

        //assert
        ASSERT_IS_NOT_NULL(certificate);
        ASSERT_ARE_EQUAL(char_ptr, TEST_RSA_CERT_WIN_EOL, certificate);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_valid_from_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //act
        int64_t valid_from = certificate_info_get_valid_from(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int64_t, RSA_CERT_VALID_FROM_TIME, valid_from);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_private_key_type_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

        //act
        PRIVATE_KEY_TYPE type = certificate_info_private_key_type(cert_handle);

        //assert
        ASSERT_ARE_EQUAL(int, PRIVATE_KEY_PAYLOAD, type);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

    TEST_FUNCTION(certificate_info_get_chain_no_chain_win_success)
    {
        //arrange
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_RSA_CERT_WIN_EOL, TEST_PRIVATE_KEY, TEST_PRIVATE_KEY_LEN, PRIVATE_KEY_PAYLOAD);

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
        CERT_INFO_HANDLE cert_handle = certificate_info_create(TEST_CERT_WITH_NO_COMMON_NAME,
                                                               TEST_PRIVATE_KEY,
                                                               TEST_PRIVATE_KEY_LEN,
                                                               PRIVATE_KEY_PAYLOAD);

        // act
        const char* result = certificate_info_get_common_name(cert_handle);

        // assert
        ASSERT_IS_NULL(result);

        //cleanup
        certificate_info_destroy(cert_handle);
    }

END_TEST_SUITE(certificate_info_func_tests)
