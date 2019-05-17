// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <time.h>

#include <openssl/pem.h>
#include <openssl/x509v3.h>

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/xlogging.h"

#include "certificate_info.h"

typedef struct CERT_DATA_INFO_TAG
{
    char* certificate_pem;
    void* private_key;
    size_t private_key_len;
    PRIVATE_KEY_TYPE private_key_type;
    uint8_t version;
    time_t not_before;
    time_t not_after;
    const char* cert_chain;
    const char* first_cert_start;
    const char* first_cert_end;
    char* first_certificate;
    char* common_name;
} CERT_DATA_INFO;

#define TEMP_DATE_LENGTH    32
#define TIME_FIELD_LENGTH   0x0D

// Construct the number of days of the start of each month
// exclude leap year (they are taken care of below)
static const int month_day[] = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };

// https://tools.ietf.org/html/rfc5280#appendix-A
#define MAX_LEN_COMMON_NAME 64

struct CERT_MARKER_TAG {
    const char *start;
    const char *end;
};
typedef struct CERT_MARKER_TAG CERT_MARKER;

const char* BEGIN_CERT_MARKER = "-----BEGIN CERTIFICATE-----";
size_t BEGIN_CERT_MARKER_LEN = sizeof(BEGIN_CERT_MARKER) - 1;
const char* END_CERT_MARKER = "-----END CERTIFICATE-----";
size_t END_CERT_MARKER_LEN = sizeof(END_CERT_MARKER_LEN) - 1;

// openssl ASN1 time format defines
#define ASN1_TIME_STRING_UTC_FORMAT 0x17
#define ASN1_TIME_STRING_UTC_LEN 13

static void extract_first_cert_and_chain
(
    const char* pem_chain,
    CERT_MARKER* first_cert,
    CERT_MARKER* chain_cert
)
{
    const char *iterator = pem_chain;
    size_t pem_len = strlen(pem_chain);
    const char* pem_end = pem_chain + pem_len - 1;
    const char* first_start = NULL;
    const char* first_end = NULL;
    const char* chain_start = NULL;
    const char* chain_end = NULL;
    bool first_cert_begin_marker_found = false;
    bool first_cert_found = false;

    while (*iterator != '\0')
    {
        const char *ptr;

        if (!first_cert_found)
        {
            // check for first cert
            if (!first_cert_begin_marker_found)
            {
                if ((ptr = strstr(iterator, BEGIN_CERT_MARKER)) != NULL)
                {
                    first_start = ptr;
                    first_cert_begin_marker_found = true;
                    iterator = ptr;
                }
            }
            else
            {
                if ((ptr = strstr(iterator, END_CERT_MARKER)) != NULL)
                {
                    first_end = ptr + END_CERT_MARKER_LEN - 1;
                    if ((first_end + 1 <= pem_end) && (*(first_end + 1) == '\r'))
                    {
                        first_end++;
                    }
                    if ((first_end + 1 <= pem_end) && (*(first_end + 1) == '\n'))
                    {
                        first_end++;
                    }
                    first_cert_found = true;
                    first_cert_begin_marker_found = true;
                    iterator = first_end;
                }
            }
        }
        else
        {
            // now find the start and end of the chain
            if ((ptr = strstr(iterator, BEGIN_CERT_MARKER)) != NULL)
            {
                chain_start = ptr;
                chain_end = pem_end;
                break;
            }
        }
        iterator++;
    }

    if (first_start && first_end)
    {
        first_cert->start = first_start;
        first_cert->end = first_end;
    }
    if (chain_start && chain_end)
    {
        chain_cert->start = chain_start;
        chain_cert->end = chain_end;
    }
}

static time_t tm_to_utc(const struct tm *tm)
{
    // Most of the calculation is easy; leap years are the main difficulty.
    int month = tm->tm_mon % 12;
    int year = tm->tm_year + tm->tm_mon / 12;
    if (month < 0) // handle negative values (% 12 are still negative).
    {
        month += 12;
        --year;
    }

    // This is the number of Februaries since 1900.
    const int year_for_leap = (month > 1) ? year + 1 : year;

    // Construct the UTC value
    time_t result = tm->tm_sec                      // Seconds
        + 60 * (tm->tm_min                          // Minute = 60 seconds
            + 60 * (tm->tm_hour                         // Hour = 60 minutes
                + 24 * (month_day[month] + tm->tm_mday - 1  // Day = 24 hours
                    + 365 * (year - 70)                         // Year = 365 days
                    + (year_for_leap - 69) / 4                  // Every 4 years is     leap...
                    - (year_for_leap - 1) / 100                 // Except centuries...
                    + (year_for_leap + 299) / 400)));           // Except 400s.
    return result < 0 ? -1 : result;
}

time_t get_utc_time_from_asn_string(const unsigned char *time_value, size_t length)
{
    time_t result;

    if (time_value == NULL)
    {
        LogError("Parse time error: Invalid time_value buffer");
        result = 0;
    }
    else if (length != TIME_FIELD_LENGTH)
    {
        LogError("Parse time error: Invalid length field");
        result = 0;
    }
    else
    {
        char temp_value[TEMP_DATE_LENGTH];
        size_t temp_idx = 0;
        struct tm target_time;
        uint32_t numeric_val;

        memset(&target_time, 0, sizeof(target_time));
        memset(temp_value, 0, TEMP_DATE_LENGTH);
        // Don't evaluate the Z at the end of the UTC time field
        for (size_t index = 0; index < TIME_FIELD_LENGTH - 1; index++)
        {
            temp_value[temp_idx++] = time_value[index];
            switch (index)
            {
            case 1:
                numeric_val = atol(temp_value) + 100;
                target_time.tm_year = numeric_val;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            case 3:
                numeric_val = atol(temp_value);
                target_time.tm_mon = numeric_val - 1;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            case 5:
                numeric_val = atol(temp_value);
                target_time.tm_mday = numeric_val;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            case 7:
                numeric_val = atol(temp_value);
                target_time.tm_hour = numeric_val;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            case 9:
                numeric_val = atol(temp_value);
                target_time.tm_min = numeric_val;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            case 11:
                numeric_val = atol(temp_value);
                target_time.tm_sec = numeric_val;
                memset(temp_value, 0, TEMP_DATE_LENGTH);
                temp_idx = 0;
                break;
            }
        }
        result = tm_to_utc(&target_time);
    }

    return result;
}

static int parse_common_name(CERT_DATA_INFO* cert_info, X509* x509_cert)
{
    int result;
    size_t cn_size = MAX_LEN_COMMON_NAME + 1;
    char *cn;
    /*const*/ X509_NAME* subj_name;

    if ((subj_name = X509_get_subject_name(x509_cert)) == NULL)
    {
        LogError("Failure obtaining certificate subject name");
        result = __FAILURE__;
    }
    else if ((cn = malloc(cn_size)) == NULL)
    {
        LogError("Could not allocate memory for common name");
        result = __FAILURE__;
    }
    else
    {
        memset(cn, cn_size, 0);
        if (X509_NAME_get_text_by_NID(subj_name, NID_commonName, cn, cn_size) == -1)
        {
            LogError("Failure X509_NAME_get_text_by_NID for field 'CN'");
            free(cn);
            result = __FAILURE__;
        }
        else
        {
            cert_info->common_name = cn;
            result = 0;
        }
    }

    return result;
}

static int parse_validity_timestamps(CERT_DATA_INFO* cert_info, X509* x509_cert)
{
    int result;
    time_t not_after, not_before;
    ASN1_TIME *exp_asn1;

    exp_asn1 = X509_get_notAfter(x509_cert);
    if ((exp_asn1->type != ASN1_TIME_STRING_UTC_FORMAT) &&
        (exp_asn1->length != ASN1_TIME_STRING_UTC_LEN))
    {
        LogError("Unsupported not after time format in certificate");
        result = __FAILURE__;
    }
    else if ((not_after = get_utc_time_from_asn_string(exp_asn1->data, exp_asn1->length)) == 0)
    {
        LogError("Could not parse not after timestamp from certificate");
        result = __FAILURE__;
    }
    else
    {
        exp_asn1 = X509_get_notBefore(x509_cert);
        if ((exp_asn1->type != ASN1_TIME_STRING_UTC_FORMAT) &&
            (exp_asn1->length != ASN1_TIME_STRING_UTC_LEN))
        {
            LogError("Unsupported not before time format in certificate");
            result = __FAILURE__;
        }
        else if ((not_before = get_utc_time_from_asn_string(exp_asn1->data, exp_asn1->length)) == 0)
        {
            LogError("Could not parse not before timestamp from certificate");
            result = __FAILURE__;
        }
        else
        {
            cert_info->not_after = not_after;
            cert_info->not_before = not_before;
            result = 0;
        }
    }

    return result;
}

static X509* load_certificate(const char* certificate)
{
    X509* result;
    BIO* bio = BIO_new(BIO_s_mem());
    BIO_write(bio, certificate, strlen(certificate));
    result = PEM_read_bio_X509(bio, NULL, NULL, NULL);
    return result;
}

static int parse_certificate_details(CERT_DATA_INFO* cert_info)
{
    int result;
    X509* x509_cert;

    if ((x509_cert = load_certificate(cert_info->certificate_pem)) == NULL)
    {
        LogError("Could not create X509 object from certificate");
        result = __FAILURE__;
    }
    else if (parse_validity_timestamps(cert_info, x509_cert) != 0)
    {
        LogError("Could not obtain validity timestamps from certificate");
        result = __FAILURE__;
    }
    else if (parse_common_name(cert_info, x509_cert) != 0)
    {
        LogError("Could not obtain common name from certificate");
        result = __FAILURE__;
    }
    else
    {
        result = 0;
    }

    return result;
}

static int parse_certificate(CERT_DATA_INFO* cert_info)
{
    int result;

    CERT_MARKER first_cert = { NULL, NULL };
    CERT_MARKER chain_cert = { NULL, NULL };

    extract_first_cert_and_chain(cert_info->certificate_pem, &first_cert, &chain_cert);

    if ((first_cert.start == NULL) || (first_cert.end == NULL))
    {
        LogError("Failure obtaining first certificate");
        result = __LINE__;
    }
    else
    {
        if (parse_certificate_details(cert_info) != 0)
        {
            LogError("Failure obtaining first certificate");
            result = __LINE__;
        }
        else
        {
            cert_info->first_cert_start = first_cert.start;
            cert_info->first_cert_end = first_cert.end;
            if ((chain_cert.start != NULL) && (chain_cert.end != NULL))
            {
                cert_info->cert_chain = chain_cert.start;
            }
        }
    }

    return result;
}

CERT_INFO_HANDLE certificate_info_create(const char* certificate, const void* private_key, size_t priv_key_len, PRIVATE_KEY_TYPE pk_type)
{
    CERT_DATA_INFO* result;
    size_t cert_len;

    if (certificate == NULL)
    {
        LogError("Invalid certificate parameter specified");
        result = NULL;
    }
    else if ((cert_len = strlen(certificate)) == 0)
    {
        LogError("Empty certificate string provided");
        result = NULL;
    }
    else if ((private_key != NULL) && (priv_key_len == 0))
    {
        LogError("Invalid private key buffer parameters specified");
        result = NULL;
    }
    else if ((private_key != NULL) &&
             (pk_type != PRIVATE_KEY_PAYLOAD) &&
             (pk_type != PRIVATE_KEY_REFERENCE))
    {
        LogError("Invalid private key type specified");
        result = NULL;
    }
    else if ((private_key == NULL) && (pk_type != PRIVATE_KEY_UNKNOWN))
    {
        LogError("Invalid private key type specified");
        result = NULL;
    }
    else if ((private_key == NULL) && (priv_key_len != 0))
    {
        LogError("Invalid private key length specified");
        result = NULL;
    }
    else if ((result = (CERT_DATA_INFO*)malloc(sizeof(CERT_DATA_INFO))) == NULL)
    {
        LogError("Failure allocating certificate info");
    }
    else
    {
        memset(result, 0, sizeof(CERT_DATA_INFO));

        if (cert_len == 0 || (result->certificate_pem = (char*)malloc(cert_len + 1)) == NULL)
        {
            LogError("Failure allocating certificate");
            certificate_info_destroy(result);
            result = NULL;
        }
        else
        {
            memcpy(result->certificate_pem, certificate, cert_len);
            result->certificate_pem[cert_len] = '\0';

            if (parse_certificate(result) != 0)
            {
                LogError("Failure parsing certificate");
                certificate_info_destroy(result);
                result = NULL;
            }
            else
            {
                size_t num_bytes_first_cert = result->first_cert_end - result->first_cert_start + 1;
                if ((result->first_certificate = (char*)malloc(num_bytes_first_cert + 1)) == NULL)
                {
                    LogError("Failure allocating memory to hold the main certificate");
                    certificate_info_destroy(result);
                    result = NULL;
                }
                else
                {
                    memcpy(result->first_certificate, result->first_cert_start, num_bytes_first_cert);
                    result->first_certificate[num_bytes_first_cert] = 0;
                    result->private_key_type = PRIVATE_KEY_UNKNOWN;
                    if (private_key != NULL)
                    {
                        if ((result->private_key = malloc(priv_key_len)) == NULL)
                        {
                            LogError("Failure allocating private key");
                            certificate_info_destroy(result);
                            result = NULL;
                        }
                        else
                        {
                            memcpy(result->private_key, private_key, priv_key_len);
                            result->private_key_len = priv_key_len;
                            result->private_key_type = pk_type;
                        }
                    }
                }
            }
        }
    }
    return result;
}

void certificate_info_destroy(CERT_INFO_HANDLE handle)
{
    CERT_DATA_INFO* cert_info = (CERT_DATA_INFO*)handle;
    if (cert_info != NULL)
    {
        free(cert_info->first_certificate);
        free(cert_info->certificate_pem);
        free(cert_info->private_key);
        free(cert_info->common_name);
        memset(cert_info, sizeof(CERT_DATA_INFO), 0);
        free(cert_info);
    }
}

const char* certificate_info_get_leaf_certificate(CERT_INFO_HANDLE handle)
{
    const char* result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = NULL;
    }
    else
    {
        result = handle->first_certificate;
    }
    return result;
}

const char* certificate_info_get_certificate(CERT_INFO_HANDLE handle)
{
    const char* result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = NULL;
    }
    else
    {
        result = handle->certificate_pem;
    }
    return result;
}

const void* certificate_info_get_private_key(CERT_INFO_HANDLE handle, size_t* priv_key_len)
{
    void* result;
    if (handle == NULL || priv_key_len == NULL)
    {
        LogError("Invalid parameter specified");
        result = NULL;
    }
    else
    {
        result = handle->private_key;
        *priv_key_len = handle->private_key_len;
    }
    return result;
}

int64_t certificate_info_get_valid_from(CERT_INFO_HANDLE handle)
{
    int64_t result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = 0;
    }
    else
    {
        result = handle->not_before;
    }
    return result;
}

int64_t certificate_info_get_valid_to(CERT_INFO_HANDLE handle)
{
    int64_t result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = 0;
    }
    else
    {
        result = handle->not_after;
    }
    return result;
}

PRIVATE_KEY_TYPE certificate_info_private_key_type(CERT_INFO_HANDLE handle)
{
    PRIVATE_KEY_TYPE result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = PRIVATE_KEY_UNKNOWN;
    }
    else
    {
        result = handle->private_key_type;
    }
    return result;
}

const char* certificate_info_get_chain(CERT_INFO_HANDLE handle)
{
    const char* result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = 0;
    }
    else
    {
        result = handle->cert_chain;
    }
    return result;
}

const char* certificate_info_get_issuer(CERT_INFO_HANDLE handle)
{
    const char* result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = 0;
    }
    else
    {
        result = NULL;
    }
    return result;
}

const char* certificate_info_get_common_name(CERT_INFO_HANDLE handle)
{
    const char* result;
    if (handle == NULL)
    {
        LogError("Invalid parameter specified");
        result = 0;
    }
    else
    {
        result = handle->common_name;
    }
    return result;
}
