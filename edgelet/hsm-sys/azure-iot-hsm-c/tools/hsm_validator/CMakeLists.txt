#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

cmake_minimum_required(VERSION 2.8.11)
project(hsm_validation_runner)

set(hsm_library "" CACHE STRING "Full path to the static hsm library that will be validated")

option(exclude_x509 "Exclude the validation of x509 interface" OFF)
option(exclude_tpm "Exclude the validation of TPM interface" OFF)

if ("${hsm_library}" STREQUAL "")
    message(FATAL_ERROR "The HSM validation application must be supplied an HSM library to link to.  Please provide -Dhsm_library=<path to library>")
endif()

if (${exclude_x509} AND ${exclude_tpm})
    message(FATAL_ERROR "You must not exculde x509 and tpm interfaces.")
endif()


set(HSM_INCLUDE_DIR "../../inc")

include_directories(${HSM_INCLUDE_DIR} .)

set(source_c_files
    ./hsm_validation_runner.c
    ./v0_0_1/hsm_v0_0_1_validation.c
    ./v0_0_2/hsm_v0_0_2_validation.c
    ./v0_0_2/validate_crypto.c
)

set(source_h_files
    ./v0_0_1/hsm_v0_0_1_validation.h
    ./v0_0_2/hsm_v0_0_2_validation.h
)

if (NOT ${exclude_x509})
    add_definitions(-DUSE_X509_INTERFACE)
    set(source_c_files ${source_c_files}
        ./v0_0_2/validate_x509.c
    )
endif()

if (NOT ${exclude_tpm})
    add_definitions(-DUSE_TPM_INTERFACE)
    set(source_c_files ${source_c_files}
        ./v0_0_2/validate_tpm.c
    )
endif()

IF(WIN32)
    #windows needs this define
    add_definitions(-D_CRT_SECURE_NO_WARNINGS)

    # Make warning as error
    add_definitions(/WX)
ELSE()
    # Make warning as error
    set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Werror -pedantic -Wno-variadic-macros -fPIC")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wall -Werror -pedantic -Wno-variadic-macros -fPIC")
ENDIF(WIN32)

add_executable(hsm_validation_runner ${source_c_files})
target_link_libraries(hsm_validation_runner ${hsm_library} azure-iot-hsm-c aziotsharedutil)
