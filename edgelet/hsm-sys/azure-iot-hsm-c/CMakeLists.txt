#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)
project(iothsm)
set (iothsm_VERSION_MAJOR 1)
set (iothsm_VERSION_MINOR 0)
set (iothsm_VERSION_PATCH 8)

#Use solution folders.
set_property(GLOBAL PROPERTY USE_FOLDERS ON)

include_directories(./deps/c-shared/inc)
include_directories(./deps/c-shared/deps/azure-macro-utils-c/inc)
include_directories(./deps/c-shared/deps/umock-c/inc)
include_directories(./deps/utpm/inc)
include_directories(. ./inc)

find_package(OpenSSL REQUIRED)
include_directories(${OPENSSL_INCLUDE_DIR})

if(USE_TEST_TPM_INTERFACE_IN_MEM)
    add_definitions(-DTEST_TPM_INTERFACE_IN_MEM)
endif(USE_TEST_TPM_INTERFACE_IN_MEM)

set(source_c_files
    ./src/certificate_info.c
    ./src/constants.c
    ./src/edge_enc_openssl_key.c
    ./src/edge_hsm_client_crypto.c
    ./src/edge_hsm_client_store.c
    ./src/edge_hsm_client_x509.c
    ./src/edge_hsm_key_interface.c
    ./src/edge_openssl_common.c
    ./src/edge_sas_perform_sign_with_key.c
    ./src/edge_pki_openssl.c
    ./src/edge_sas_key.c
    ./src/hsm_certificate_props.c
    ./src/hsm_client_data.c
    ./src/hsm_client_tpm_device.c
    ./src/hsm_client_tpm_in_mem.c
    ./src/hsm_client_tpm_select.c
    ./src/hsm_log.c
    ./src/hsm_utils.c
)

set(source_h_files
    ./inc/certificate_info.h
    ./inc/hsm_client_data.h
    ./inc/hsm_certificate_props.h
    ./src/edge_sas_perform_sign_with_key.h
    ./src/hsm_client_store.h
    ./src/hsm_client_tpm_device.h
    ./src/hsm_client_tpm_in_mem.h
    ./src/hsm_constants.h
    ./src/hsm_key.h
    ./src/hsm_log.h
    ./src/hsm_utils.h
)

if(MSVC)
    #windows needs this define
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)

    # Make warning as error
    add_definitions(/WX)
    if (run_unittests)
        add_definitions(/W3)
    else()
        add_definitions(/W4)
    endif(run_unittests)

    # export functions in DLL
    set(source_c_files ${source_c_files}
        ./src/hsm_client_data.def
    )

    add_definitions(-DGB_MEASURE_MEMORY_FOR_THIS -DGB_DEBUG_ALLOC)
else()
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -g -fPIC -Wall -Werror -Wextra -Wformat=2 -Wformat-security -std=c99 ")

    # suppress clang compiler warnings complaining about calls to vsnprintf
    if(CMAKE_C_COMPILER_ID MATCHES "Clang")
        set (CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wno-error=format-nonliteral")
    endif()

    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -g -fPIC -Wall -Werror -Wextra -Wformat=2 -Wformat-security ")

    if (CMAKE_C_COMPILER_ID STREQUAL "GNU")
        set (CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wtrampolines -fstack-protector-all -Wstack-protector --param ssp-buffer-size=4 ")
        set (CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wtrampolines -fstack-protector-all -Wstack-protector --param ssp-buffer-size=4 ")
    endif()
endif(MSVC)

# We want this to always be a shared library and let the dynamic linker on the
# target system find the HSM library.
if(BUILD_SHARED)
    add_library(iothsm SHARED ${source_c_files} ${source_in_mem_store_c_files} ${source_h_files})
else()
    add_library(iothsm STATIC ${source_c_files} ${source_in_mem_store_c_files} ${source_h_files})
endif(BUILD_SHARED)

set(preinstall_iothsm_library_dll ${CMAKE_CURRENT_BINARY_DIR}/$(Configuration)/iothsm.dll)

function(copy_iothsm_dll whatIsBuilding whatIsBuildingLocation)
    if(WIN32 AND BUILD_SHARED)
        add_custom_command(TARGET ${whatIsBuilding} POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                ${preinstall_iothsm_library_dll}
                ${whatIsBuildingLocation})
    endif()
endfunction(copy_iothsm_dll)

function(prepare_edge_homedir whatIsBuilding)
    if(WIN32)
        STRING(CONCAT testOnlyHomeDir  "$ENV{Temp}" "/" "${whatIsBuilding}")
        STRING(REGEX REPLACE "\\\\" "/" testOnlyHomeDir ${testOnlyHomeDir} )
    else()
        STRING(CONCAT testOnlyHomeDir  "/tmp/" "${whatIsBuilding}")
    endif(WIN32)

    FILE(MAKE_DIRECTORY ${testOnlyHomeDir})
    add_definitions(-DTESTONLY_IOTEDGE_HOMEDIR="${testOnlyHomeDir}")
endfunction(prepare_edge_homedir)

if (run_unittests)
    enable_testing()
    set(save_ut ${run_unittests})
    set(run_unittests OFF CACHE BOOL "unittests" FORCE)
endif(run_unittests)

add_subdirectory(./deps/c-shared EXCLUDE_FROM_ALL)
add_subdirectory(./deps/utpm EXCLUDE_FROM_ALL)

if (save_ut)
    set(run_unittests ${save_ut} CACHE BOOL "unittests" FORCE)
endif(save_ut)

if(WIN32)
    target_link_libraries(iothsm aziotsharedutil utpm $ENV{OPENSSL_ROOT_DIR}/lib/ssleay32.lib $ENV{OPENSSL_ROOT_DIR}/lib/libeay32.lib advapi32)
else()
    target_link_libraries(iothsm aziotsharedutil utpm ${OPENSSL_LIBRARIES})
endif(WIN32)

if (${run_unittests})
    add_subdirectory(tests)
endif()

set_target_properties(iothsm PROPERTIES
        VERSION ${iothsm_VERSION_MAJOR}.${iothsm_VERSION_MINOR}.${iothsm_VERSION_PATCH}
        SOVERSION 1)

if(WIN32)
    install(TARGETS iothsm DESTINATION ${CMAKE_INSTALL_LIBDIR})
else()
    install(TARGETS iothsm
            ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR})
endif(WIN32)

# CPack
include (InstallRequiredSystemLibraries)

if (NOT CPACK_PACKAGE_VERSION)
    set (CPACK_PACKAGE_VERSION_MAJOR "${iothsm_VERSION_MAJOR}")
    set (CPACK_PACKAGE_VERSION_MINOR "${iothsm_VERSION_MINOR}")
    set (CPACK_PACKAGE_VERSION_PATCH "${iothsm_VERSION_PATCH}")
endif()

set (CPACK_PACKAGE_NAME "libiothsm-std")
set (CPACK_PACKAGE_DESCRIPTION_SUMMARY "Azure IoT standard mode HSM lib")

if (NOT CPACK_GENERATOR)
    set (CPACK_GENERATOR "DEB")
endif()

set (CPACK_DEBIAN_PACKAGE_PROVIDES "libiothsm")
set (CPACK_DEBIAN_PACKAGE_MAINTAINER "Azure IoT Edge Devs")
set (CPACK_DEBIAN_FILE_NAME "DEB-DEFAULT")
set (CPACK_DEBIAN_PACKAGE_GENERATE_SHLIBS "ON")
set (CPACK_DEBIAN_PACKAGE_DEPENDS "${OPENSSL_DEPENDS_SPEC}")

set (CPACK_RPM_FILE_NAME RPM-DEFAULT)
set (CPACK_RPM_PACKAGE_RELEASE_DIST On)
set (CPACK_RPM_PACKAGE_URL "http://github.com/azure/iotedge")
set (CPACK_RPM_PACKAGE_VENDOR "Microsoft Corporation")
set (CPACK_RPM_PACKAGE_DESCRIPTION "Azure IoT standard mode HSM lib")
set (CPACK_RPM_PACKAGE_LICENSE "Proprietary")
set (CPACK_RPM_PACKAGE_DEPENDS "${OPENSSL_DEPENDS_SPEC}")

include (CPack)

if (${run_unittests})
    if(WIN32)
        target_link_libraries(iothsm shell32)
    endif(WIN32)
endif()
