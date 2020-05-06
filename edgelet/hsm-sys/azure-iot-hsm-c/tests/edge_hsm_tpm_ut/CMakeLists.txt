#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for edge_hsm_tpm_ut
cmake_minimum_required(VERSION 2.8.11)

compileAsC11()

set(theseTestsName edge_hsm_tpm_ut)

include_directories(../../src)

add_definitions(-DGB_DEBUG_ALLOC)

set(${theseTestsName}_test_files
    ../../src/hsm_client_tpm_in_mem.c
    ../../src/hsm_log.c
    ../../src/constants.c
    ${theseTestsName}.c
)

set(${theseTestsName}_h_files
    ../../src/hsm_client_tpm_in_mem.h

)

build_c_test_artifacts(${theseTestsName} ON "tests/azure_c_shared_utility_tests")

if(WIN32)
    target_link_libraries(${theseTestsName}_exe advapi32)
endif(WIN32)
