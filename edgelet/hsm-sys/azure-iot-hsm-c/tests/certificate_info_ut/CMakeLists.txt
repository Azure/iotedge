#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)

compileAsC11()
set(theseTestsName certificate_info_ut)

set(${theseTestsName}_test_files
    ${theseTestsName}.c
)

set(${theseTestsName}_c_files
    ${SHARED_UTIL_REAL_TEST_FOLDER}/real_base64.c
    ${SHARED_UTIL_REAL_TEST_FOLDER}/real_buffer.c
    ../../src/certificate_info.c
)

set(${theseTestsName}_h_files
)

build_c_test_artifacts(${theseTestsName} ON "tests")
