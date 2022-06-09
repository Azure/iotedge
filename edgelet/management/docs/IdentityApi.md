# \IdentityApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_identity**](IdentityApi.md#create_identity) | **Post** /identities/ | Create an identity.
[**delete_identity**](IdentityApi.md#delete_identity) | **Delete** /identities/{name} | Delete an identity.
[**list_identities**](IdentityApi.md#list_identities) | **Get** /identities/ | List identities.
[**update_identity**](IdentityApi.md#update_identity) | **Put** /identities/{name} | Update an identity.


# **create_identity**
> ::models::Identity create_identity(api_version, identity)
Create an identity.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **identity** | [**IdentitySpec**](IdentitySpec.md)|  | 

### Return type

[**::models::Identity**](Identity.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **delete_identity**
> delete_identity(api_version, name)
Delete an identity.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the identity to delete. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **list_identities**
> ::models::IdentityList list_identities(api_version)
List identities.

This returns the list of current known idenities. 

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]

### Return type

[**::models::IdentityList**](IdentityList.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_identity**
> ::models::Identity update_identity(api_version, name, updateinfo)
Update an identity.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the identity to update. (urlencoded) | 
  **updateinfo** | [**UpdateIdentity**](UpdateIdentity.md)|  | 

### Return type

[**::models::Identity**](Identity.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

