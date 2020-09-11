# \SecretApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**delete_secret**](SecretApi.md#delete_secret) | **DELETE** /modules/{name}/secrets/{secretId} | Delete a secret.
[**get_secret**](SecretApi.md#get_secret) | **GET** /modules/{name}/secrets/{secretId} | Get the value of a secret.
[**pull_secret**](SecretApi.md#pull_secret) | **POST** /modules/{name}/secrets/{secretId} | Pull secret value from Azure Key Vault.
[**refresh_secret**](SecretApi.md#refresh_secret) | **PATCH** /modules/{name}/secrets/{secretId} | Refresh secret.
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
 - **Accept**: text/plain;charset=utf-8

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **pull_secret**
> pull_secret(api_version, name, secret_id, akv_id)
Pull secret value from Azure Key Vault.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The module name. (urlencoded) | 
  **secret_id** | **String**| The name of the secret. (urlencoded) | 
  **akv_id** | **String**| Azure Key Vault secret identifier. | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: text/plain;charset=utf-8
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **refresh_secret**
> refresh_secret(api_version, name, secret_id)
Refresh secret.

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

 - **Content-Type**: text/plain;charset=utf-8
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

