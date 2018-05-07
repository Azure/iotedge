# \WorkloadApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_identity_certificate**](WorkloadApi.md#create_identity_certificate) | **Post** /modules/{name}/certificate/identity | 
[**create_server_certificate**](WorkloadApi.md#create_server_certificate) | **Post** /modules/{name}/certificate/server | 
[**decrypt**](WorkloadApi.md#decrypt) | **Post** /modules/{name}/decrypt | 
[**encrypt**](WorkloadApi.md#encrypt) | **Post** /modules/{name}/encrypt | 
[**sign**](WorkloadApi.md#sign) | **Post** /modules/{name}/sign | 


# **create_identity_certificate**
> ::models::CertificateResponse create_identity_certificate(api_version, name)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to get certificate. (urlencoded) | 

### Return type

[**::models::CertificateResponse**](CertificateResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **create_server_certificate**
> ::models::CertificateResponse create_server_certificate(api_version, name, request)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to get certificate. (urlencoded) | 
  **request** | [**ServerCertificateRequest**](ServerCertificateRequest.md)| Parameters for certificate creation. | 

### Return type

[**::models::CertificateResponse**](CertificateResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **decrypt**
> ::models::DecryptResponse decrypt(api_version, name, payload)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module on whose behalf the ciphertext will be decrypted. (urlencoded) | 
  **payload** | [**DecryptRequest**](DecryptRequest.md)| The data to be decrypted. | 

### Return type

[**::models::DecryptResponse**](DecryptResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **encrypt**
> ::models::EncryptResponse encrypt(api_version, name, payload)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module on whose behalf the plaintext will be encrypted. (urlencoded) | 
  **payload** | [**EncryptRequest**](EncryptRequest.md)| The data to be encrypted. | 

### Return type

[**::models::EncryptResponse**](EncryptResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **sign**
> ::models::SignResponse sign(api_version, name, payload)


### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module on whose behalf the payload will be signed. (urlencoded) | 
  **payload** | [**SignRequest**](SignRequest.md)| The data to be signed. | 

### Return type

[**::models::SignResponse**](SignResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

