# Test Dashboard Documentation

## High Level Overview
Grafana is a tool where you can hook up data sources, query them, then visualize the queries via a GUI editor.

The test dashboard is configured to pull from two data sources:
Azure Monitor (Log Analytics): Used for longhaul / stress / connectivity test reports and metrics
SQL Server: Used for imformation about builds / CI / E2E

It then queries both of these to get the data and the visualization logic is built into the frontend.

## Architecture
There are two docker containers supporting the backend. The first is what is running the frontend grafana instance, and the second is a scheduled job ingesting data from Azure Dev Ops (one of the data sources) into a SQL server. A container to support the Azure Log Analytics data ingestion (the other data source) is not needed because grafana provides direct integration with this service.

## Usage - Development

This repository will allow you to spin up a local grafana dashboard for viewing and editing the test dashboard. In order to get started, first clone the repo, remove the .tmp extensions from all files, substitute the SQL server credentials in datasources.yaml, then run the following commands:
```
docker build -t azure-monitor-test .
docker run -d -p 3000:3000 azure-monitor-test
```

Your grafana dashboard should now be accessible on localhost port 3000. The default username/password is admin/admin. Once signed in, you must add a datasource for Azure Monitor manually as grafana's integration here is currently broken.

Grafana has a base image allowing you to provision via Dockerfile by loading datasources and dashboards through json and yaml files. One can create and edit dashboards once the container is running, but in order to save created dashboards make sure to export them to json before deleting the container. If you want your created dashboard to be visible when the container is rebuilt, follow these steps:
- Move the exported json to the dashboards folder
- Rebuild and restart the container then you should see it running with your new dashboard

This link is helpful for provisioning instructions:

https://56k.cloud/blog/provisioning-grafana-datasources-and-dashboards-automagically/

## Usage - Deployment

The test dashboard should be live at:
https://iotedgetestdashboard.azurewebsites.net/d/OLjJ46wWz/home

There are four azure resources which support the test dashboard:
1. An app service hosting the dashboard
2. A SQL Server storing data from Azure Dev Ops
3. A container instance hosting the scheduled ingestion job from Azure Dev Ops to the SQL server. The source code for this can be found by searching for VstsPipelineSync in our repository.
4. A container registry storing the images used by 1) and 3)

All of these resources will be located under the Azure ressource group [TestDashboard](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/5ed2dcb6-29bb-40de-a855-8c24a8260343/resourceGroups/TestDashboard/overview)

### Changing the grafana frontend
If making any changes to the dashboard frontend, you will have to rebuild the test dashboard container and redeploy the app service. In order to do this, follow the steps under Development to save your altered dashboard, then build and push the new image to the container registry. After this is complete you can redeploy the app service with the new image you have pushed. 

Once redeployed, you will have to manually add the credentials for Azure Monitor, set the home page, and re-register users.

### Changing the Azure Dev Ops data ingestion
Data is ingested into Azure Dev Ops through the VstsPipelineSync project in our repository. One can make changes to this project then build and push a new image to the test dashboard container registry. After this, delete and recreate a new Azure continaer instance pointing to the new image.

