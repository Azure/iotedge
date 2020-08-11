# workload\SecretApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**delete_secret**](SecretApi.md#delete_secret) | **DELETE** /modules/{name}/secrets/{secretId} | Delete a secret.
[**get_secret**](SecretApi.md#get_secret) | **GET** /modules/{name}/secrets/{secretId} | Get the value of a secret.
[**set_secret**](SecretApi.md#set_secret) | **PUT** /modules/{name}/secrets/{secretId} | Set the value of a secret.


# **delete_secret**
> delete_secret(api_version, name, secret_id)
Delete a secret.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The module name. (urlencoded) | 
  **secret_id** | **String**| The name of the secret. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_secret**
> String get_secret(api_version, name, secret_id)
Get the value of a secret.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The module name. (urlencoded) | 
  **secret_id** | **String**| The name of the secret. (urlencoded) | 

### Return type

**String**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **set_secret**
> set_secret(api_version, name, secret_id, value)
Set the value of a secret.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The module name. (urlencoded) | 
  **secret_id** | **String**| The name of the secret. (urlencoded) | 
  **value** | **String**| The value of the secret. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

