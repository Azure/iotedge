#Copyright (c) Microsoft. All rights reserved.
#Licensed under the MIT license. See LICENSE file in the project root for full license information.

usePermissiveRulesForSamplesAndTests()

add_subdirectory(hsm_certificate_props_ut)
#add_subdirectory(certificate_info_ut)
add_subdirectory(edge_hsm_tpm_ut)
add_subdirectory(edge_hsm_key_interface_sas_key_ut)
add_subdirectory(edge_hsm_sas_auth_int)
#add_subdirectory(edge_hsm_enc_dec_ft)
