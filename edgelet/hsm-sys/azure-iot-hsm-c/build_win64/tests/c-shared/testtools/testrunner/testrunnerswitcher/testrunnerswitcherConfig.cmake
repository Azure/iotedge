#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

include("${CMAKE_CURRENT_LIST_DIR}/testrunnerswitcherTargets.cmake")

get_target_property(TESTRUNNERSWITCHER_INCLUDES testrunnerswitcher INTERFACE_INCLUDE_DIRECTORIES)

set(TESTRUNNERSWITCHER_INCLUDES ${TESTRUNNERSWITCHER_INCLUDES} CACHE INTERNAL "")