# \SystemInformationApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**get_system_info**](SystemInformationApi.md#get_system_info) | **GET** /systeminfo | Return host system information.
[**get_system_resources**](SystemInformationApi.md#get_system_resources) | **GET** /systeminfo/resources | Return host resource usage (DISK, RAM, CPU).


# **get_system_info**
> ::models::SystemInfo get_system_info(api_version)
Return host system information.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]

### Return type

[**::models::SystemInfo**](SystemInfo.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_system_resources**
> ::models::SystemResources get_system_resources(api_version)
Return host resource usage (DISK, RAM, CPU).

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]

### Return type

[**::models::SystemResources**](SystemResources.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

