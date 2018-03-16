"""Implementation of tests for module `edgectl.deployment.deploymentdocker.py`."""
from __future__ import print_function
import sys
import unittest
import os
from mock import mock, patch, mock_open, MagicMock, PropertyMock
import docker
import edgectl.errors
from edgectl.host.dockerclient import EdgeDockerClient


if sys.version_info[0] < 3:
    OPEN_BUILTIN = '__builtin__.open'
else:
    OPEN_BUILTIN = 'builtins.open'

# pylint: disable=invalid-name
# pylint: disable=line-too-long
# pylint: disable=too-many-lines
# pylint: disable=too-many-lines
# pylint: disable=no-self-use
# pylint: disable=too-many-public-methods
# pylint: disable=too-many-arguments
class TestEdgeDockerClientCheckAvailability(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.check_availability"""
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_check_availability_valid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when API check_availability returns true
        """
        # arrange
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        result = client.check_availability()

        # assert
        mock_docker_client.info.assert_called_with()
        self.assertTrue(result)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_check_availability_invalid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when API check_availability returns false
        """
        # arrange
        mock_docker_client.info.side_effect = docker.errors.APIError('docker unavailable')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        result = client.check_availability()

        # assert
        mock_docker_client.info.assert_called_with()
        self.assertFalse(result)


class TestEdgeDockerClientLogin(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.login"""

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_login_valid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when API login is called using valid input arguments
        """
        # arrange
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        address = 'test_address'
        uname = 'test_user'
        password = 'test_pass'
        # act

        client.login(address, uname, password)

        # assert
        mock_docker_client.login.assert_called_with(username=uname,
                                                    password=password, registry=address)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_login_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker login raises an exception
        """
        # arrange
        mock_docker_client.login.side_effect = docker.errors.APIError('login fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        address = 'test_address'
        uname = 'test_user'
        password = 'test_pass'

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.login(address, uname, password)


class TestEdgeDockerClientGetOSType(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.get_os_type"""

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_get_os_valid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker client API info returns a valid OSType
        """
        # arrange
        os_type = 'TEST_OS'
        mock_docker_client.info.return_value = {'OSType': os_type}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        result = client.get_os_type()

        # assert
        mock_docker_client.info.assert_called_with()
        self.assertEqual(result, os_type.lower())

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_get_os_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker info raises an exception
        """
        # arrange
        mock_docker_client.info.side_effect = docker.errors.APIError('info fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeError):
            client.get_os_type()


class TestEdgeDockerClientGetLocalImageSHAId(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.get_local_image_sha_id"""

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_get_local_image_sha_id_valid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker client API inspect_image returns a valid id
        """
        # arrange
        test_id = '1234'
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'

        # act
        result = client.get_local_image_sha_id(image)

        # assert
        mock_docker_api_client.inspect_image.assert_called_with(image)
        self.assertEqual(result, test_id)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_get_local_image_sha_id_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker api inspect_image raises an exception
        """
        # arrange
        mock_docker_api_client.inspect_image.side_effect = docker.errors.APIError('inspect fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'

        # act
        result = client.get_local_image_sha_id(image)

        # assert
        mock_docker_api_client.inspect_image.assert_called_with(image)
        self.assertEqual(result, None)


class TestEdgeDockerClientPull(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.pull"""

    @mock.patch('edgectl.host.dockerclient.EdgeDockerClient.get_local_image_sha_id')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_pull_image_exists_locally_with_no_newer_image_valid(self,
                                                                 mock_docker_client,
                                                                 mock_docker_api_client,
                                                                 mock_get_local_id):
        """
            Tests call stack when docker client pull is called with a locally avilable image
            and no newer image available in the registry
        """
        # arrange
        test_id = '1234'
        mock_get_local_id.return_value = test_id
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'
        username = "test_user"
        password = "test_password"
        auth_dict = {'username': username, 'password': password}

        # act
        result = client.pull(image, username, password)

        # assert
        mock_get_local_id.assert_called_with(image)
        mock_docker_api_client.inspect_image.assert_called_with(image)
        mock_docker_client.images.pull.assert_called_with(image, auth_config=auth_dict)
        self.assertFalse(result)

    @mock.patch('edgectl.host.dockerclient.EdgeDockerClient.get_local_image_sha_id')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_pull_image_exists_locally_with_newer_image_valid(self,
                                                              mock_docker_client,
                                                              mock_docker_api_client,
                                                              mock_get_local_id):
        """
            Tests call stack when docker client pull is called with a locally avilable image
            and a newer image available in the registry
        """
        # arrange
        test_id = '1234'
        mock_get_local_id.return_value = '1000'
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'
        username = "test_user"
        password = "test_password"
        auth_dict = {'username': username, 'password': password}

        # act
        result = client.pull(image, username, password)

        # assert
        mock_get_local_id.assert_called_with(image)
        mock_docker_api_client.inspect_image.assert_called_with(image)
        mock_docker_client.images.pull.assert_called_with(image, auth_config=auth_dict)
        self.assertTrue(result)

    @mock.patch('edgectl.host.dockerclient.EdgeDockerClient.get_local_image_sha_id')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_pull_image_exists_locally_with_newer_image_no_credentials_valid(self,
                                                                             mock_docker_client,
                                                                             mock_docker_api_client,
                                                                             mock_get_local_id):
        """
            Tests call stack when docker client pull is called with a locally avilable image
            and no newer image available in the registry to be accessed without any credentials
        """
        # arrange
        test_id = '1234'
        mock_get_local_id.return_value = '1000'
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'
        auth_dict = None

        # act
        result = client.pull(image, None, None)

        # assert
        mock_get_local_id.assert_called_with(image)
        mock_docker_api_client.inspect_image.assert_called_with(image)
        mock_docker_client.images.pull.assert_called_with(image, auth_config=auth_dict)
        self.assertTrue(result)

    @mock.patch('edgectl.host.dockerclient.EdgeDockerClient.get_local_image_sha_id')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_pull_image_no_image_exists_locally(self,
                                                mock_docker_client,
                                                mock_docker_api_client,
                                                mock_get_local_id):
        """
            Tests call stack when docker client pull is called with no locally avilable image
        """
        # arrange
        test_id = '1234'
        mock_get_local_id.return_value = None
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'
        username = "test_user"
        password = "test_password"
        auth_dict = {'username': username, 'password': password}

        # act
        result = client.pull(image, username, password)

        # assert
        mock_get_local_id.assert_called_with(image)
        mock_docker_api_client.inspect_image.assert_not_called()
        mock_docker_client.images.pull.assert_called_with(image, auth_config=auth_dict)
        self.assertTrue(result)

    @mock.patch('edgectl.host.dockerclient.EdgeDockerClient.get_local_image_sha_id')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_pull_raises_exception(self,
                                   mock_docker_client,
                                   mock_docker_api_client,
                                   mock_get_local_id):
        """
            Tests call stack when docker client pull raises exeception
        """
        # arrange
        test_id = '1234'
        mock_get_local_id.return_value = None
        mock_docker_api_client.inspect_image.return_value = {'Id': test_id}
        mock_docker_client.images.pull.side_effect = docker.errors.APIError('docker unavailable')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        image = 'test_image'
        username = "test_user"
        password = "test_password"

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.pull(image, username, password)


class TestContainerSpec(docker.models.containers.Container):
    """
        Class used in mock autospec for containers
    """
    name = 'name'
    status = 'status'
    def stop(self, **kwargs):
        """ Mock stop method """
        pass
    def start(self, **kwargs):
        """ Mock start method """
        pass
    def remove(self, **kwargs):
        """ Mock remove method """
        pass


class TestEdgeDockerContainerOps(unittest.TestCase):
    """
        Unit tests for APIs
            EdgeDockerClient.start
            EdgeDockerClient.restart
            EdgeDockerClient.stop
            EdgeDockerClient.remove
            EdgeDockerClient.status
            EdgeDockerClient.stop_by_label
            EdgeDockerClient.remove_by_label
            EdgeDockerClient.create
    """
    TEST_CONTAINER_NAME = 'test_name'
    TEST_LABEL = 'test_label'

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_start_valid(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests execution of a valid start command
        """
        # arrange
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.start(self.TEST_CONTAINER_NAME)

        # assert
        mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
        mock_container.start.assert_called_with()

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_start_fails_raises_exception(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests whether EdgeDeploymentError is raised when docker container start fails
        """
        # arrange
        mock_container.start.side_effect = docker.errors.APIError('start failure')
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.start(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_start_invalid_container_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker container start fails
        """
        # arrange
        mock_docker_client.containers.get.side_effect = docker.errors.NotFound('invalid image')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.start(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_restart_valid(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests execution of a valid restart command
        """
        # arrange
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.restart(self.TEST_CONTAINER_NAME)

        # assert
        mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
        mock_container.restart.assert_called_with(timeout=5)

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_restart_with_args_valid(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests execution of a valid restart command with args
        """
        # arrange
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.restart(self.TEST_CONTAINER_NAME, timeout_int=50)

        # assert
        mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
        mock_container.restart.assert_called_with(timeout=50)

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_restart_fails_raises_exception(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests whether EdgeDeploymentError is raised when docker container restart fails
        """
        # arrange
        mock_container.restart.side_effect = docker.errors.APIError('restart failure')
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.restart(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_restart_invalid_container_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker container restart fails
        """
        # arrange
        mock_docker_client.containers.get.side_effect = docker.errors.NotFound('invalid image')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.restart(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_stop_valid(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests execution of a valid stop command
        """
        # arrange
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.stop(self.TEST_CONTAINER_NAME)

        # assert
        mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
        mock_container.stop.assert_called_with()

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_stop_fails_raises_exception(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests whether EdgeDeploymentError is raised when docker container stop fails
        """
        # arrange
        mock_container.stop.side_effect = docker.errors.APIError('stop failure')
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.stop(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_stop_invalid_container_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker container stop fails
        """
        # arrange
        mock_docker_client.containers.get.side_effect = docker.errors.NotFound('invalid image')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.stop(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_valid(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests execution of a valid remove command
        """
        # arrange
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.remove(self.TEST_CONTAINER_NAME)

        # assert
        mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
        mock_container.remove.assert_called_with()

    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_fails_raises_exception(self, mock_docker_client, mock_docker_api_client, mock_container):
        """
            Tests whether EdgeDeploymentError is raised when docker container remove fails
        """
        # arrange
        mock_container.remove.side_effect = docker.errors.APIError('remove failure')
        mock_docker_client.containers.get.return_value = mock_container
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.remove(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_invalid_container_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker container remove fails
        """
        # arrange
        mock_docker_client.containers.get.side_effect = docker.errors.NotFound('invalid image')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.remove(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_status_valid(self, mock_docker_client, mock_docker_api_client,
                          mock_container, mock_non_match_container):
        """
            Tests execution of a valid status command
        """
        # @note when setting the container object using autospec=True, it could not
        # set the properties name and status of the mock object.
        # Thus we resort to using TestContainerSpec as the autospec where these are
        # settable. It should be noted that for the status test it was sufficient to use
        # @mock.patch('docker.models.containers.Container') directly but we are using
        # TestContainerSpec for consistency

        # arrange
        test_status = 'running'
        type(mock_container).status = PropertyMock(return_value=test_status)
        type(mock_container).name = PropertyMock(return_value=self.TEST_CONTAINER_NAME)
        type(mock_non_match_container).status = PropertyMock(return_value='running')
        type(mock_non_match_container).name = PropertyMock(return_value='blah')
        mock_docker_client.containers.list.return_value = [mock_non_match_container, mock_container]
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        result = client.status(self.TEST_CONTAINER_NAME)

        # assert
        mock_docker_client.containers.list.assert_called_with(all=True)
        self.assertEqual(test_status, result)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_status_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker containers list fails
        """
        # arrange
        mock_docker_client.containers.list.side_effect = docker.errors.APIError('list failure')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.status(self.TEST_CONTAINER_NAME)

    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_stop_by_label_valid(self, mock_docker_client, mock_docker_api_client,
                                 mock_container1, mock_container2):
        """
            Tests execution of a valid stop by label command
        """
        # @note when setting multiple container mocks autospec=True failed which is
        # why we resort to using TestContainerSpec as the autospec class

        # arrange
        mock_docker_client.containers.list.return_value = [mock_container1, mock_container2]
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        filter_dict = {'label': self.TEST_LABEL}

        # act
        client.stop_by_label(self.TEST_LABEL)

        # assert
        mock_docker_client.containers.list.assert_called_with(all=True, filters=filter_dict)
        mock_container1.stop.assert_called_with()
        mock_container2.stop.assert_called_with()

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_stop_by_label_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker containers list fails
        """
        # arrange
        mock_docker_client.containers.list.side_effect = docker.errors.APIError('list failure')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.stop_by_label(self.TEST_LABEL)

    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.models.containers.Container', autospec=TestContainerSpec)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_by_label_valid(self, mock_docker_client, mock_docker_api_client,
                                   mock_container1, mock_container2):
        """
            Tests execution of a valid remove by label command
        """
        # @note when setting multiple container mocks autospec=True failed which is
        # why we resort to using TestContainerSpec as the autospec class

        # arrange
        mock_docker_client.containers.list.return_value = [mock_container1, mock_container2]
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        filter_dict = {'label': self.TEST_LABEL}

        # act
        client.remove_by_label(self.TEST_LABEL)

        # assert
        mock_docker_client.containers.list.assert_called_with(all=True, filters=filter_dict)
        mock_container1.remove.assert_called_with()
        mock_container2.remove.assert_called_with()

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_by_label_raises_exception(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker containers list fails
        """
        # arrange
        mock_docker_client.containers.list.side_effect = docker.errors.APIError('list failure')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.remove_by_label(self.TEST_LABEL)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_valid(self, mock_docker_client, mock_docker_api_client):
        """
            Tests execution of a valid docker create container command
        """
        # arrange
        image = 'test_image'
        container_name = 'test_name'
        detach_bool = True
        env_dict = {'test_key_env': 'test_val_env'}
        nw_name = 'test_network_name'
        ports_dict = {'test_key_ports': 'test_val_ports'}
        volume_dict = {'test_key_volume': {'bind': 'test_val_bind', 'mode': 'test_val_mode'}}
        log_config_dict = {'type': 'test_val_log', 'config': {'opt1':'val1'}}
        mounts_list = ['mount1', 'mount2']
        restart_policy_dict = {'test_key_restart': 'test_val_restart'}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create(image,
                      name=container_name,
                      detach=detach_bool,
                      environment=env_dict,
                      network=nw_name,
                      ports=ports_dict,
                      volumes=volume_dict,
                      log_config=log_config_dict,
                      mounts=mounts_list,
                      restart_policy=restart_policy_dict)

        # assert
        mock_docker_client.containers.create.assert_called_with(image,
                                                                detach=detach_bool,
                                                                environment=env_dict,
                                                                name=container_name,
                                                                network=nw_name,
                                                                ports=ports_dict,
                                                                volumes=volume_dict,
                                                                log_config=log_config_dict,
                                                                mounts=mounts_list,
                                                                restart_policy=restart_policy_dict)

    def _create_common_invocation(self, client):
        image = 'test_image'
        container_name = 'test_name'
        detach_bool = True
        env_dict = {'test_key_env': 'test_val_env'}
        nw_name = 'test_network_name'
        ports_dict = {'test_key_ports': 'test_val_ports'}
        volume_dict = {'test_key_volume': {'bind': 'test_val_bind', 'mode': 'test_val_mode'}}
        log_config_dict = {'type': 'test_val_log', 'config': {'opt1':'val1'}}
        mounts_list = ['mount1', 'mount2']
        restart_policy_dict = {'test_key_restart': 'test_val_restart'}

        # act
        client.create(image,
                      name=container_name,
                      detach=detach_bool,
                      environment=env_dict,
                      network=nw_name,
                      ports=ports_dict,
                      volumes=volume_dict,
                      log_config=log_config_dict,
                      mounts=mounts_list,
                      restart_policy=restart_policy_dict)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_raises_except_when_containerError_is_raised(self,
                                                                mock_docker_client,
                                                                mock_docker_api_client):
        """
            Tests execution of create container raises exception edgectl.errors.EdgeDeploymentError
            when docker client API create raises ContainerError
        """
        # arrange
        except_obj = docker.errors.ContainerError('container', 1, 'cmd', 'image', 'stderr')
        mock_docker_client.containers.create.side_effect = except_obj
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            self._create_common_invocation(client)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_raises_except_when_ImageNotFound_is_raised(self,
                                                               mock_docker_client,
                                                               mock_docker_api_client):
        """
            Tests execution of create container raises exception edgectl.errors.EdgeDeploymentError
            when docker client API create raises ImageNotFound
        """
        # arrange
        except_obj = docker.errors.ImageNotFound('image error')
        mock_docker_client.containers.create.side_effect = except_obj
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            self._create_common_invocation(client)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_raises_except_when_APIError_is_raised(self,
                                                          mock_docker_client,
                                                          mock_docker_api_client):
        """
            Tests execution of create container raises exception edgectl.errors.EdgeDeploymentError
            when docker client API create raises APIError
        """
        # arrange
        except_obj = docker.errors.APIError('image error')
        mock_docker_client.containers.create.side_effect = except_obj
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            self._create_common_invocation(client)

class TestEdgeDockerNetworkCreate(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.create_network"""

    TEST_NETWORK = 'test_network'

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_nw_create_no_networks_exist_linux(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker network create is called when there are no networks
            available for Linux type OS.
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.networks.list.return_value = None
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)


        # act
        client.create_network(self.TEST_NETWORK)

        # assert
        mock_docker_client.networks.create.assert_called_with(self.TEST_NETWORK, driver='bridge')

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_nw_create_no_networks_exist_windows(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker network create is called when there are no networks
            available for Windows type OS.
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Windows'}
        mock_docker_client.networks.list.return_value = None
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create_network(self.TEST_NETWORK)

        # assert
        mock_docker_client.networks.create.assert_called_with(self.TEST_NETWORK, driver='nat')

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_nw_create_other_non_matching_networks_exist(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker network create is called when there are
            other networks available that do not match the provided network name.
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.networks.list.return_value = []
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create_network(self.TEST_NETWORK)

        # assert
        mock_docker_client.networks.create.assert_called_with(self.TEST_NETWORK, driver='bridge')

    @mock.patch('docker.models.networks.Network', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_nw_create_network_exists(self, mock_docker_client, mock_docker_api_client, mock_network):
        """
            Tests call stack when docker network create is called when there are
            other networks available that do not match the provided network name.
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.networks.list.return_value = [mock_network]
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create_network(self.TEST_NETWORK)

        # assert
        mock_docker_client.networks.create.assert_not_called()

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_network_raises_exception_when_info_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker info list fails
        """
        # arrange
        mock_docker_client.info.side_effect = docker.errors.APIError('info failure')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.create_network(self.TEST_NETWORK)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_network_raises_exception_when_list_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker network list fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.networks.list.side_effect = docker.errors.APIError('list failure')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.create_network(self.TEST_NETWORK)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_network_raises_exception_when_create_fails(self, mock_docker_client, mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker network list fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.networks.list.return_value = None
        mock_docker_client.networks.create.side_effect = docker.errors.APIError('nw create failed')

        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.create_network(self.TEST_NETWORK)


class TestEdgeDockerVolumes(unittest.TestCase):
    """Unit tests for API EdgeDockerClient.create_volume and EdgeDockerClient.remove_volume"""

    TEST_CONTAINER_NAME = 'test_container'
    TEST_VOLUME_NAME = 'test_volume'

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_volume_when_it_does_not_exist(self, mock_docker_client, mock_docker_api_client):
        """
            Tests call stack when docker volume create is called when the volume does not exist.
        """
        # arrange
        mock_docker_client.volumes.get.side_effect = docker.errors.NotFound('no volume exists')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create_volume(self.TEST_VOLUME_NAME)

        # assert
        mock_docker_client.volumes.create.assert_called_with(self.TEST_VOLUME_NAME)

    @mock.patch('docker.models.volumes.Volume', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_volume_when_volume_exits(self, mock_docker_client,
                                             mock_docker_api_client, mock_volume):
        """
            Tests call stack when docker volume create is not called when the volume exists.
        """
        # arrange
        mock_docker_client.volumes.get.return_value = mock_volume
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.create_volume(self.TEST_VOLUME_NAME)

        # assert
        mock_docker_client.volumes.create.assert_not_called()

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_volume_raises_exception_when_volume_get_fails(self,
                                                                  mock_docker_client,
                                                                  mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker volume get fails
        """
        # arrange
        mock_docker_client.volumes.get.side_effect = docker.errors.APIError('volume get fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.create_volume(self.TEST_VOLUME_NAME)
            mock_docker_client.volumes.create.assert_not_called()

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_create_volume_raises_exception_when_volume_create_fails(self,
                                                                     mock_docker_client,
                                                                     mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker volume create fails
        """
        # arrange
        mock_docker_client.volumes.get.side_effect = docker.errors.NotFound('no volume exists')
        mock_docker_client.volumes.create.side_effect = docker.errors.APIError('vol create fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.create_volume(self.TEST_VOLUME_NAME)
            mock_docker_client.volumes.create.assert_not_called()

    @mock.patch('docker.models.volumes.Volume', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_volume_when_volume_exits(self, mock_docker_client,
                                             mock_docker_api_client, mock_volume):
        """
            Tests call stack when docker volume remove is called when the volume exists.
        """
        # arrange
        mock_docker_client.volumes.get.return_value = mock_volume
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act
        client.remove_volume(self.TEST_VOLUME_NAME)

        # assert
        mock_volume.remove.assert_called_with(False)

    @mock.patch('docker.models.volumes.Volume', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_volume_with_args_when_volume_exits(self, mock_docker_client,
                                                       mock_docker_api_client, mock_volume):
        """
            Tests call stack when docker volume remove is called when the volume exists.
        """
        # arrange
        mock_docker_client.volumes.get.return_value = mock_volume
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        force_flag = True

        # act
        client.remove_volume(self.TEST_VOLUME_NAME, force_flag)

        # assert
        mock_volume.remove.assert_called_with(force_flag)

    @mock.patch('docker.models.volumes.Volume', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_volume_raises_exception_when_volume_get_fails(self,
                                                                  mock_docker_client,
                                                                  mock_docker_api_client,
                                                                  mock_volume):
        """
            Tests whether EdgeDeploymentError is raised when docker volume get fails
        """
        # arrange
        mock_docker_client.volumes.get.return_value = mock_volume
        mock_docker_client.volumes.get.side_effect = docker.errors.APIError('volume get fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.remove_volume(self.TEST_VOLUME_NAME)
            mock_volume.remove.assert_not_called()

    @mock.patch('docker.models.volumes.Volume', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_remove_volume_raises_exception_when_volume_remove_fails(self,
                                                                     mock_docker_client,
                                                                     mock_docker_api_client,
                                                                     mock_volume):
        """
            Tests whether EdgeDeploymentError is raised when docker volume remove fails
        """
        # arrange
        mock_volume.remove.side_effect = docker.errors.APIError('vol remove fails')
        mock_docker_client.volumes.get.return_value = mock_volume
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.remove_volume(self.TEST_VOLUME_NAME)
            mock_volume.remove.assert_called_with(True)

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_copy_file_to_volume_raises_exception_when_info_fails(self,
                                                                  mock_docker_client,
                                                                  mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker info fails
        """
        # arrange
        mock_docker_client.info.side_effect = docker.errors.APIError('info fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.utils.EdgeUtils.copy_files')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_copy_file_to_volume_windows_valid(self,
                                               mock_docker_client,
                                               mock_docker_api_client,
                                               mock_copy_utils):
        """
            Tests a valid invocation of copy_file_to_volume
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Windows'}
        mock_docker_api_client.inspect_volume.return_value = {'Mountpoint': '\\\\some_path\\\\mount\\'}
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'

        # act
        client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

        # arrange
        mock_docker_api_client.inspect_volume(dest_dir)
        mock_copy_utils.assert_called_with(src_file, os.path.join('\\some_path\\mount\\', dest_file))

    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_copy_file_to_volume_raises_exception_when_vol_inspect_fails(self,
                                                                         mock_docker_client,
                                                                         mock_docker_api_client):
        """
            Tests whether EdgeDeploymentError is raised when docker volume inspect fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Windows'}
        mock_docker_api_client.inspect_volume.side_effect = docker.errors.APIError('inspect fails')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.utils.EdgeUtils.copy_files')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_insert_file_win_raises_exception_when_copy_files_raises_os_except(self,
                                                                               mock_docker_client,
                                                                               mock_docker_api_client,
                                                                               mock_copy_utils):
        """
            Tests whether EdgeDeploymentError is raised copy host files into volume fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Windows'}
        mock_docker_api_client.inspect_volume.return_value = {'Mountpoint': '\\\\some_path\\\\mount\\'}
        mock_copy_utils.side_effect = OSError('os access error')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.utils.EdgeUtils.copy_files')
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_insert_file_win_raises_exception_when_copy_files_raises_io_except(self,
                                                                               mock_docker_client,
                                                                               mock_docker_api_client,
                                                                               mock_copy_utils):
        """
            Tests whether EdgeDeploymentError is raised copy host files into volume fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Windows'}
        mock_docker_api_client.inspect_volume.return_value = {'Mountpoint': '\\\\some_path\\\\mount\\'}
        mock_copy_utils.side_effect = IOError('io access error')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'

        # act, assert
        with self.assertRaises(edgectl.errors.EdgeDeploymentError):
            client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.host.EdgeDockerClient.create_tar_objects')
    @mock.patch('tarfile.TarFile', autospec=True)
    @mock.patch('tarfile.TarInfo', autospec=True)
    @mock.patch('io.BytesIO', autospec=True)
    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_ins_file_in_ctr_linux_raises_except_when_io_except_raised(self,
                                                                       mock_docker_client,
                                                                       mock_docker_api_client,
                                                                       mock_container,
                                                                       mock_tar_stream,
                                                                       mock_tarinfo,
                                                                       mock_tarfile,
                                                                       mock_tar_factory):
        """
            Tests whether EdgeDeploymentError is raised when opening host file fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.containers.get.return_value = mock_container
        mock_tar_factory.return_value = (mock_tar_stream, mock_tarinfo, mock_tarfile)
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'
        mocked_data = b'MOCKED_DATA'
        test_time_value = 1518825244.88

        # act, assert
        with patch('time.time', MagicMock(return_value=test_time_value)):
            with patch(OPEN_BUILTIN, mock_open(read_data=mocked_data)) as mocked_open:
                with self.assertRaises(edgectl.errors.EdgeDeploymentError):
                    mocked_open.side_effect = IOError('open io except')
                    client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.host.EdgeDockerClient.create_tar_objects')
    @mock.patch('tarfile.TarFile', autospec=True)
    @mock.patch('tarfile.TarInfo', autospec=True)
    @mock.patch('io.BytesIO', autospec=True)
    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_ins_file_in_ctr_linux_raises_except_when_os_except_raised(self,
                                                                       mock_docker_client,
                                                                       mock_docker_api_client,
                                                                       mock_container,
                                                                       mock_tar_stream,
                                                                       mock_tarinfo,
                                                                       mock_tarfile,
                                                                       mock_tar_factory):
        """
            Tests whether EdgeDeploymentError is raised when opening host file fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.containers.get.return_value = mock_container
        mock_tar_factory.return_value = (mock_tar_stream, mock_tarinfo, mock_tarfile)
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'
        mocked_data = b'MOCKED_DATA'
        test_time_value = 1518825244.88

        # act, assert
        with patch('time.time', MagicMock(return_value=test_time_value)):
            with patch(OPEN_BUILTIN, mock_open(read_data=mocked_data)) as mocked_open:
                with self.assertRaises(edgectl.errors.EdgeDeploymentError):
                    mocked_open.side_effect = OSError('open os except')
                    client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.host.EdgeDockerClient.create_tar_objects')
    @mock.patch('tarfile.TarFile', autospec=True)
    @mock.patch('tarfile.TarInfo', autospec=True)
    @mock.patch('io.BytesIO', autospec=True)
    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_ins_file_in_ctr_linux_raises_except_when_put_archive_fails(self,
                                                                        mock_docker_client,
                                                                        mock_docker_api_client,
                                                                        mock_container,
                                                                        mock_tar_stream,
                                                                        mock_tarinfo,
                                                                        mock_tarfile,
                                                                        mock_tar_factory):
        """
            Tests whether EdgeDeploymentError is raised when container put archive fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.containers.get.return_value = mock_container
        mock_tar_factory.return_value = (mock_tar_stream, mock_tarinfo, mock_tarfile)
        mock_container.put_archive.side_effect = docker.errors.APIError('put archive error')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'
        mocked_data = b'MOCKED_DATA'
        test_time_value = 1518825244.88

        # act, assert
        with patch('time.time', MagicMock(return_value=test_time_value)):
            with patch(OPEN_BUILTIN, mock_open(read_data=mocked_data)):
                with self.assertRaises(edgectl.errors.EdgeDeploymentError):
                    client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.host.EdgeDockerClient.create_tar_objects')
    @mock.patch('tarfile.TarFile', autospec=True)
    @mock.patch('tarfile.TarInfo', autospec=True)
    @mock.patch('io.BytesIO', autospec=True)
    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_ins_file_in_ctr_linux_raises_except_when_container_get_fails(self,
                                                                          mock_docker_client,
                                                                          mock_docker_api_client,
                                                                          mock_container,
                                                                          mock_tar_stream,
                                                                          mock_tarinfo,
                                                                          mock_tarfile,
                                                                          mock_tar_factory):
        """
            Tests whether EdgeDeploymentError is raised when container get fails
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.containers.get.return_value = mock_container
        mock_tar_factory.return_value = (mock_tar_stream, mock_tarinfo, mock_tarfile)
        mock_docker_client.containers.get.side_effect = docker.errors.APIError('get error')
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'
        mocked_data = b'MOCKED_DATA'
        test_time_value = 1518825244.88

        # act, assert
        with patch('time.time', MagicMock(return_value=test_time_value)):
            with patch(OPEN_BUILTIN, mock_open(read_data=mocked_data)):
                with self.assertRaises(edgectl.errors.EdgeDeploymentError):
                    client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

    @mock.patch('edgectl.host.EdgeDockerClient.create_tar_objects')
    @mock.patch('tarfile.TarFile', autospec=True)
    @mock.patch('tarfile.TarInfo', autospec=True)
    @mock.patch('io.BytesIO', autospec=True)
    @mock.patch('docker.models.containers.Container', autospec=True)
    @mock.patch('docker.APIClient', autospec=True)
    @mock.patch('docker.DockerClient', autospec=True)
    def test_copy_file_to_volume_linux_valid(self,
                                             mock_docker_client,
                                             mock_docker_api_client,
                                             mock_container,
                                             mock_tar_stream,
                                             mock_tarinfo,
                                             mock_tarfile,
                                             mock_tar_factory):
        """
            Tests a valid invocation of copy_file_to_volume for docker OS type linux
        """
        # arrange
        mock_docker_client.info.return_value = {'OSType': 'Linux'}
        mock_docker_client.containers.get.return_value = mock_container
        mock_tar_factory.return_value = (mock_tar_stream, mock_tarinfo, mock_tarfile)
        client = EdgeDockerClient.create_instance(mock_docker_client, mock_docker_api_client)
        src_file = 'src.txt'
        dest_file = 'dest.txt'
        dest_dir = 'dest'
        mocked_data = b'MOCKED_DATA'
        test_time_value = 1518825244.88

        # act
        with patch('time.time', MagicMock(return_value=test_time_value)):
            with patch(OPEN_BUILTIN, mock_open(read_data=mocked_data)) as mocked_open:
                client.copy_file_to_volume(self.TEST_CONTAINER_NAME, dest_file, dest_dir, src_file)

                # assert
                #mock_tarfile.assert_called_with(fileobj=mock_tar_stream, mode='w')
                mocked_open.assert_called_with(src_file, 'rb')
                self.assertEqual(mock_tarinfo.size, len(mocked_data))
                self.assertEqual(mock_tarinfo.mtime, test_time_value)
                self.assertEqual(mock_tarinfo.mode, 0o444)
                mock_tarfile.addfile.assert_called()
                mock_tarfile.close.assert_called_with()
                mock_tar_stream.seek.assert_called_with(0)
                mock_docker_client.containers.get.assert_called_with(self.TEST_CONTAINER_NAME)
                mock_container.put_archive(dest_dir, mock_tar_stream)

if __name__ == '__main__':
    test_classes = [
        TestEdgeDockerClientCheckAvailability,
        TestEdgeDockerClientLogin,
        TestEdgeDockerClientGetOSType,
        TestEdgeDockerClientGetLocalImageSHAId,
        TestEdgeDockerClientPull,
        TestEdgeDockerContainerOps,
        TestEdgeDockerNetworkCreate,
        TestEdgeDockerVolumes,
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
