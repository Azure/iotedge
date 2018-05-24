// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "azure_c_shared_utility/umock_c_prod.h"

MOCKABLE_FUNCTION(,int, perform_sign_with_key, const unsigned char *, key, size_t,  key_len, 
                                  const unsigned char *, data_to_be_signed, size_t, data_to_be_signed_size, 
                                  unsigned char **, digest, size_t *, digest_size);

