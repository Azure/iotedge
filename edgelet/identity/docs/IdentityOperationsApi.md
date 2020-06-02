# \IdentityOperationsApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**get_current_identity**](IdentityOperationsApi.md#get_current_identity) | **Get** /identity | Get primary cloud identity for authenticated workload (caller)


# **get_current_identity**
> crate::models:::IdentityResult get_current_identity(api_version)
Get primary cloud identity for authenticated workload

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2020-06-28]

### Return type

[**crate::models:::IdentityResult**](IdentityResult.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

