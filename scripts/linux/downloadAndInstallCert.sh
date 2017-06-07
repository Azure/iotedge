#!/bin/bash                                                                                                                                      

usage()
{
    echo "Missing arguments. Usage: $0 -u <App SP> -s <App secret> -t <App Tenant ID> -c <Cert name> -v <keyVault name>"
    exit 1;
}

while getopts ":c:u:s:t:v:" o; do
    case "${o}" in
        u)
            APP_SP_NAME=${OPTARG}
            ;;
        s)
            APP_SP_SECRET=${OPTARG}
            ;;
        t)
            APP_TENANT_ID=${OPTARG}
            ;;
        c)
            CERT_NAME=${OPTARG}
            ;;
        v)
            KEYVAULT_NAME=${OPTARG}
            ;;
        *)
            usage
            ;;
    esac
done
shift $((OPTIND-1))

if [ -z "${APP_SP_NAME}" ] || [ -z "${APP_SP_SECRET}" ] || [ -z "${APP_TENANT_ID}" ] || [ -z "${CERT_NAME}" ] || [ -z "${KEYVAULT_NAME}" ]; then
    usage
fi                               
							   
BASEDIR=$(dirname "$0")                                                                                                                          
CERT_PATH=$AGENT_WORKFOLDER/iotedgetestcert.pfx                                                                                                  
                                                   
# Login to KeyVault using App Service Principal												   
echo Logging in to KeyVault using App Service Principal
az login --service-principal -u $APP_SP_NAME --tenant $APP_TENANT_ID -p $APP_SP_SECRET 
echo Done logging in       
	   
# Download the Cert					
echo Downloading cert from KeyVault	
keyVaultCertSecret="$(az keyvault secret show --name $CERT_NAME --vault-name $KEYVAULT_NAME)"
keyVaultCert="$(echo $keyVaultCertSecret | jq -r '.value')"	
echo Done downloading cert from KeyVault
				   
# Install the Cert				   
echo Installing Cert
powershell -command "$BASEDIR/InstallCert.ps1 -CertificateValue $keyVaultCert"
echo Done installing Cert.

exit 0