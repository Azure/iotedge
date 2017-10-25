from __future__ import print_function
import logging as log
import docker
from commandbase import EdgeDeploymentCommand

class DockerClient(object):
    def __init__(self, deployment_config):
        log.info('Setting Up Docker Client With URI: ' + deployment_config.uri)
        self._client = docker.DockerClient.from_env()
        self._api_client = docker.APIClient()

    def login(self, addr, uname, pword):
        log.info('Logging into Registry ' + addr + ' using username ' + uname)
        try:
            self._client.login(username=uname, password=pword, registry=addr)
        except docker.errors.APIError as ex:
            log.error('Could Not Login To Registry ' + addr \
                      + ' using username ' + uname)
            print(ex)
            raise

    def pull(self, image, username, password):
        log.info('Executing Docker Pull For Image: ' + image)
        is_updated = True
        old_tag = None
        try:
            inspect_dict = self._api_client.inspect_image(image)
            old_tag = inspect_dict['Id']
            log.debug('Existing Image Tag: ' + old_tag)
        except docker.errors.APIError as ex:
            log.info('Docker image not found:' + image)

        try:
            auth_dict = None
            if username:
                auth_dict = {'username': username, 'password': password}
            pull_result = self._client.images.pull(image, auth_config=auth_dict)
            log.debug('Pulled Image: ' + str(pull_result))
            if old_tag:
                inspect_dict = self._api_client.inspect_image(image)
                new_tag = inspect_dict['Id']
                log.debug('Post Pull Image Tag: ' + new_tag)
                if new_tag == old_tag:
                    log.debug('Image is up to date')
                    is_updated = False
        except docker.errors.APIError as ex:
            log.error('Docker Pull, Inspect Error For Image:' + image + ' ' + str(ex))
            raise

        return is_updated

    def start(self, container_name):
        log.info('Starting Container:' + container_name)
        try:
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    container.start()
        except docker.errors.APIError as ex:
            log.error('Could Not Start Container ' + container_name)
            print(ex)
            raise

    def restart(self, container_name, timeout_int=5):
        log.info('Restarting Container:' + container_name)
        try:
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    container.restart(timeout=timeout_int)
        except docker.errors.APIError as ex:
            log.error('Could Not Retart Container ' + container_name)
            print(ex)
            raise

    def stop(self, container_name):
        log.info('Stopping Container:' + container_name)
        try:
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    container.stop()
        except docker.errors.APIError as ex:
            log.error('Could Not Stop Container ' + container_name)
            print(ex)
            raise

    def status(self, container_name):
        log.info('Getting Status For Container:' + container_name)
        try:
            result = None
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    result = container.status
            return result
        except docker.errors.APIError as ex:
            log.error('Error Observed While Checking Status For:' + container_name)
            print(ex)
            raise

    def remove(self, container_name):
        log.info('Removing Container:' + container_name)
        try:
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    container.remove()
        except docker.errors.APIError as ex:
            log.error('Could Not Remove Container ' + container_name)
            print(ex)
            raise

    def stop_by_label(self, label):
        log.info('Stopping Containers By Label:' + label)
        try:
            filter_dict = {'label': label}
            containers = self._client.containers.list(all=True,
                                                      filters=filter_dict)
            for container in containers:
                container.stop()
        except docker.errors.APIError as ex:
            log.error('Could Not Stop Containers By Label ' + label)
            print(ex)
            raise
        return

    def remove_by_label(self, label):
        log.info('Removing Containers By Label:' + label)
        try:
            filter_dict = {'label': label}
            containers = self._client.containers.list(all=True,
                                                      filters=filter_dict)
            for container in containers:
                container.remove()
        except docker.errors.APIError as ex:
            log.error('Could Not Remove Containers By Label ' + label)
            print(ex)
            raise
        return

    def create_network(self, network_name):
        log.info('Creating Network:' + network_name)
        create_network = False
        try:
            networks = self._client.networks.list(names=[network_name])
            if networks:
                num_networks = len(networks)
                if num_networks == 0:
                    create_network = True
            else:
                create_network = True
            if create_network:
                self._client.networks.create(network_name, driver="bridge")
        except docker.errors.APIError as ex:
            log.error('Could Not Create Docker Network:' + network_name)
            print(ex)
            raise

    def run(self, image, container_name, detach_bool, env_dict, nw_name,
            ports_dict, volume_dict):
        try:
            log.info('Executing docker run ' + image
                     + ' name:' + container_name
                     + ' detach:' + str(detach_bool)
                     + ' network:' + nw_name)
            for key in list(env_dict.keys()):
                log.info(' env: ' + key + ':' + env_dict[key])
            for key in list(ports_dict.keys()):
                log.info(' port: ' + key + ':' + str(ports_dict[key]))
            for key in list(volume_dict.keys()):
                log.info(' volume: ' + key + ':'
                         + volume_dict[key]['bind']
                         + ', ' + volume_dict[key]['mode'])
            self._client.containers.run(image,
                                        detach=detach_bool,
                                        environment=env_dict,
                                        name=container_name,
                                        network=nw_name,
                                        ports=ports_dict,
                                        volumes=volume_dict)
        except docker.errors.ContainerError as ex_ctr:
            log.error(container_name + ' Container Exited With Errors!')
            print(ex_ctr)
            raise
        except docker.errors.ImageNotFound as ex_img:
            log.error('Could Not Execute Docker Run. Image Not Found:' + image)
            print(ex_img)
            raise
        except docker.errors.APIError as ex:
            log.error('Could Not Execute Docker Run For Image:' + image)
            print(ex)
            raise

class EdgeDeploymentCommandDocker(EdgeDeploymentCommand):
    _edge_runtime_container_name = 'edgeAgent'
    _edge_runtime_network_name = 'azure-iot-edge'
    _edge_agent_container_label = \
            'net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent'

    def __init__(self, config_obj):
        EdgeDeploymentCommand.__init__(self, config_obj)
        self._client = DockerClient(self._config_obj.deployment_config)
        return

    def obtain_edge_agent_login(self):
        result = None
        edge_reg = self._config_obj.deployment_config.edge_image_repository
        for registry in self._config_obj.deployment_config.registries:
            if registry['address'] == edge_reg:
                result = registry
                break
        return result

    def start(self):
        log.info('Executing Edge \'start\' For Docker Deployment')
        create_new_container = False
        start_existing_container = False
        container_name = self._edge_runtime_container_name
        edge_config = self._config_obj

        image = edge_config.deployment_config.edge_image
        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_RUNNING:
            log.error('Edge runtime Is currently running. '
                      + 'Please stop or restart the Edge runtime and retry.')
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Edge runtime Is currently restarting. '
                      + 'Please stop the Edge runtime and retry.')
        else:
            edge_reg = self.obtain_edge_agent_login()
            username = None
            password = None
            if edge_reg:
                username = edge_reg['username']
                password = edge_reg['password']
            is_updated = self._client.pull(image, username, password)
            if is_updated:
                create_new_container = True
            else:
                if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
                    create_new_container = True
                    log.debug('Edge Runtime Container ' + container_name
                              + ' does not exist.')
                else:
                    start_existing_container = True

        if start_existing_container:
            self._client.start(container_name)
            print('Edge Runtime Started.')
        elif create_new_container:
            nw_name = self._edge_runtime_network_name
            self._client.create_network(nw_name)
            env_dict = {'DockerUri': edge_config.deployment_config.uri,
                        'DeviceConnectionString': edge_config.connection_string,
                        'EdgeDeviceHostName': edge_config.hostname,
                        'NetworkId': nw_name}
            idx = 0
            for registry in edge_config.deployment_config.registries:
                key = 'DockerRegistryAuth__' + str(idx) + '__serverAddress'
                val = registry['address']
                env_dict[key] = val

                key = 'DockerRegistryAuth__' + str(idx) + '__username'
                val = registry['username']
                env_dict[key] = val

                key = 'DockerRegistryAuth__' + str(idx) + '__password'
                val = registry['password']
                env_dict[key] = val
                idx = idx + 1

            ports_dict = {'8883/tcp': 8883,
                          '443/tcp': 443}
            volume_dict = {}
            if edge_config.deployment_config.uri_port and \
                    edge_config.deployment_config.uri_port != '':
                key = edge_config.deployment_config.uri_port + '/tcp'
                val = int(edge_config.deployment_config.uri_port)
                ports_dict[key] = val
            else:
                key = edge_config.deployment_config.uri_endpoint
                volume_dict[key] = {'bind': key, 'mode': 'rw'}
            self._client.run(image,
                             container_name,
                             True,
                             env_dict,
                             nw_name,
                             ports_dict,
                             volume_dict)
            print('Edge Runtime Started.')
        return

    def stop(self):
        log.info('Executing Edge \'stop\' For Docker Deployment')
        container_name = self._edge_runtime_container_name

        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.error('Edge Runtime Container container_name does not exist.')
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Edge runtime Is Restarting. Please retry later.')
        else:
            if status != self.EDGE_RUNTIME_STATUS_RUNNING:
                log.info('Edge runtime is already stopped.')
            else:
                self._client.stop(container_name)
            log.info('Stopping All Edge Modules.')
            self._client.stop_by_label(self._edge_agent_container_label)
            print('Edge Runtime Stopped.')
        return

    def restart(self):
        log.info('Executing Edge \'restart\' For Docker Deployment')
        container_name = self._edge_runtime_container_name

        status = self.status()
        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            self.start()
        elif status == self.EDGE_RUNTIME_STATUS_RESTARTING:
            log.error('Edge runtime Is Restarting. Please retry later.')
        else:
            self._client.restart(container_name)
            print('Edge Runtime Restarted.')
        return

    def status(self):
        log.info('Executing Edge \'status\' For Docker Deployment')

        result = EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        status = self._client.status(self._edge_runtime_container_name)
        if status is not None:
            if status == 'running':
                result = self.EDGE_RUNTIME_STATUS_RUNNING
            elif status == 'restarting':
                result = self.EDGE_RUNTIME_STATUS_RESTARTING
            else:
                result = self.EDGE_RUNTIME_STATUS_STOPPED
        return result

    def uninstall_common(self):
        container_name = self._edge_runtime_container_name

        status = self.status()
        if status == self.EDGE_RUNTIME_STATUS_UNAVAILABLE:
            log.info('Edge Runtime Container container_name does not exist.')
        else:
            self._client.stop(container_name)
            self._client.remove(container_name)

        log.info('Uninstalling All Edge Modules.')
        self._client.stop_by_label(self._edge_agent_container_label)
        self._client.remove_by_label(self._edge_agent_container_label)
        return

    def uninstall(self):
        log.info('Executing Edge \'uninstall\' For Docker Deployment')
        self.uninstall_common()
        print('Edge Runtime Uninstalled.')
        return

    def setup(self):
        log.info('Executing Edge \'setup\' For Docker Deployment')
        self.uninstall_common()
        print('Edge Runtime Setup.')
        return
