openssl genrsa 2048 > private.pem
openssl req -batch -x509 -new -key private.pem -out public.pem 
openssl pkcs12 -export -in public.pem -inkey private.pem -out broker.pfx -password pass: