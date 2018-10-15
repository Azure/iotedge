#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)

compileAsC11()
set(theseTestsName hsm_certificate_props_ut)

set(${theseTestsName}_test_files
    ${theseTestsName}.c
)

set(${theseTestsName}_c_files
    ../../src/hsm_certificate_props.c
    ${SHARED_UTIL_SRC_FOLDER}/xlogging.c
    ${SHARED_UTIL_SRC_FOLDER}/consolelogger.c
    ${SHARED_UTIL_SRC_FOLDER}/../tests/real_test_files/real_crt_abstractions.c
)

set(${theseTestsName}_h_files
)

build_c_test_artifacts(${theseTestsName} ON "tests")
