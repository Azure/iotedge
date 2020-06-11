# \KeyOperationsApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**sign**](KeyOperationsApi.md#sign) | **Post** /sign | Sign using identity keys


# **sign**
> crate::models:::SignResponse sign(api_version, sign_payload)
Sign using identity keys

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2020-06-28]
  **sign_payload** | [**SignRequest**](SignRequest.md)| The data to be signed. | 

### Return type

[**crate::models:::SignResponse**](SignResponse.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

