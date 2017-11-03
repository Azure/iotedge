# Function Sample

## How to Run
- Manual build of this sample has been uploaded to edgebuilds.azurecr.io/azedge-functions-sample-amd64:1.0.0-preview007;

- Just add a filter module into your edgeConfiguration.json deployment file: 
Example:
```json
{
"filter": {  
    "version": "2.0", 
    "type": "docker", 
    "status": "running",
    "restartPolicy": "always", 
    "settings": { 
      "image": "edgebuilds.azurecr.io/azedge-functions-sample-amd64:1.0.0-preview007",
      "createOptions": ""
    }
  }
}
```

- And Add routes like this: 

	```json

  "routes":{
    "route1": "FROM /messages/modules/tempSensor/outputs/temperatureOutput INTO BrokeredEndpoint('/modules/filter/inputs/input1')",
    "route2": "FROM /messages/modules/filter/outputs/alertOutput INTO $upstream"
  }

- Make sure you add registry credentials for edgebuilds.azurecr.io (See credentials in the portal)

## How to build the image: 
- Go to folder: <Azure-IoT-Edge-Core Repo path>\edge-modules\functions
- Run the command: `docker build --no-cache -t edgebuilds.azurecr.io/azedge-functions-sample-amd64:<LatestTag> --file .\samples\docker\linux\amd64\Dockerfile .`

- Do a docker push: `docker push edgebuilds.azurecr.io/azedge-functions-sample-amd64:<LatestTag>`
