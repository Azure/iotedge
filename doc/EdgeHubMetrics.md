Name | Additional Tags | Description | Type
--- | --- | --- | ---
edgehub_gettwin_total | source, id | Total number of GetTwin calls | Counter
edgehub_messages_received_total | protocol, id | Total number of messages received from clients | Counter
edgehub_messages_sent_total | to, from | Total number of messages sent to clients or upstream | Counter
edgehub_reported_properties_total | target, id | Total reported property updates calls | Counter
edgehub_message_size_bytes | id | Message size from clients. | Counter
edgehub_gettwin_duration_seconds | source, id | Time taken for get twin operations | Counter
edgehub_message_send_duration_seconds | to, from | Time taken to send a message | Counter
edgehub_reported_properties_update_duration_seconds | target, id | Time taken to update reported properties |  Counter


Note: All metrics contain the following tags
Tag | Description
---|---
iothub | The hub the device is talking to
edge_device | The device id of the current device
instance_number | A Guid representing the current runtime. On restart, all metrics will be reset. This Guid makes it easier to reconcile restarts. 