#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)
project(iothsm)
set (iothsm_VERSION_MAJOR 0)
set (iothsm_VERSION_MINOR 1)

#Use solution folders.
set_property(GLOBAL PROPERTY USE_FOLDERS ON)

include_directories(./azure-c-shared-utility/inc)
include_directories(. ./inc)

find_package(OpenSSL REQUIRED)
include_directories(${OPENSSL_INCLUDE_DIR})

set(source_c_files
    ./src/certificate_info.c
    ./src/constants.c
    ./src/edge_hsm_client_crypto.c
    ./src/edge_hsm_client_store.c
    ./src/edge_hsm_client_tpm.c
    ./src/edge_hsm_client_x509.c
    ./src/edge_hsm_key_interface.c
    ./src/edge_pki_openssl.c
    ./src/edge_sas_key.c
    ./src/hsm_certificate_props.c
    ./src/hsm_client_data.c
    ./src/hsm_log.c
    ./src/hsm_utils.c
)

set(source_h_files
    ./inc/certificate_info.h
    ./inc/hsm_client_data.h
    ./inc/hsm_certificate_props.h
)

if(WIN32)
    #windows needs this define
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)

    # Make warning as error
    add_definitions(/WX)

    # export functions in DLL
    set(source_c_files ${source_c_files}
        ./src/hsm_client_data.def
    )

    add_definitions(-DGB_MEASURE_MEMORY_FOR_THIS -DGB_DEBUG_ALLOC)
else()
if (run_unittests)
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g -fPIC -std=c99")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -fPIC")
else()
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g -fPIC -Wall -Werror -std=c99 ")
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -fPIC -Wall -Werror ")
    # todo enable extra warnings when azure shared util warnings are fixed
    # extra warnings
    #set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g -fPIC -Wextra -Wformat=2 -Wformat-security -std=c99 ")
    #set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -fPIC -Wextra -Wformat=2 -Wformat-security ")
    #if (CMAKE_C_COMPILER_ID STREQUAL "GNU")
    #    set (CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wtrampolines -fstack-protector-all -Wstack-protector --param ssp-buffer-size=4 ")
    #    set (CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wtrampolines -fstack-protector-all -Wstack-protector --param ssp-buffer-size=4" )
    #endif()
endif(run_unittests)
endif(WIN32)

# We want this to always be a shared library and let the dynamic linker on the
# target system find the HSM library.
if(BUILD_SHARED)
    add_library(iothsm SHARED ${source_c_files} ${source_h_files})
else()
    add_library(iothsm STATIC ${source_c_files} ${source_h_files})
endif(BUILD_SHARED)

if (run_unittests)
    enable_testing()
    set(save_ut ${run_unittests})
    set(run_unittests OFF CACHE BOOL "unittests" FORCE)
    add_subdirectory(./azure-c-shared-utility/testtools/ctest)
    add_subdirectory(./azure-c-shared-utility/testtools/testrunner)
    add_subdirectory(./azure-c-shared-utility/testtools/umock-c)
endif(run_unittests)

add_subdirectory(./azure-c-shared-utility EXCLUDE_FROM_ALL)

if (save_ut)
    set(run_unittests ${save_ut} CACHE BOOL "unittests" FORCE)
endif(save_ut)

if (${run_unittests})
    add_subdirectory(tests)
endif()

if(WIN32)
    target_link_libraries(iothsm aziotsharedutil $ENV{OPENSSL_ROOT_DIR}/lib/ssleay32.lib $ENV{OPENSSL_ROOT_DIR}/lib/libeay32.lib)
else()
     target_link_libraries(iothsm aziotsharedutil ${OPENSSL_LIBRARIES})
endif(WIN32)

install(TARGETS iothsm DESTINATION lib)

# CPack
include (InstallRequiredSystemLibraries)
set (CPACK_PACKAGE_VERSION_MAJOR "${iothsm_VERSION_MAJOR}")
set (CPACK_PACKAGE_VERSION_MINOR "${iothsm_VERSION_MINOR}")
set (CPACK_PACKAGE_NAME "libiothsm-std")

set (CPACK_GENERATOR "DEB")
set (CPACK_DEBIAN_PACKAGE_PROVIDES "libiothsm")
set (CPACK_DEBIAN_PACKAGE_MAINTAINER "Azure IoT Edge Devs <edgedevs@microsoft.com>")
set (CPACK_DEBIAN_FILE_NAME "DEB-DEFAULT")
include (CPack)
