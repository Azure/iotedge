# Credentials

## Properties
Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**auth_type** | **String** | Indicates the type of authentication credential used. | [default to null]
**source** | **String** | Indicates the source of the authentication credential. | [default to null]
**key** | **String** | The symmetric key used for authentication. Specified only if the &#39;authType&#39; is &#39;symmetric-key&#39; and the &#39;source&#39; is &#39;payload&#39;. | [optional] [default to null]
**identity_cert** | **String** | The identity certificate. Should be a PEM formatted byte array if the &#39;authType&#39; is &#39;x509&#39; and the &#39;source&#39; is &#39;payload&#39; or should be a reference to the certificate if the &#39;authType&#39; is &#39;x509&#39; and the &#39;source&#39; is &#39;hsm&#39;. | [optional] [default to null]
**identity_private_key** | **String** | The identity private key. Should be a PEM formatted byte array if the &#39;authType&#39; is &#39;x509&#39; and the &#39;source&#39; is &#39;payload&#39; or should be a reference to the private key if the &#39;authType&#39; is &#39;x509&#39; and the &#39;source&#39; is &#39;hsm&#39;. | [optional] [default to null]

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)


