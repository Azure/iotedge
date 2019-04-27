# \HostingApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**get_device_connection_information**](HostingApi.md#get_device_connection_information) | **Get** /edge/device/connectioninformation | Gets the IoT hub connection information of the device.

# **get_device_connection_information**
> ::models::DeviceConnectionInfo get_device_connection_information(api_version)
Gets the IoT hub connection information of the device.

This returns the IoT hub connection information of the device. 

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2019-04-10]

### Return type

[**::models::DeviceConnectionInfo**](DeviceConnectionInfo.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)
