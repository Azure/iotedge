FROM ubuntu:18.04

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/generic_mqtt_tester ./

RUN apt-get update && apt-get install -y libssl-dev

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
/app/generic_mqtt_tester