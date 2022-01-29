FROM arm32v7/ubuntu:18.04

RUN apt-get update && apt-get install -y libssl-dev 

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

WORKDIR /app

COPY ./armv7-unknown-linux-gnueabihf/release/generic-mqtt-tester ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /app/generic-mqtt-tester
