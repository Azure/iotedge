FROM busybox

# Add an unprivileged user account for running mqttd	
ARG MQTTDUSER_ID=1000

RUN adduser -Du ${MQTTDUSER_ID} mqttduser

EXPOSE 1883/tcp
EXPOSE 8883/tcp

ADD ./mqtt/target/x86_64-unknown-linux-musl/release/mqttd /usr/local/bin/mqttd

USER mqttduser

ENTRYPOINT ["/usr/local/bin/mqttd"]
