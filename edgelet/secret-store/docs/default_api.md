# default_api

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
**DeleteSecret**](default_api.md#DeleteSecret) | **DELETE** /{id} | 
**GetSecret**](default_api.md#GetSecret) | **GET** /{id} | 
**PullSecret**](default_api.md#PullSecret) | **POST** /{id} | 
**RefreshSecret**](default_api.md#RefreshSecret) | **PATCH** /{id} | 
**SetSecret**](default_api.md#SetSecret) | **PUT** /{id} | 


# **DeleteSecret**
> DeleteSecret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to "2020-07-22".to_string()]
  **id** | **String**| The secret ID. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json, 

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **GetSecret**
> String GetSecret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to "2020-07-22".to_string()]
  **id** | **String**| The secret ID. | 

### Return type

[**String**](string.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json, 

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **PullSecret**
> PullSecret(api_version, id, body)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to "2020-07-22".to_string()]
  **id** | **String**| The secret ID. | 
  **body** | **String**| Azure Key Vault secret URI. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json, 

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **RefreshSecret**
> RefreshSecret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to "2020-07-22".to_string()]
  **id** | **String**| The secret ID. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json, 

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **SetSecret**
> SetSecret(api_version, id, body)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to "2020-07-22".to_string()]
  **id** | **String**| The secret ID. | 
  **body** | **String**| The value of the secret. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

