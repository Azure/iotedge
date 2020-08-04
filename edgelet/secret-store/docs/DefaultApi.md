# \DefaultApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**delete_secret**](DefaultApi.md#delete_secret) | **Delete** /{id} | 
[**get_secret**](DefaultApi.md#get_secret) | **Get** /{id} | 
[**pull_secret**](DefaultApi.md#pull_secret) | **Post** /{id} | 
[**refresh_secret**](DefaultApi.md#refresh_secret) | **Patch** /{id} | 
[**set_secret**](DefaultApi.md#set_secret) | **Put** /{id} | 


# **delete_secret**
> delete_secret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to 2020-07-22]
  **id** | **String**| The secret ID. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_secret**
> String get_secret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to 2020-07-22]
  **id** | **String**| The secret ID. | 

### Return type

**String**

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **pull_secret**
> pull_secret(api_version, id, akv_uri)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to 2020-07-22]
  **id** | **String**| The secret ID. | 
  **akv_uri** | **String**| Azure Key Vault secret URI. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **refresh_secret**
> refresh_secret(api_version, id)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to 2020-07-22]
  **id** | **String**| The secret ID. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **set_secret**
> set_secret(api_version, id, value)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The API version. | [default to 2020-07-22]
  **id** | **String**| The secret ID. | 
  **value** | **String**| The value of the secret. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

