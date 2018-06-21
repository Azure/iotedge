#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for edge_hsm_store_int
cmake_minimum_required(VERSION 2.8.11)

compileAsC11()

include_directories(../../src)

set(theseTestsName edge_hsm_store_inut)

add_definitions(-DGB_DEBUG_ALLOC)

prepare_edge_homedir(${theseTestsName})

set(${theseTestsName}_test_files
    # test file
    edge_hsm_store_int.c

    # the following files are needed when running tests using BUILD_SHARED=ON
    ../../src/edge_sas_perform_sign_with_key.c
    ../../src/edge_enc_openssl_key.c
    ../../src/edge_sas_key.c
    ../../src/edge_hsm_key_interface.c
    ../../src/edge_hsm_client_store.c
    ../../src/certificate_info.c
    ../../src/edge_pki_openssl.c
    ../../src/hsm_utils.c
    ../../src/hsm_log.c
    ../../src/constants.c
)

set(${theseTestsName}_h_files

)

build_c_test_artifacts(${theseTestsName} ON "tests/azure_c_shared_utility_tests")

if(WIN32)
    target_link_libraries(${theseTestsName}_exe iothsm aziotsharedutil $ENV{OPENSSL_ROOT_DIR}/lib/ssleay32.lib $ENV{OPENSSL_ROOT_DIR}/lib/libeay32.lib)
else()
     target_link_libraries(${theseTestsName}_exe iothsm aziotsharedutil ${OPENSSL_LIBRARIES})
endif(WIN32)

copy_iothsm_dll(${theseTestsName}_exe ${CMAKE_CURRENT_BINARY_DIR}/$(Configuration))
