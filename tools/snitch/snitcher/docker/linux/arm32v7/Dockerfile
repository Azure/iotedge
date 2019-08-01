FROM ubuntu:18.04

RUN apt-get update -y && \
    apt-get install -y apt-utils ca-certificates curl gpg && \
    curl https://ftp-master.debian.org/keys/archive-key-9-security.asc | gpg --dearmor > archive-key-9-security.gpg && \
    cp ./archive-key-9-security.gpg /etc/apt/trusted.gpg.d/ &&\
    echo "deb http://security.debian.org/debian-security stretch/updates main" >> /etc/apt/sources.list && \
    apt-get update -y && \
    apt-get install -y libssl1.0.2
    
COPY ./snitcher /usr/local/bin/
CMD /usr/local/bin/snitcher