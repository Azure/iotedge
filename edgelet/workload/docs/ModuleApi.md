# workload\ModuleApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**list_modules**](ModuleApi.md#list_modules) | **GET** /modules | List modules.


# **list_modules**
> ::models::ModuleList list_modules(api_version)
List modules.

This returns the list of currently running modules and their statuses. 

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]

### Return type

[**::models::ModuleList**](ModuleList.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

