#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)
project(iothsm)

#Use solution folders.
set_property(GLOBAL PROPERTY USE_FOLDERS ON)

include_directories(./azure-c-shared-utility/inc)
include_directories(. ./inc)

set(source_c_files
    ./src/cert_props.c
    ./src/edge_hsm_client_crypto.c
    ./src/edge_hsm_client_store.c
    ./src/edge_hsm_client_tpm.c
    ./src/edge_hsm_client_x509.c
    ./src/edge_hsm_key_interface.c
    ./src/edge_sas_key.c
    ./src/hsm_log.c
)

set(source_h_files
    ./inc/hsm_client_data.h
)


IF(WIN32)
    #windows needs this define
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)

    # Make warning as error
    add_definitions(/WX)

    # export functions in DLL
    set(source_c_files ${source_c_files}
        ./src/hsm_client_data.def
    )

    add_definitions(-DGB_MEASURE_MEMORY_FOR_THIS -DGB_DEBUG_ALLOC)
ELSE()
    set(source_c_files ${source_c_files}
        ./src/edge_cert_key.c
    )
    find_package(OpenSSL REQUIRED)
    include_directories(${OPENSSL_INCLUDE_DIR})
    # Make warning as error
    # todo add -Wall -Werror ...
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g -fPIC -std=c99")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -fPIC")
ENDIF(WIN32)

# We want this to always be a shared library and let the dynamic linker on the
# target system find the HSM library.
IF(BUILD_SHARED)
    add_library(iothsm SHARED ${source_c_files} ${source_h_files})
ELSE()
    add_library(iothsm STATIC ${source_c_files} ${source_h_files})
ENDIF(BUILD_SHARED)

add_subdirectory(./azure-c-shared-utility)

if (${run_unittests})
    add_subdirectory(test)
endif()

IF(WIN32)
     target_link_libraries(iothsm aziotsharedutil)
ELSE()
     target_link_libraries(iothsm aziotsharedutil ${OPENSSL_LIBRARIES})
ENDIF(WIN32)
install(TARGETS iothsm DESTINATION .)
