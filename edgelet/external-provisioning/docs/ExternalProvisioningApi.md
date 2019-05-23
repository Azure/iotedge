# \ExternalProvisioningApi

All URIs are relative to *http://localhost*

Method | HTTP request | Description
------------- | ------------- | -------------
[**get_device_provisioning_information**](ExternalProvisioningApi.md#get_device_provisioning_information) | **Get** /edge/device/provisioninginformation | Gets the IoT hub provisioning information of the device.


# **get_device_provisioning_information**
> ::models::DeviceProvisioningInfo get_device_provisioning_information(api_version)
Gets the IoT hub provisioning information of the device.

This returns the IoT hub provisioning information of the device. 

### Required Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
  **api_version** | **String**| The version of the API. | [default to 2019-04-10]

### Return type

[**::models::DeviceProvisioningInfo**](DeviceProvisioningInfo.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

