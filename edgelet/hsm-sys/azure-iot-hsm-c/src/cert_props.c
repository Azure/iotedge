#include <stdlib.h>
#include <string.h>
#include "azure_c_shared_utility/gballoc.h"

#include "hsm_client_data.h"

typedef struct HSM_CERTIFICATE_PROPS_TAG *CERT_PROPS_HANDLE;

#define MAX_COUNTRY_LEN 2
#define MAX_COUNTRY_SIZE MAX_COUNTRY_LEN + 1
#define MAX_STATE_LEN 128
#define MAX_STATE_SIZE MAX_STATE_LEN + 1
#define MAX_ORGANIZATION_LEN 64
#define MAX_ORGANIZATION_SIZE MAX_ORGANIZATION_LEN + 1
#define MAX_COMMON_NAME_LEN 64
#define MAX_COMMON_NAME_SIZE MAX_COMMON_NAME_LEN + 1
#define MAX_ALIAS_LEN 64
#define MAX_ALIAS_SIZE MAX_ALIAS_LEN + 1

typedef enum CERT_PROPERTY_TYPE_TAG
{
    COUNTRY = 0,
    STATE,
    LOCALITY,
    ORGANIZATION,
    ORGANIZATION_UNIT,
    COMMON_NAME,
    ALIAS_NAME,
} CERT_PROPERTY_TYPE;

struct CERT_PROPERTY_LIMITS_TAG
{
    CERT_PROPERTY_TYPE property;
    size_t min_length;
    size_t max_length;
};
typedef struct CERT_PROPERTY_LIMITS_TAG CERT_PROPERTY_LIMITS;

static CERT_PROPERTY_LIMITS limits_array[] = {
    {COUNTRY, MAX_COUNTRY_LEN, MAX_COUNTRY_LEN},
    {STATE, 0, MAX_STATE_LEN},
    {LOCALITY, 0, MAX_STATE_LEN},
    {ORGANIZATION, 0, MAX_ORGANIZATION_LEN},
    {ORGANIZATION_UNIT, 0, MAX_ORGANIZATION_LEN},
    {COMMON_NAME, 1, MAX_COMMON_NAME_LEN},
    {ALIAS_NAME, 1, MAX_ALIAS_LEN},
};

struct HSM_CERTIFICATE_PROPS_TAG
{
    size_t validity_mins;
    CERTIFICATE_TYPE certificate_type;
    char country[MAX_COUNTRY_SIZE];
    char state[MAX_STATE_SIZE];
    char locality[MAX_STATE_SIZE];
    char organization[MAX_ORGANIZATION_SIZE];
    char organization_unit[MAX_ORGANIZATION_SIZE];
    char common_name[MAX_COMMON_NAME_SIZE];
    char issuer_alias[MAX_ALIAS_SIZE];
    char alias[MAX_ALIAS_SIZE];
};
typedef struct HSM_CERTIFICATE_PROPS_TAG HSM_CERTIFICATE_PROPS;

static int string_copy(char *dest, const char *src, size_t dest_size)
{
    int result = 1;
    size_t len = strlen(src);
    if (len < dest_size)
    {
        strncpy(dest, src, dest_size);
        result = 0;
    }
    return result;
}

static int validate_and_copy
(
    char *dest,
    const char *src,
    size_t dest_size,
    CERT_PROPERTY_TYPE type
)
{
    int result = 1;
    size_t len;
    int idx;
    for (idx = 0; idx < (sizeof(limits_array) / sizeof(limits_array[0])); idx++)
    {
        if (limits_array[idx].property == type)
        {
            len = strlen(src);
            if ((len < dest_size) &&
                (limits_array[idx].min_length <= len) &&
                (len <= limits_array[idx].max_length))
            {
                strncpy(dest, src, dest_size);
                result = 0;
                break;
            }
        }
    }
    return result;
}

CERT_PROPS_HANDLE create_certificate_props(void)
{
    return (CERT_PROPS_HANDLE)calloc(1, sizeof(HSM_CERTIFICATE_PROPS));
}

void destroy_certificate_props(CERT_PROPS_HANDLE handle)
{
    if (handle != NULL)
    {
        free(handle);
    }
}

int set_validity_in_mins(CERT_PROPS_HANDLE handle, size_t validity_mins)
{
    int result = 1;
    if ((handle != NULL) && (validity_mins > 0))
    {
        handle->validity_mins = validity_mins;
        result = 0;
    }
    return result;
}

int get_validity_in_mins(CERT_PROPS_HANDLE handle, size_t *p_validity_mins)
{
    int result = 1;
    if ((handle != NULL) && (p_validity_mins != NULL))
    {
        *p_validity_mins = handle->validity_mins;
        result = 0;
    }
    return result;
}

int set_common_name(CERT_PROPS_HANDLE handle, const char *common_name)
{
    int result = 1;
    if ((handle != NULL) && (common_name != NULL))
    {
        result = validate_and_copy(handle->common_name,
                                   common_name,
                                   MAX_COMMON_NAME_SIZE,
                                   COMMON_NAME);
    }
    return result;
}

int get_common_name(CERT_PROPS_HANDLE handle, char *common_name, size_t common_name_size)
{
    int result = 1;
    if ((handle != NULL) && (common_name != NULL))
    {
        result = string_copy(common_name, handle->common_name, common_name_size);
    }
    return result;
}

int set_issuer_alias(CERT_PROPS_HANDLE handle, const char *issuer_alias)
{
    int result = 1;
    if ((handle != NULL) && (issuer_alias != NULL))
    {
        result = validate_and_copy(handle->issuer_alias,
                                   issuer_alias,
                                   MAX_ALIAS_SIZE,
                                   ALIAS_NAME);
    }
    return result;
}

int get_issuer_alias(CERT_PROPS_HANDLE handle, char *issuer_alias, size_t alias_size)
{
    int result = 1;
    if ((handle != NULL) && (issuer_alias != NULL))
    {
        result = string_copy(issuer_alias, handle->issuer_alias, alias_size);
    }
    return result;
}

int set_alias(CERT_PROPS_HANDLE handle, const char *alias)
{
    int result = 1;
    if ((handle != NULL) && (alias != NULL))
    {
        result = validate_and_copy(handle->alias,
                                   alias,
                                   MAX_ALIAS_SIZE,
                                   ALIAS_NAME);
    }
    return result;
}

int get_alias(CERT_PROPS_HANDLE handle, char *alias, size_t alias_size)
{
    int result = 1;
    if ((handle != NULL) && (alias != NULL))
    {
        result = string_copy(alias, handle->alias, alias_size);
    }
    return result;
}

int set_certificate_type(CERT_PROPS_HANDLE handle, CERTIFICATE_TYPE type)
{
    int result = 1;
    if (handle != NULL) {
        if ((type == CERTIFICATE_TYPE_CLIENT) ||
            (type == CERTIFICATE_TYPE_SERVER) ||
            (type == CERTIFICATE_TYPE_CA)) {
            handle->certificate_type = type;
            result = 0;
        }
    }
    return result;
}

int get_certificate_type(CERT_PROPS_HANDLE handle, CERTIFICATE_TYPE *p_type)
{
    int result = 1;
    if ((handle != NULL) && (p_type != NULL)) {
        *p_type = handle->certificate_type;
        result = 0;
    }
    return result;
}
