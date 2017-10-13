#########
# This container is used to create a local instance
# of the "Hosted Linux Preview" VSTS build agent.
#
# It will be used to define the agent used to run
# our BVT test suite against the Raspberry Pi.
#########
FROM microsoft/vsts-agent:ubuntu-16.04-tfs-2017-docker-17.03.0-ce-standard

ARG AGENT_VERSION="2.120.1"

WORKDIR /vsts

# Install VSTS-Agent
RUN curl \
      -SL https://github.com/Microsoft/vsts-agent/releases/download/v$AGENT_VERSION/vsts-agent-ubuntu.16.04-x64-$AGENT_VERSION.tar.gz \
      --output /vsts/vsts-agent-ubuntu.16.04-x64-$AGENT_VERSION.tar.gz \
 && tar xzf /vsts/vsts-agent-ubuntu.16.04-x64-$AGENT_VERSION.tar.gz \
 && rm /vsts/vsts-agent-ubuntu.16.04-x64-$AGENT_VERSION.tar.gz

# Launch VSTS-Agent
CMD /vsts/bin/Agent.Listener configure \
      --unattended \
      --agent "${VSTS_AGENT:-$(hostname)}" \
      --url "https://${VSTS_ACCOUNT:-msazure}.visualstudio.com" \
      --auth PAT \
      --token "${VSTS_TOKEN}" \
      --pool "${VSTS_POOL:-Azure-IoT-Edge-Core}" \
      --work "${VSTS_WORK:-_work}" \
      --replace \
 && /vsts/bin/Agent.Listener run
