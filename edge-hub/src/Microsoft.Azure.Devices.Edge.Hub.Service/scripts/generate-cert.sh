#!/bin/sh

# This Script uses openssl to generate a self signed certificate

if [ ! -f $SSL_CERTIFICATE_PATH/$SSL_CERTIFICATE_NAME ] ; then
 echo "Generate certificate"
 
 openssl req -nodes -new -x509 -keyout /etc/ssl/private/mqtt-server.key -out $SSL_CERTIFICATE_PATH/mqtt-server.crt -subj '/CN=edge.iot.microsoft.com'
 openssl pkcs12 -export -out $SSL_CERTIFICATE_PATH/$SSL_CERTIFICATE_NAME -inkey /etc/ssl/private/mqtt-server.key -in $SSL_CERTIFICATE_PATH/mqtt-server.crt -passout pass:

 echo "Certificate generated"

else echo "Certificate already exists"
fi
