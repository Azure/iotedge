#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for device_auth_ut
cmake_minimum_required(VERSION 2.8.11)

include_directories(../../src)

compileAsC11()
set(theseTestsName hsm_client_tpm_ut)

set(${theseTestsName}_test_files
    ${theseTestsName}.c
)

set(${theseTestsName}_c_files
    ../../src/hsm_client_tpm_device.c
    ../../src/hsm_log.c
    ../../src/constants.c
)

set(${theseTestsName}_h_files
    ../../src/hsm_client_tpm_device.h
)

build_c_test_artifacts(${theseTestsName} ON "tests")

if(WIN32)
    target_link_libraries(${theseTestsName}_exe advapi32)
endif(WIN32)
