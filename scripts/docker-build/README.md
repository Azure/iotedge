Containerized Build Environment
===============================

This Dockerfile creates an environment based on the VSTS "Hosted Linux Preview" build agent.

Image Creation
--------------

```
docker build -t edge-build-env .
```

Use Cases
---------

### Local VSTS Agent

  Launch a local VSTS agent, test your build configuration and interact with it through VSTS.

  ```
  docker run -it -e "VSTS_TOKEN=<your VSTS personal access token>" --net host -v /var/run/docker.sock:/var/run/docker.sock edge-build-env
  ```

### Temporary Build Environment

  Enter the environment made available to the VSTS agent, validate dependencies and test your scripts. 

  ```
  docker run -it -e "VSTS_TOKEN=<your VSTS personal access token>" --net host -v /var/run/docker.sock:/var/run/docker.sock edge-build-env /bin/bash
  ```
