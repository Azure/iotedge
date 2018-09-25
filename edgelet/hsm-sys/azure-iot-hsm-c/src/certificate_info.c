// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <time.h>
#include "certificate_info.h"

#include "azure_c_shared_utility/gballoc.h"
#include "azure_c_shared_utility/base64.h"
#include "azure_c_shared_utility/buffer_.h"
#include "azure_c_shared_utility/xlogging.h"

typedef struct CERT_DATA_INFO_TAG
{
    char* certificate_pem;
    void* private_key;
    size_t private_key_len;
    PRIVATE_KEY_TYPE private_key_type;
    uint8_t version;
    time_t not_before;
    time_t not_after;
    char* subject;
    char* issuer;
    const char* cert_chain;
    const char* first_cert_start;
    const char* first_cert_end;
    char* first_certificate;
} CERT_DATA_INFO;

typedef enum X509_ASN1_STATE_TAG
{
    STATE_INITIAL,
    STATE_TBS_CERTIFICATE,
    STATE_SIGNATURE_ALGO,
    STATE_SIGNATURE_VALUE
} X509_ASN1_STATE;

typedef enum ASN1_TYPE_TAG
{
    ASN1_BOOLEAN = 0x1,
    ASN1_INTEGER = 0x2,
    ASN1_BIT_STRING = 0x3,
    ASN1_OCTET_STRING = 0x4,
    ASN1_NULL = 0x5,
    ASN1_OBJECT_ID = 0x6,
    ASN1_UTF8_STRING = 0xC,
    ASN1_PRINTABLE_STRING = 0x13,
    ASN1_T61_STRING = 0x16,
    ASN1_UTCTIME = 0x17,
    ASN1_GENERALIZED_STRING = 0x18,
    ASN1_SEQUENCE = 0x30,
    ASN1_SET = 0x31,
    ASN1_INVALID
} ASN1_TYPE;

typedef enum TBS_CERTIFICATE_FIELD_TAG
{
    FIELD_VERSION,
    FIELD_SERIAL_NUM,
    FIELD_SIGNATURE,
    FIELD_ISSUER,
    FIELD_VALIDITY,
    FIELD_SUBJECT,
    FIELD_SUBJECT_PUBLIC_KEY_INFO,
    FIELD_ISSUER_UNIQUE_ID,
    FIELD_SUBJECT_UNIQUE_ID,
    FIELD_EXTENSIONS
} TBS_CERTIFICATE_FIELD;

typedef struct ASN1_OBJECT_TAG
{
    ASN1_TYPE type;
    size_t length;
    const unsigned char* value;
} ASN1_OBJECT;

// Construct the number of days of the start of each month
// exclude leap year (they are taken care of below)
static const int month_day[] = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };

#define ASN1_MARKER         0x30
#define LENGTH_EXTENTION    0x82
#define ASN1_TYPE_INTEGER   0x02
#define EXTENDED_LEN_FLAG   0x80
#define LEN_FLAG_COUNT      0x7F
#define TLV_OVERHEAD_SIZE   0x2
#define LENGTH_OF_VALIDITY  0x1E
#define TEMP_DATE_LENGTH    32
#define NOT_AFTER_OFFSET    15
#define TIME_FIELD_LENGTH   0x0D
#define END_HEADER_LENGTH   25 // length of end header string -----END CERTIFICATE-----
#define INVALID_TIME        -1

static BUFFER_HANDLE decode_certificate(CERT_DATA_INFO* cert_info)
{
    BUFFER_HANDLE result;
    const char* iterator = cert_info->certificate_pem;
    char* cert_base64;
    size_t cert_idx = 0;

    // Allocate enough space for the certificate,
    // no need to do append a +1 due to we're not
    // copying the headers
    size_t len = strlen(iterator);
    if ((cert_base64 = malloc(len)) == NULL)
    {
        LogError("Failure allocating base64 decoding certificate");
        result = NULL;
    }
    else
    {
        bool begin_hdr_end = false;
        int begin_hdr_len = 0;
        memset(cert_base64, 0, len);
        cert_info->first_cert_start = cert_info->certificate_pem;
        // If the cert does not begin with a '-' then
        // the certificate doesn't have a header
        if (*iterator != '-')
        {
            begin_hdr_end = true;
        }
        while (*iterator != '\0')
        {
            if (begin_hdr_end)
            {
                // Once we are in the header then, copy the cert excluding \r\n on all lines
                if (*iterator != '\r' && *iterator != '\n')
                {
                    cert_base64[cert_idx++] = *iterator;
                }
                if (*iterator == '\n' && *(iterator + 1) == '-')
                {
                    // mark the end of the first certificate including \r\n characters
                    cert_info->first_cert_end = iterator + END_HEADER_LENGTH + 1;
                    if (*(cert_info->first_cert_end) == '\r')
                    {
                        cert_info->first_cert_end++;
                    }

                    // Check to see if we have a chain embedded in the certificate
                    // if we've have more data after the END HEADER then we have a chain
                    if ((((iterator - cert_info->certificate_pem) + END_HEADER_LENGTH) + begin_hdr_len) < (int)len)
                    {
                        iterator++;
                        // Find the certificate chain here for later use
                        while (*iterator != '\0')
                        {
                            // check for end header
                            if (*iterator == '\n')
                            {
                                cert_info->cert_chain = iterator + 1;
                                break;
                            }
                            iterator++;
                        }
                    }

                    // If we encounter the \n- to signal the end header break out
                    break;
                }
            }
            else if (!begin_hdr_end && *iterator == '\n')
            {
                // Loop through the cert until we get to the \n at the end
                // of the header
                begin_hdr_end = true;
            }
            else
            {
                begin_hdr_len++;
            }
            iterator++;
        }
        result = Base64_Decoder(cert_base64);
        free(cert_base64);
    }
    return result;
}

static char* get_object_id_value(const ASN1_OBJECT* target_obj)
{
    // TODO: need to implement
    (void)target_obj;
    return NULL;
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

static time_t get_utctime_value(const unsigned char* time_value)
{
    // Check the the type
    if (*time_value != ASN1_UTCTIME)
    {
        LogError("Parse time error: Unknown time format");
        return 0;
    }
    else
    {
        return get_utc_time_from_asn_string((time_value + 2), *(time_value + 1));
    }
}

static size_t calculate_size(const unsigned char* buff, size_t* pos_change)
{
    size_t result;
    if ((buff[0] & EXTENDED_LEN_FLAG))
    {
        // We are using more than 128 bits, let see how many
        size_t num_bits = buff[0] & LEN_FLAG_COUNT;
        result = 0;
        for (size_t idx = 0; idx < num_bits; idx++)
        {
            unsigned char temp = buff[idx + 1];
            if (idx == 0)
            {
                result = temp;
            }
            else
            {
                result = (result << 8) + temp;
            }
        }
        *pos_change = num_bits + 1;
    }
    else
    {
        // The buffer is the size
        result = buff[0];
        *pos_change = 1;
    }
    return result;
}

static size_t parse_asn1_object(unsigned char* tbs_info, ASN1_OBJECT* asn1_obj)
{
    size_t idx = 0;
    size_t pos_change;
    // determine the type
    asn1_obj->type = tbs_info[idx++];
    asn1_obj->length = calculate_size(&tbs_info[idx], &pos_change);
    asn1_obj->value = &tbs_info[idx + pos_change];
    return pos_change;
}

static int parse_tbs_cert_info(unsigned char* tbs_info, size_t len, CERT_DATA_INFO* cert_info)
{
    int result = 0;
    int continue_loop = 0;
    size_t size_len;

    TBS_CERTIFICATE_FIELD tbs_field = FIELD_VERSION;
    unsigned char* iterator = tbs_info;
    ASN1_OBJECT target_obj;

    while ((iterator < tbs_info + len) && (result == 0) && (continue_loop == 0))
    {
        switch (tbs_field)
        {
        case FIELD_VERSION:
            // Version field
            if (*iterator == 0xA0) // Array type
            {
                iterator++;
                if (*iterator == 0x03) // Length of this array
                {
                    iterator++;
                    parse_asn1_object(iterator, &target_obj);
                    // Validate version
                    cert_info->version = target_obj.value[0];
                    iterator += 3;  // Increment past the array type
                    tbs_field = FIELD_SERIAL_NUM;
                }
                else
                {
                    LogError("Parse Error: Invalid version field");
                    result = __LINE__;
                }
            }
            else
            {
                // RFC 5280: Version is optional, assume version 1
                cert_info->version = 1;
                tbs_field = FIELD_SERIAL_NUM;
            }
            break;
        case FIELD_SERIAL_NUM:
            // OID
            parse_asn1_object(iterator, &target_obj);
            get_object_id_value(&target_obj);
            iterator += target_obj.length + TLV_OVERHEAD_SIZE; // Increment lenght plus type and length
            tbs_field = FIELD_SIGNATURE;
            break;
        case FIELD_SIGNATURE:
            parse_asn1_object(iterator, &target_obj);
            iterator += target_obj.length + TLV_OVERHEAD_SIZE;
            tbs_field = FIELD_ISSUER;   // Go to the next field
            break;
        case FIELD_ISSUER:
            size_len = parse_asn1_object(iterator, &target_obj);
            // adding additional OVERHEAD_SIZE on the value due to the size value not being included in the length
            iterator += target_obj.length + TLV_OVERHEAD_SIZE + (size_len - 1);
            tbs_field = FIELD_VALIDITY;   // Go to the next field
            break;
        case FIELD_VALIDITY:
            parse_asn1_object(iterator, &target_obj);
            if (target_obj.length != LENGTH_OF_VALIDITY)
            {
                result = __LINE__;
            }
            else
            {
                // Convert the ASN1 UTC format to a time
                if ((cert_info->not_before = get_utctime_value(target_obj.value)) == 0)
                {
                    result = __LINE__;
                }
                else if ((cert_info->not_after = get_utctime_value(target_obj.value + NOT_AFTER_OFFSET)) == 0)
                {
                    result = __LINE__;
                }
                else
                {
                    iterator += target_obj.length + TLV_OVERHEAD_SIZE;
                    tbs_field = FIELD_SUBJECT;   // Go to the next field
                    continue_loop = 1;
                }
            }
            break;
        case FIELD_SUBJECT:
            size_len = parse_asn1_object(iterator, &target_obj);
			// adding additional OVERHEAD_SIZE on the value due to the size value not being included in the length
            iterator += target_obj.length + TLV_OVERHEAD_SIZE + (size_len - 1);
            tbs_field = FIELD_VALIDITY;   // Go to the next field
            continue_loop = 1;
            break;
        case FIELD_SUBJECT_PUBLIC_KEY_INFO:
        case FIELD_ISSUER_UNIQUE_ID:
        case FIELD_SUBJECT_UNIQUE_ID:
        case FIELD_EXTENSIONS:
            break;
        }
    }
    return result;
}

static int parse_asn1_data(unsigned char* section, size_t len, X509_ASN1_STATE state, CERT_DATA_INFO* cert_info)
{
    int result = 0;
    for (size_t index = 0; index < len; index++)
    {
        if (section[index] == ASN1_MARKER)
        {
            index++;
            size_t offset;
            size_t section_size = calculate_size(&section[index], &offset);
            index += offset;
            result = parse_asn1_data(section+index, section_size, STATE_TBS_CERTIFICATE, cert_info);
            break;

        }
        else if (state == STATE_TBS_CERTIFICATE)
        {
            result = parse_tbs_cert_info(&section[index], len, cert_info);
            // Only parsing the TBS area of the certificate
            // Break here
            break;
        }
    }
    return result;
}

static int parse_certificate(CERT_DATA_INFO* cert_info)
{
    int result;
    BUFFER_HANDLE cert_bin = decode_certificate(cert_info);
    if (cert_bin == NULL)
    {
        LogError("Failure decoding certificate");
        result = __LINE__;
    }
    else
    {
        unsigned char* cert_buffer = BUFFER_u_char(cert_bin);
        size_t cert_buff_len = BUFFER_length(cert_bin);
        if (parse_asn1_data(cert_buffer, cert_buff_len, STATE_INITIAL, cert_info) != 0)
        {
            LogError("Failure parsing asn1 data field");
            result = __LINE__;
        }
        else
        {
            result = 0;
        }
        BUFFER_delete(cert_bin);
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

        if (cert_len == 0 || (result->certificate_pem = malloc(cert_len + 1)) == NULL)
        {
            LogError("Failure allocating certificate");
            free(result);
            result = NULL;
        }
        else
        {
            memcpy(result->certificate_pem, certificate, cert_len);
            result->certificate_pem[cert_len] = '\0';

            if (parse_certificate(result) != 0)
            {
                LogError("Failure parsing certificate");
                free(result->certificate_pem);
                free(result);
                result = NULL;
            }
            else
            {
                size_t num_bytes_first_cert = result->first_cert_end - result->first_cert_start + 1;
                if ((result->first_certificate = malloc(num_bytes_first_cert + 1)) == NULL)
                {
                    LogError("Failure allocating memory to hold the main certificate");
                    free(result->certificate_pem);
                    free(result);
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
                            free(result->first_certificate);
                            free(result->certificate_pem);
                            free(result);
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
        if (cert_info->private_key != NULL)
        {
            free(cert_info->private_key);
        }
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
        result = NULL;
    }
    return result;
}
