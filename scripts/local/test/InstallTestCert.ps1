. .\Login.ps1
Login

Set-AzureRmContext -SubscriptionName IOT_EDGE_DEV1

& "..\..\windows\DownloadAndInstallCertificate.ps1" edgebuildkv IoTEdgeTestCert