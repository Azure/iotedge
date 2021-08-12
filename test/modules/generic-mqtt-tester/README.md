# Generic MQTT Test Module

## Description
See description here:
[link](src/tester.rs#L96)

## Env Vars

Defaults can be seen here:
[link](config/default.json)

|Env Var| Description|
|----------|------------|
|TEST_SCENARIO | Specifies the mode that the test module runs in|
|TRC_URL| Specifies the url of the TestResultCoordinator (TRC). Needs manual specification if deploying on a node which does not run the TRC. Othewise will defaul accordingly.|
|TRACKING_ID| Specifies the tracking id for which to tag reports to the TRC. Needed so the TRC can verify test results are truly coming from the same test.|
|TEST_START_DELAY| Specifies the delay before the test logic starts running. Needed to give time for the environment to spin up and get everything ready.|
|MESSAGE_FREQUENCY| Specifies the frequency of messages being sent from any initiator-based test scenario.|
|MESSAGE_SIZE_IN_BYTES| Bytes in each message. All dummy data.|
|TOPIC_SUFFIX | Specifies the suffix to be used when publishing or subscribing. Messages will be sent and received on `initiate/<topic_suffix>`.|
|MESSAGES_TO_SEND| Specifies the amount of messages that will be sent from any initiator-based test scenario before the test module shuts down.|

