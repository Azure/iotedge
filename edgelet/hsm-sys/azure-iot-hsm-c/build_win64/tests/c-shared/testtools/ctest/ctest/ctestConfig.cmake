#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

include("${CMAKE_CURRENT_LIST_DIR}/ctestTargets.cmake")

get_target_property(CTEST_INCLUDES ctest INTERFACE_INCLUDE_DIRECTORIES)

set(CTEST_INCLUDES ${CTEST_INCLUDES} CACHE INTERNAL "")