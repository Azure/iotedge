#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

#this is CMakeLists.txt for edge_hsm_crypto_int
cmake_minimum_required(VERSION 2.8.11)

compileAsC11()

set(theseTestsName edge_hsm_crypto_int)

add_definitions(-DGB_DEBUG_ALLOC)

set(${theseTestsName}_test_files
    ${theseTestsName}.c
)

set(${theseTestsName}_h_files

)

build_c_test_artifacts(${theseTestsName} ON "tests/azure_c_shared_utility_tests")

target_link_libraries(${theseTestsName}_exe iothsm aziotsharedutil)
