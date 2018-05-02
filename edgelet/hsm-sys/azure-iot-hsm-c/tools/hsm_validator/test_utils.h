// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#ifndef TEST_UTILS_H
#define TEST_UTILS_H

#include <stdio.h>
#include <stdlib.h>

#define S(x) #x
#define S_(x) S(x)
#define __SLINE__ S_(__LINE__)
#define ASSERT(expr) { if (!(expr)) { \
    (void)printf("Assertion failed at " __FILE__ ", line " __SLINE__ ":\n" #expr "\n"); \
    return 1; } }

typedef struct RECORD_RESULTS
{
    size_t passed;
    size_t failed;
} RECORD_RESULTS;

#define INIT_RECORD struct RECORD_RESULTS results = { 0, 0 }
#define ADD_RECORD(rec) { \
    RECORD_RESULTS tmp = rec; \
    results.passed += tmp.passed; \
    results.failed += tmp.failed; \
}
#define PRINT_RECORD (void)printf("%zu failed, %zu passed\n", results.failed, results.passed)
#define RECORD(result) { result == 0 ? ++results.passed : ++results.failed; }
#define RETURN_RECORD return results
#define RETURN_EMPTY_RECORD INIT_RECORD; RETURN_RECORD
#define RETURN_FAILED_RECORD return results.failed

#endif // TEST_UTILS_H