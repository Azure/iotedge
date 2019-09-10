#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for certificate_info_ut
cmake_minimum_required(VERSION 2.8.11)

compileAsC11()

include_directories(../../src)

set(theseTestsName certificate_info_ut)

add_definitions(-DGB_DEBUG_ALLOC)

set(${theseTestsName}_test_files
    ${theseTestsName}.c
)

set(${theseTestsName}_c_files
    pki_mocked.c
    ../../src/hsm_log.c
)

set(${theseTestsName}_h_files

)

build_c_test_artifacts(${theseTestsName} ON "tests")

if(WIN32)
    target_link_libraries(${theseTestsName}_exe advapi32)
endif(WIN32)
