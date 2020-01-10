param (
    [Parameter(Mandatory=$true)][string]$v
 )
 $arch = "amd64"
 
dotnet publish C:\Users\Lee\source\repos\iotedge\test\modules\MetricsValidator\MetricsValidator.csproj
 
docker build --rm -f "C:\Users\Lee\source\repos\iotedge\test\modules\MetricsValidator\docker\linux\amd64\Dockerfile" -t lefitchereg1.azurecr.io/metrics_validator_test:0.0.$v-$arch "C:\Users\Lee\source\repos\iotedge\test\modules\MetricsValidator\bin\Debug\netcoreapp2.1\publish\" ; if ($?) { docker push lefitchereg1.azurecr.io/metrics_validator_test:0.0.$v-$arch }
