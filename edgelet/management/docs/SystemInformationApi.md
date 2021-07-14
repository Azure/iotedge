# \SystemInformationApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**get_support_bundle**](SystemInformationApi.md#get_support_bundle) | **Get** /systeminfo/supportbundle | Return zip of support bundle.
[**get_system_info**](SystemInformationApi.md#get_system_info) | **Get** /systeminfo | Return host system information.
[**get_system_resources**](SystemInformationApi.md#get_system_resources) | **Get** /systeminfo/resources | Return host resource usage (DISK, RAM, CPU).


# **get_support_bundle**
> get_support_bundle(api_version, optional)
Return zip of support bundle.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
 **optional** | **map[string]interface{}** | optional parameters | nil if no parameters

### Optional Parameters
Optional parameters are passed through a map[string]interface{}.

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **api_version** | **String**| The version of the API. | [default to 2018-06-28]
 **since** | **String**| Duration to get logs from. Can be relative (1d, 10m, 1h30m etc.) or absolute (unix timestamp or rfc 3339) | 
 **host** | **String**| Path to the management host | 
 **iothub_hostname** | **String**| Hub to use when calling iotedge check | 
 **edge_runtime_only** | **bool**| Exclude customer module logs | [default to false]

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/zip

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

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

