# Rust API client for swagger

No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)

## Overview
This API client was generated by the [swagger-codegen](https://github.com/swagger-api/swagger-codegen) project.  By using the [swagger-spec](https://github.com/swagger-api/swagger-spec) from a remote server, you can easily generate an API client.

- API version: 2019-10-22
- Package version: 1.0.0
- Build package: io.swagger.codegen.languages.RustClientCodegen

## Installation
Put the package under your project folder and add the following in import:
```
    "./swagger"
```

## Documentation for API Endpoints

All URIs are relative to *http://localhost*

Class | Method | HTTP request | Description
------------ | ------------- | ------------- | -------------
*DeviceActionsApi* | [**reprovision_device**](docs/DeviceActionsApi.md#reprovision_device) | **Post** /device/reprovision | Trigger a device reprovisioning flow.
*IdentityApi* | [**create_identity**](docs/IdentityApi.md#create_identity) | **Post** /identities/ | Create an identity.
*IdentityApi* | [**delete_identity**](docs/IdentityApi.md#delete_identity) | **Delete** /identities/{name} | Delete an identity.
*IdentityApi* | [**list_identities**](docs/IdentityApi.md#list_identities) | **Get** /identities/ | List identities.
*IdentityApi* | [**update_identity**](docs/IdentityApi.md#update_identity) | **Put** /identities/{name} | Update an identity.
*ModuleApi* | [**create_module**](docs/ModuleApi.md#create_module) | **Post** /modules | Create module.
*ModuleApi* | [**delete_module**](docs/ModuleApi.md#delete_module) | **Delete** /modules/{name} | Delete a module.
*ModuleApi* | [**get_module**](docs/ModuleApi.md#get_module) | **Get** /modules/{name} | Get a module&#39;s status.
*ModuleApi* | [**list_modules**](docs/ModuleApi.md#list_modules) | **Get** /modules | List modules.
*ModuleApi* | [**module_logs**](docs/ModuleApi.md#module_logs) | **Get** /modules/{name}/logs | Get module logs.
*ModuleApi* | [**prepare_update_module**](docs/ModuleApi.md#prepare_update_module) | **Post** /modules/{name}/prepareupdate | Prepare to update a module.
*ModuleApi* | [**restart_module**](docs/ModuleApi.md#restart_module) | **Post** /modules/{name}/restart | Restart a module.
*ModuleApi* | [**start_module**](docs/ModuleApi.md#start_module) | **Post** /modules/{name}/start | Start a module.
*ModuleApi* | [**stop_module**](docs/ModuleApi.md#stop_module) | **Post** /modules/{name}/stop | Stop a module.
*ModuleApi* | [**update_module**](docs/ModuleApi.md#update_module) | **Put** /modules/{name} | Update a module.
*SystemInformationApi* | [**get_system_info**](docs/SystemInformationApi.md#get_system_info) | **Get** /systeminfo | Return host system information.


## Documentation For Models

 - [Config](docs/Config.md)
 - [EnvVar](docs/EnvVar.md)
 - [ErrorResponse](docs/ErrorResponse.md)
 - [ExitStatus](docs/ExitStatus.md)
 - [Identity](docs/Identity.md)
 - [IdentityList](docs/IdentityList.md)
 - [IdentitySpec](docs/IdentitySpec.md)
 - [ModuleDetails](docs/ModuleDetails.md)
 - [ModuleList](docs/ModuleList.md)
 - [ModuleSpec](docs/ModuleSpec.md)
 - [RuntimeStatus](docs/RuntimeStatus.md)
 - [Status](docs/Status.md)
 - [SystemInfo](docs/SystemInfo.md)
 - [UpdateIdentity](docs/UpdateIdentity.md)


## Documentation For Authorization
 Endpoints do not require authorization.


## Author


