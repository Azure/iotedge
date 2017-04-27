FROM armhf/ubuntu:16.04

########### Install Gateway dependencies ###########
RUN apt-get update && apt-get install -y \
    libglib2.0-dev \
    ca-certificates \
    curl \
    libcurl4-openssl-dev \
    libssl-dev \
    uuid-dev

########### Setup Environment Variables ##########
WORKDIR /app

ENV LD_LIBRARY_PATH=/app/lib:/app/install-deps/lib

########### Install And Run Gateway and BLE Module ###########

COPY publish .

CMD ./ble_gateway ./ble_config/config.json
