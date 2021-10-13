#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for certificate_info_func_tests
cmake_minimum_required(VERSION 2.8.12)

compileAsC11()

include_directories(../../src ../test_utils)

# todo change to _int after making integration tests work with c-utility tests
set(theseTestsName certificate_info_func_utests)

add_definitions(-DGB_DEBUG_ALLOC)

set(${theseTestsName}_test_files
    # test file
    certificate_info_ft.c

    # the following files are needed when running tests using BUILD_SHARED=ON
    ../../src/certificate_info.c
    ../../src/hsm_log.c
)

set(${theseTestsName}_h_files

)

build_c_test_artifacts(${theseTestsName} ON "tests/azure_c_shared_utility_tests")

if(WIN32)
    target_link_libraries(${theseTestsName}_exe iothsm aziotsharedutil $ENV{OPENSSL_ROOT_DIR}/lib/libssl.lib $ENV{OPENSSL_ROOT_DIR}/lib/libcrypto.lib)
else()
     target_link_libraries(${theseTestsName}_exe iothsm aziotsharedutil ${OPENSSL_LIBRARIES})
endif(WIN32)

copy_iothsm_dll(${theseTestsName}_exe ${CMAKE_CURRENT_BINARY_DIR}/$(Configuration))
