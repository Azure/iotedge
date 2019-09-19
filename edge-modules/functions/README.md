# Function Sample
An Azure function that behaves similar to temperature filter function. The Azure function is used to test Azure function deployment as demonstrated in https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-deploy-function

## How to Run
- Just add a filter module into your edgeConfiguration.json deployment file: 
Example:
```json
{
  "tempFilterFunctions": {
    "type": "docker",
    "restartPolicy": "always",
    "status": "running",
    "settings": {
      "image": "<path/to/tempFilterFunctionsImage>"
    },
    "env": {
      "AZURE_FUNCTIONS_ENVIRONMENT": {
        "value": "Development"
      }
    }
  }
}
```

- And Add routes like this:

```json

"routes": {
        "TempFilterFunctionsToCloud": "FROM /messages/modules/tempFilterFunctions/outputs/output1 INTO $upstream",
        "TempSensorToTempFilter": "FROM /messages/modules/tempSensor/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/tempFilterFunctions/inputs/input1')"
      }
```

- Make sure you add registry credentials for edgebuilds.azurecr.io (See credentials in the portal)

## How to build the function image: 
The procedure to build Azure function image is indifference from how would one build a dotnet application image:
- Go to folder: <Azure-IoT-Edge-Core Repo path>\edge-modules\functions\samples
- Run the command: `dotnet publish -c Release`
- Run the command: `docker build --no-cache -t <DockerRegServer:5000>/<ImageName>:<LatestTag> --file .\samples\docker\linux\amd64\Dockerfile .`
- Do a docker push: `docker push <DockerRegServer:5000>/<ImageName>:<LatestTag>`
