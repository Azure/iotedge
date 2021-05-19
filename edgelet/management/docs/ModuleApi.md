# \ModuleApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**create_module**](ModuleApi.md#create_module) | **Post** /modules | Create module.
[**delete_module**](ModuleApi.md#delete_module) | **Delete** /modules/{name} | Delete a module.
[**get_module**](ModuleApi.md#get_module) | **Get** /modules/{name} | Get a module&#39;s status.
[**list_modules**](ModuleApi.md#list_modules) | **Get** /modules | List modules.
[**module_logs**](ModuleApi.md#module_logs) | **Get** /modules/{name}/logs | Get module logs.
[**prepare_update_module**](ModuleApi.md#prepare_update_module) | **Post** /modules/{name}/prepareupdate | Prepare to update a module.
[**restart_module**](ModuleApi.md#restart_module) | **Post** /modules/{name}/restart | Restart a module.
[**start_module**](ModuleApi.md#start_module) | **Post** /modules/{name}/start | Start a module.
[**stop_module**](ModuleApi.md#stop_module) | **Post** /modules/{name}/stop | Stop a module.
[**update_module**](ModuleApi.md#update_module) | **Put** /modules/{name} | Update a module.


# **create_module**
> ::models::ModuleDetails create_module(api_version, module)
Create module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **module** | [**ModuleSpec**](ModuleSpec.md)|  | 

### Return type

[**::models::ModuleDetails**](ModuleDetails.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **delete_module**
> delete_module(api_version, name)
Delete a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to delete. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **get_module**
> ::models::ModuleDetails get_module(api_version, name)
Get a module's status.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to get. (urlencoded) | 

### Return type

[**::models::ModuleDetails**](ModuleDetails.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

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

# **module_logs**
> module_logs(api_version, name, optional)
Get module logs.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to obtain logs for. (urlencoded) | 
 **optional** | **map[string]interface{}** | optional parameters | nil if no parameters

### Optional Parameters
Optional parameters are passed through a map[string]interface{}.

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **api_version** | **String**| The version of the API. | [default to 2018-06-28]
 **name** | **String**| The name of the module to obtain logs for. (urlencoded) | 
 **follow** | **bool**| Return the logs as a stream. | [default to false]
 **tail** | **String**| Only return this number of lines from the end of the logs. | [default to all]
 **timestamps** | **bool**| Return logs with prepended rfc3339 timestamp to each line of log. | [default to false]
 **since** | **String**| Only return logs since this time, as a duration (1 day, 1d, 90m, 2 days 3 hours 2 minutes), rfc3339 timestamp, or UNIX timestamp. | [default to 0]

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **prepare_update_module**
> prepare_update_module(api_version, name, module)
Prepare to update a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to update. (urlencoded) | 
  **module** | [**ModuleSpec**](ModuleSpec.md)|  | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **restart_module**
> restart_module(api_version, name)
Restart a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to restart. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **start_module**
> start_module(api_version, name)
Start a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to start. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **stop_module**
> stop_module(api_version, name)
Stop a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to stop. (urlencoded) | 

### Return type

 (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: Not defined

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

# **update_module**
> ::models::ModuleDetails update_module(api_version, name, module, optional)
Update a module.

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2018-06-28]
  **name** | **String**| The name of the module to update. (urlencoded) | 
  **module** | [**ModuleSpec**](ModuleSpec.md)|  | 
 **optional** | **map[string]interface{}** | optional parameters | nil if no parameters

### Optional Parameters
Optional parameters are passed through a map[string]interface{}.

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **api_version** | **String**| The version of the API. | [default to 2018-06-28]
 **name** | **String**| The name of the module to update. (urlencoded) | 
 **module** | [**ModuleSpec**](ModuleSpec.md)|  | 
 **start** | **bool**| Flag indicating whether module should be started after updating. | [default to false]

### Return type

[**::models::ModuleDetails**](ModuleDetails.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

