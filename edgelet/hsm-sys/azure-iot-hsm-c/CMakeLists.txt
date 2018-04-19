#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)
project(iothsm)

#Use solution folders.
set_property(GLOBAL PROPERTY USE_FOLDERS ON)

#find_package(OpenSSL REQUIRED)
include_directories(. ./inc)

set(source_c_files
    ./src/hsm_client_data.c
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

ELSE()
    # Make warning as error
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Werror -fPIC")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -fPIC")
ENDIF(WIN32)

# We want this to always be a shared library and let the dynamic linker on the 
# target system find the HSM library.
add_library(iothsm SHARED ${source_c_files} ${source_h_files})
install(TARGETS iothsm DESTINATION .)

add_subdirectory(./azure-c-shared-utility)
