sudo curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az extension add --name log-analytics
az login --service-principal -u http://azure-cli-2016-08-05-14-31-15 -p VerySecret --tenant
az monitor log-analytics query -w fdf47b96-87f3-4b86-90b9-d83e2deae8a0 --analytics-query "Alert  | limit 5"