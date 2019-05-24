#ifndef EDGE_OPENSSL_COMMON_H
#define EDGE_OPENSSL_COMMON_H

#ifdef __cplusplus
#include <cstdbool>
#include <cstddef>
extern "C" {
#else
#include <stdbool.h>
#include <stddef.h>
#endif

#include "umock_c/umock_c_prod.h"

MOCKABLE_FUNCTION(, void, initialize_openssl);

#ifdef __cplusplus
}
#endif

#endif //EDGE_OPENSSL_COMMON_H
