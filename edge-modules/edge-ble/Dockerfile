FROM node:8

RUN apt-get update && \
    apt-get install -y \
    bluetooth bluez libbluetooth-dev libudev-dev

WORKDIR /app
ADD . /app

RUN npm set progress=false && npm install --only=production

ENTRYPOINT ["node", "/app/main.js"]
