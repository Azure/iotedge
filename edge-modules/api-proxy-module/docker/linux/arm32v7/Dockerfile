#-------------------------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See https://go.microsoft.com/fwlink/?linkid=2090316 for license information.
#-------------------------------------------------------------------------------------------------------------
FROM arm32v7/nginx:1.19.9
WORKDIR /app

RUN chown -R nginx:nginx /app && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d
RUN touch /var/run/nginx.pid && \
        chown -R nginx:nginx /var/run/nginx.pid 
        
COPY ./docker/linux/arm32v7/api-proxy-module .
COPY ./docker/linux/arm32v7/templates .

USER nginx  

#expose ports
EXPOSE 443/tcp	
EXPOSE 80/tcp
#used by registry
EXPOSE 5000/tcp
#used by blob storage
EXPOSE 11002/tcp
#default
EXPOSE 8000/tcp
ENTRYPOINT ["/app/api-proxy-module"]
 