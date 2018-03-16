"""
Module defines class EdgeDockerClient which implements methods to interact with the
docker daemon in order to setup, bootstrap and control the Edge runtime.
"""
from __future__ import print_function
from io import BytesIO
import logging
import tarfile
import time
import os
import docker
import edgectl.errors
from edgectl.utils import EdgeUtils

class EdgeDockerClient(object):
    """
    EdgeDockerClient implements APIs to interact with the docker py
    module and is used to configure, launch and control the Edge runtime.
    """
    _DOCKER_INFO_OS_TYPE_KEY = 'OSType'

    def __init__(self, docker_client=None, docker_api_client=None):
        if docker_client is not None:
            self._client = docker_client
        else:
            self._client = docker.DockerClient.from_env()

        if docker_api_client is not None:
            self._api_client = docker_api_client
        else:
            params_dict = docker.utils.kwargs_from_env()
            base_url = None
            tls = None
            if params_dict:
                keys_list = list(params_dict.keys())
                if 'base_url' in keys_list:
                    base_url = params_dict['base_url']
                if 'tls' in keys_list:
                    tls = params_dict['tls']
            self._api_client = docker.APIClient(base_url=base_url, tls=tls)

    @classmethod
    def create_instance(cls, docker_client, docker_api_client):
        """
        Factory method useful in testing.
        """
        return cls(docker_client, docker_api_client)

    def check_availability(self):
        """
        API to check if docker is available

        Returns:
            True if docker is available.
            False otherwise
        """
        is_available = False
        try:
            self._client.info()
            is_available = True
        except docker.errors.APIError as ex:
            msg = 'Could not connect to docker daemon. {0}'.format(ex)
            logging.error(msg)

        return is_available

    def login(self, address, username, password):
        """
        API to log into a docker registry using the supplied credentials

        Args:
            address (str): Docker registry address
            username (str): Docker registry username
            password (str): Docker registry password

        Raises:
            edgectl.errors.EdgeDeploymentError
        """
        logging.info('Logging into registry %s using username %s.', address, username)
        try:
            self._client.login(username=username, password=password, registry=address)
        except docker.errors.APIError as ex:
            msg = 'Could not login to registry {0} using username {1}.'.format(address, username)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def get_os_type(self):
        """
        API to return the docker info OSType

        Returns:
            Docker OS type string in lower case

        Raises:
            edgectl.errors.EdgeDeploymentError
        """
        try:
            info = self._client.info()
            return info[self._DOCKER_INFO_OS_TYPE_KEY].lower()
        except docker.errors.APIError as ex:
            msg = 'Docker daemon returned error'
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def get_local_image_sha_id(self, image):
        """
        API to return the image sha id if it is available locally.

        Args:
            image (str): Name of image from which to retrieve it's id

        Returns:
            String containing the tag of the image
            None if the image is not available locally.
        """
        local_id = None
        try:
            logging.info('Checking if image exists locally: %s', image)
            inspect_dict = self._api_client.inspect_image(image)
            local_id = inspect_dict['Id']
            logging.info('Image exists locally. Id: %s', local_id)
        except docker.errors.APIError:
            logging.info('Image not found locally: %s', image)

        return local_id

    def pull(self, image, username, password):
        """
        API to pull the latest binaries from the given image's repository.

        Args:
            image (str): Name of image to pull from its repository.
            username (str): Username to access image's repository.
                            None if no credentials are required.
            password (str): Password to access image's repository.
                            None if no credentials are required.

        Returns:
            True if a newer image was found and downloaded
            False otherwise

        Raises:
            edgectl.errors.EdgeDeploymentError
        """
        logging.info('Executing pull for: %s', image)

        old_id = self.get_local_image_sha_id(image)
        if old_id is None:
            logging.info('Please note depending on network conditions and registry server' \
                         ' availability this may take a few minutes.')
        else:
            logging.info('Checking for newer tag for image: %s', image)

        try:
            is_updated = True
            auth_dict = None
            if username is not None:
                auth_dict = {'username': username, 'password': password}
            self._client.images.pull(image, auth_config=auth_dict)
            logging.info('Completed pull for image: %s', image)
            if old_id is not None:
                inspect_dict = self._api_client.inspect_image(image)
                new_id = inspect_dict['Id']
                logging.debug('Newly pulled image id: %s', new_id)
                if new_id == old_id:
                    logging.info('Image is up to date.')
                    is_updated = False
                else:
                    logging.info('Pulled image with newer tag: %s', new_id)

            return is_updated
        except docker.errors.APIError as ex:
            msg = 'Error during pull for image {0}'.format(image)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def _get_container_by_name(self, container_name):
        try:
            return self._client.containers.get(container_name)
        except docker.errors.NotFound as nf_ex:
            msg = 'Could not find container by name {0}'.format(container_name)
            logging.error(msg)
            print(nf_ex)
            raise edgectl.errors.EdgeDeploymentError(msg, nf_ex)
        except docker.errors.APIError as ex:
            msg = 'Error getting container by name: {0}'.format(container_name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def _exec_container_method(self, container_name, method, **kwargs):
        container = self._get_container_by_name(container_name)
        try:
            getattr(container, method)(**kwargs)
        except docker.errors.APIError as ex:
            msg = 'Could not {0} container: {1}'.format(method, container_name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def start(self, container_name):
        """
        API to start a container given its name

        Args:
            container_name (str): Name of the container

        Raises:
            edgectl.errors.EdgeDeploymentError if the container was not found
            or there were problems encountered starting the container.
        """
        logging.info('Starting container: ' + container_name)
        self._exec_container_method(container_name, 'start')

    def restart(self, container_name, timeout_int=5):
        """
        API to restart a container given its name and timeout period to
        wait for the restart operation to complete.

        Args:
            container_name (str): Name of the container

        Raises:
            edgectl.errors.EdgeDeploymentError if the container was not found
            or there were problems encountered restarting the container.
        """
        logging.info('Restarting container: ' + container_name)
        self._exec_container_method(container_name, 'restart', timeout=timeout_int)

    def stop(self, container_name):
        """
        API to stop a container given its name

        Args:
            container_name (str): Name of the container

        Raises:
            edgectl.errors.EdgeDeploymentError if the container was not found
            or there were problems encountered stopping the container.
        """
        logging.info('Stopping container: ' + container_name)
        self._exec_container_method(container_name, 'stop')

    def status(self, container_name):
        """
        API to obtain the status container given its name

        Args:
            container_name (str): Name of the container

        Returns
            Status as reprted by docker, None if the container was not found.

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered getting the status of the container.
        """
        try:
            result = None
            containers = self._client.containers.list(all=True)
            for container in containers:
                if container_name == container.name:
                    result = container.status
            return result
        except docker.errors.APIError as ex:
            msg = 'Error while checking status for: {0}'.format(container_name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def remove(self, container_name):
        """
        API to remove a container given its name

        Args:
            container_name (str): Name of the container

        Raises:
            edgectl.errors.EdgeDeploymentError if the container was not found
            or there were problems encountered removing the container.
        """
        logging.info('Removing container: ' + container_name)
        self._exec_container_method(container_name, 'remove')

    def stop_by_label(self, label):
        """
        API to stop all containers labeled with the given label string

        Args:
            label (str): Label string

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered stopping the container(s).
        """
        logging.info('Stopping containers by label: ' + label)
        try:
            filter_dict = {'label': label}
            containers = self._client.containers.list(all=True,
                                                      filters=filter_dict)
            for container in containers:
                container.stop()
        except docker.errors.APIError as ex:
            logging.error('Could not stop containers by label: %s', label)
            msg = 'Could not stop containers by label: {0}'.format(label)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def remove_by_label(self, label):
        """
        API to remove all containers labeled with the given label string

        Args:
            label (str): Label string

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered removing the container(s).
        """
        logging.info('Removing containers by label: ' + label)
        try:
            filter_dict = {'label': label}
            containers = self._client.containers.list(all=True,
                                                      filters=filter_dict)
            for container in containers:
                container.remove()
        except docker.errors.APIError as ex:
            msg = 'Could not remove containers by label: {0}'.format(label)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def create_network(self, network_name):
        """
        API to create a docker network given the network name unless one
        is already available.

        Args:
            network_name (str): Network name string

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered when creating the network.
        """
        logging.info('Creating network: ' + network_name)
        create_network = False
        try:
            networks = self._client.networks.list(names=[network_name])
            if networks:
                num_networks = len(networks)
                if num_networks == 0:
                    create_network = True
            else:
                create_network = True
            if create_network is True:
                os_name = self.get_os_type()
                if os_name == 'windows':
                    # default network type in Windows is nat
                    self._client.networks.create(network_name, driver="nat")
                else:
                    self._client.networks.create(network_name, driver="bridge")
        except docker.errors.APIError as ex:
            msg = 'Could not create docker network: {0}'.format(network_name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def create(self, image, **kwargs):
        """
        This is essentially a wrapper API on top of docker.container.create using only the
        minimum required arguments and kwargs.

        Arguments:
            image {str} -- Image to be used to create the container

        Kwargs
            detach {bool} -- IF True, run container in the background, False by default.
            environment {dict} -- Environment variable dict using string key and value pairs
            name {str} -- Name of the container
            network {str} -- Name of the network to connect the container to
            ports {dict} -- Specification of dict format described in docker py documentation.
            volumes {dict} -- Specification of dict format described in docker py documentation.
            log_config {dict} -- Specification of dict format described in docker py documentation.
            mounts {list} -- Specification of dict format described in docker py documentation.
            restart_policy {dict} -- Specification of dict format described in
                                     docker py documentation.

        Raises:
            edgectl.errors.EdgeDeploymentError -- Raised when container
            exited with error code, image was not found and any errors reported
            edgectl.errors.EdgeDeploymentError -- [description]
        """

        try:

            detach_bool = kwargs.get('detach', False)
            container_name = kwargs.get('name')
            nw_name = kwargs.get('network')
            env_dict = kwargs.get('environment')
            ports_dict = kwargs.get('ports')
            volume_dict = kwargs.get('volumes')
            log_config_dict = kwargs.get('log_config')

            logging.info('Executing docker create %s  name: %s  detach: %s' \
                         ' network: %s', image, container_name,
                         str(detach_bool), nw_name)
            for key in list(env_dict.keys()):
                logging.debug(' env: %s:%s', key, env_dict[key])
            for key in list(ports_dict.keys()):
                logging.debug(' port: %s:%s', key, str(ports_dict[key]))
            for key in list(volume_dict.keys()):
                logging.debug(' volume: %s:%s, %s', key,
                              volume_dict[key]['bind'], volume_dict[key]['mode'])
            if 'type' in list(log_config_dict.keys()):
                logging.debug(' logging driver: %s', log_config_dict['type'])
            if 'config' in list(log_config_dict.keys()):
                for key in list(log_config_dict['config'].keys()):
                    logging.debug(' log opt: %s:%s',
                                  key, log_config_dict['config'][key])
            self._client.containers.create(image,
                                           detach=detach_bool,
                                           environment=env_dict,
                                           name=container_name,
                                           network=nw_name,
                                           ports=ports_dict,
                                           volumes=volume_dict,
                                           log_config=log_config_dict,
                                           mounts=kwargs.get('mounts'),
                                           restart_policy=kwargs.get('restart_policy'))
        except docker.errors.ContainerError as ex_ctr:
            msg = 'Container exited with errors: {0}'.format(container_name)
            logging.error(msg)
            print(ex_ctr)
            raise edgectl.errors.EdgeDeploymentError(msg, ex_ctr)
        except docker.errors.ImageNotFound as ex_img:
            msg = 'Docker create failed. Image not found: {0}'.format(image)
            logging.error(msg)
            print(ex_img)
            raise edgectl.errors.EdgeDeploymentError(msg, ex_img)
        except docker.errors.APIError as ex:
            msg = 'Docker create failed for image: {0}'.format(image)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def _get_volume_if_exists(self, name):
        logging.debug('Checking if volume exists: %s', name)
        try:
            return self._client.volumes.get(name)
        except docker.errors.NotFound:
            logging.debug('Volume does not exist: %s', name)
            return None
        except docker.errors.APIError as ex:
            msg = 'Docker volume get failed for: {0}'.format(name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def create_volume(self, volume_name):
        """
        API to create a docker volume given the volume name unless one
        is already available.

        Args:
            volume_name (str): Volume name string

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered when creating the volume.
        """
        try:
            volume = self._get_volume_if_exists(volume_name)
            if volume is None:
                logging.info('Creating volume: %s', volume_name)
                self._client.volumes.create(volume_name)
        except docker.errors.APIError as ex:
            msg = 'Docker volume create failed for: {0}'.format(volume_name)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def remove_volume(self, volume_name, force=False):
        """
        API to remove a docker volume given the volume name if one exists.

        Args:
            volume_name (str): Volume name string
            force (bool): Force removal of volumes that were already removed

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered when removing the volume.
        """
        try:
            volume = self._get_volume_if_exists(volume_name)
            if volume is not None:
                logging.info('Removing volume: %s', volume_name)
                volume.remove(force)
        except docker.errors.APIError as ex:
            msg = 'Docker volume remove failed for: {0}, force flag: {1}'.format(volume_name, force)
            logging.error(msg)
            print(ex)
            raise edgectl.errors.EdgeDeploymentError(msg, ex)

    def copy_file_to_volume(self,
                            container_name,
                            volume_dest_file_name,
                            volume_dest_dir_path,
                            host_src_file):
        """
        API to copy a host resident file (host_src_file) to destination path
        within the a volume at path volume_dest_dir_path/volume_dest_file_name.
        The volume is mapped into a container identified by the given
        container name.

        Args:
            container_name (str): Container name string
            volume_dest_file_name (str): Destination file within the volume
            volume_dest_dir_path (str): Destination directory path within the volume
            host_src_file (str): Path on the host of the file to be copied

        Raises:
            edgectl.errors.EdgeDeploymentError if there were problems
            encountered when copying host_src_file to the volume.
        """
        if self.get_os_type() == 'windows':
            self._insert_file_in_volume_mount(volume_dest_dir_path, host_src_file, volume_dest_file_name)
        else:
            self._insert_file_in_container(container_name,
                                           volume_dest_file_name,
                                           volume_dest_dir_path,
                                           host_src_file)

    def _insert_file_in_container(self,
                                  container_name,
                                  volume_dest_file_name,
                                  volume_dest_dir_path,
                                  host_src_file):
        try:
            (tar_stream, dest_archive_info, container_tar_file) = \
                self.create_tar_objects(volume_dest_file_name)
            file_data = open(host_src_file, 'rb').read()
            dest_archive_info.size = len(file_data)
            dest_archive_info.mtime = time.time()
            dest_archive_info.mode = 0o444
            container_tar_file.addfile(dest_archive_info, BytesIO(file_data))
            container_tar_file.close()
            tar_stream.seek(0)
            container = self._get_container_by_name(container_name)
            container.put_archive(volume_dest_dir_path, tar_stream)
        except docker.errors.APIError as docker_ex:
            msg = 'Container put_archive failed for container: {0}'.format(container_name)
            logging.error(msg)
            print(docker_ex)
            raise edgectl.errors.EdgeDeploymentError(msg, docker_ex)
        except (OSError, IOError) as ex_os:
            msg = 'File IO error seen during put archive for container: {0}. ' \
                  'Errno: {1}, Error {2}'.format(container_name, str(ex_os.errno), ex_os.strerror)
            logging.error(msg)
            print(ex_os)
            raise edgectl.errors.EdgeDeploymentError(msg, ex_os)

    def _insert_file_in_volume_mount(self, volume_name, host_src_file, volume_dest_file_name):
        """
        Use volume introspection to place files into the host mountpoint in order
        to work around issues with Docker filesystem operations on Windows
        Hyper-V containers and container mountpoints
        """
        try:
            volume_name = (volume_name.split('/'))[-1]
            volume_info = self._api_client.inspect_volume(volume_name)
            EdgeUtils.copy_files(host_src_file.replace('\\\\', '\\'),
                                 os.path.join(volume_info['Mountpoint'].replace('\\\\', '\\'), volume_dest_file_name))
        except docker.errors.APIError as docker_ex:
            msg = 'Docker volume inspect failed for: {0}'.format(volume_name)
            logging.error(msg)
            print(docker_ex)
            raise edgectl.errors.EdgeDeploymentError(msg, docker_ex)
        except (OSError, IOError) as ex_os:
            msg = 'File IO error seen copying files to volume: {0}. ' \
                  'Errno: {1}, Error {2}'.format(volume_name, str(ex_os.errno), ex_os.strerror)
            logging.error(msg)
            print(ex_os)
            raise edgectl.errors.EdgeDeploymentError(msg, ex_os)

    @staticmethod
    def create_tar_objects(container_dest_file_name):
        """Helper method to create requisite tar file objects needed to add a file to a volume.
        Useful during testing.

        Arguments:
            container_dest_file_name {str} -- Destination file path within the container

        Return
            Tuple of BytesIO stream, TarInfo object and Tarfile object
        """
        tar_stream = BytesIO()
        dest_archive_info = tarfile.TarInfo(name=container_dest_file_name)
        container_tar_file = tarfile.TarFile(fileobj=tar_stream, mode='w')
        return (tar_stream, dest_archive_info, container_tar_file)
