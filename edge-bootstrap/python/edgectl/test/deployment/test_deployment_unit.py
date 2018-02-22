"""Implementation of tests for module `edgectl.deployment.deploymentdocker.py`."""
from __future__ import print_function
import unittest
from mock import mock, patch, MagicMock
import docker
from edgectl.deployment import EdgeCommandFactory
from edgectl.deployment import EdgeDeploymentCommand
from edgectl.deployment.deploymentdocker import EdgeDeploymentCommandDocker
from edgectl.config import EdgeHostConfig
from edgectl.config import EdgeDeploymentConfigDocker
from edgectl.errors import EdgeDeploymentError
from edgectl.errors import EdgeValueError
from edgectl.config.edgeconstants import EdgeUpstreamProtocol

EDGE_AGENT_DOCKER_CONTAINER_NAME = 'edgeAgent'
EDGE_MODULES_LABEL = 'net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent'
EDGE_HUB_VOL_NAME = 'edgehub'
EDGE_MODULE_VOL_NAME = 'edgemodule'

# pylint: disable=C0103
# disables invalid method name warning which is triggered because the test names are long
# pylint: disable=C0301
# disables line too long pylint warning
# pylint: disable=R0913
# disables too many arguments
# pylint: disable=C0302
# disables too many lines in module
# pylint: disable=R0201
# disables method could be a function
class TestEdgeDeploymentDockerStatus(unittest.TestCase):
    """Unit tests for class EdgeDeploymentCommandDocker.status"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)
        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.status()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.status()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        result = command.status()

        # assert
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE, result)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        result = command.status()

        # assert
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED, result)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        result = command.status()

        # assert
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING, result)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        result = command.status()

        # assert
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING, result)

class TestEdgeDeploymentDockerLogin(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.login"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.login()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.login()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.login()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.login()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.login()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_not_called()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client, mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.login()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_not_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.pull.assert_not_called()
        mock_deployment_start.assert_called()

class TestEdgeDeploymentDockerUpdate(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.update"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.update()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.update()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """

        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.update()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_called_with('testServer0/testImage:testTag',
                                            'testUsername0', 'testPassword0')

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.update()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_called_with('testServer0/testImage:testTag',
                                            'testUsername0', 'testPassword0')

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.update()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_not_called()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client, mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.update()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.pull.assert_called_with('testServer0/testImage:testTag',
                                            'testUsername0', 'testPassword0')
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_deployment_start.assert_called()

class TestEdgeDeploymentDockerStop(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.stop"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.stop()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.stop()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.stop()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.stop_by_label.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.stop()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.stop()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.stop_by_label.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.stop()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)

class TestEdgeDeploymentDockerRestart(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.restart"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.restart()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.restart()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client, mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.restart()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_deployment_start.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.restart()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.restart()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.stop_by_label.assert_not_called()
        mock_client.start.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.restart()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

class TestEdgeDeploymentDockerUninstall(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.uninstall"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.uninstall()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.uninstall()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.uninstall()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.remove.assert_not_called()
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.uninstall()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.uninstall()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.uninstall()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

class TestEdgeDeploymentDockerSetup(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.setup"""

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.setup()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.setup()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_unavailable_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.setup()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_not_called()
        mock_client.remove.assert_not_called()
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'stopped'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.setup()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'restarting'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.setup()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_valid(self, mock_client):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'linux'
        mock_client.status.return_value = 'running'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.setup()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client.remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_client.remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

class TestEdgeDeploymentDockerStart(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.start"""

    COMMAND_NAME = 'start'
    NETWORK_NAME = 'azure-iot-edge'
    LINUX_EDGE_HUB_VOL_PATH = '/mnt/edgehub'
    LINUX_EDGE_MODULE_VOL_PATH = '/mnt/edgemodule'
    WINDOWS_EDGE_HUB_VOL_PATH = 'c:/mnt/edgehub'
    WINDOWS_EDGE_MODULE_VOL_PATH = 'c:/mnt/edgemodule'
    CA_CERT_FILE = 'ca_cert.pem'
    CHAIN_CERT_FILE = 'chain_cert.pem'
    HUB_CERT_FILE = 'hub_cert.pem'
    MOCK_CA_CERT_DICT = {'file_name': CA_CERT_FILE, 'file_path': '/test/' + CA_CERT_FILE}
    MOCK_CHAIN_CERT_DICT = {'file_name': CHAIN_CERT_FILE, 'file_path': '/test/' + CHAIN_CERT_FILE}
    MOCK_HUB_CERT_DICT = {'file_name': HUB_CERT_FILE, 'file_path': '/test/' + HUB_CERT_FILE}
    UNIX_DOCKER_ENDPOINT = '/var/run/docker.sock'

    ENV_COMMON_DICT = {
        'DockerUri': 'https://myhost:1234',
        'DeviceConnectionString': 'HostName=a;DeviceId=b;SharedAccessKey=c',
        'EdgeDeviceHostName': 'testhostname',
        'NetworkId': NETWORK_NAME,
        'RuntimeLogLevel': 'debug',
        'DockerRegistryAuth__0__serverAddress': 'testServer0',
        'DockerRegistryAuth__0__username': 'testUsername0',
        'DockerRegistryAuth__0__password': 'testPassword0',
        'DockerRegistryAuth__1__serverAddress': 'testServer1',
        'DockerRegistryAuth__1__username': 'testUsername1',
        'DockerRegistryAuth__1__password': 'testPassword1',
        'DockerLoggingDriver': 'testDriver',
        'DockerLoggingOptions__testOpt0': '0',
        'DockerLoggingOptions__testOpt1': '1',
        'EdgeHubVolumeName': EDGE_HUB_VOL_NAME,
        'EdgeModuleVolumeName': EDGE_MODULE_VOL_NAME
    }
    ENV_LINUX_DICT = {
        'EdgeHubVolumePath': LINUX_EDGE_HUB_VOL_PATH,
        'EdgeModuleVolumePath': LINUX_EDGE_MODULE_VOL_PATH,
        'EdgeModuleCACertificateFile': LINUX_EDGE_MODULE_VOL_PATH + '/' + CA_CERT_FILE,
        'EdgeModuleHubServerCAChainCertificateFile': LINUX_EDGE_HUB_VOL_PATH + '/' + CHAIN_CERT_FILE,
        'EdgeModuleHubServerCertificateFile': LINUX_EDGE_HUB_VOL_PATH + '/' + HUB_CERT_FILE
    }
    ENV_WINDOWS_DICT = {
        'EdgeHubVolumePath': WINDOWS_EDGE_HUB_VOL_PATH,
        'EdgeModuleVolumePath': WINDOWS_EDGE_MODULE_VOL_PATH,
        'EdgeModuleCACertificateFile': WINDOWS_EDGE_MODULE_VOL_PATH + '/' + CA_CERT_FILE,
        'EdgeModuleHubServerCAChainCertificateFile': WINDOWS_EDGE_HUB_VOL_PATH + '/' + CHAIN_CERT_FILE,
        'EdgeModuleHubServerCertificateFile': WINDOWS_EDGE_HUB_VOL_PATH + '/' + HUB_CERT_FILE
    }

    DOCKER_LOG_OPTS_DICT = {
        'testOpt0': '0',
        'testOpt1': '1'
    }
    DOCKER_LOG_CONFIG_DICT = {
        'type': 'testDriver',
        'config': DOCKER_LOG_OPTS_DICT
    }

    DOCKER_PIPE = '\\\\.\\pipe\\docker_engine'
    WINDOWS_MOUNTS_LIST = [
        docker.types.Mount(target=DOCKER_PIPE, source=DOCKER_PIPE, type='npipe')
    ]
    LINUX_MOUNTS_LIST = []
    HOST_MOUNTS_LIST_DICT = {
        'linux': {
            'tcp_port': LINUX_MOUNTS_LIST,
            'unix_port': LINUX_MOUNTS_LIST
        },
        'windows': {
            'tcp_port': [],
            'npipe': WINDOWS_MOUNTS_LIST
        }
    }
    WINDOWS_VOLUME_DICT = {
        EDGE_HUB_VOL_NAME: {'bind': WINDOWS_EDGE_HUB_VOL_PATH, 'mode': 'rw'},
        EDGE_MODULE_VOL_NAME: {'bind': WINDOWS_EDGE_MODULE_VOL_PATH, 'mode': 'rw'},
    }
    LINUX_TCP_ENDPOINT_VOLUME_DICT = {
        EDGE_HUB_VOL_NAME: {'bind': LINUX_EDGE_HUB_VOL_PATH, 'mode': 'rw'},
        EDGE_MODULE_VOL_NAME: {'bind': LINUX_EDGE_MODULE_VOL_PATH, 'mode': 'rw'},
    }
    LINUX_UNIX_ENDPOINT_VOLUME_DICT = {
        EDGE_HUB_VOL_NAME: {'bind': LINUX_EDGE_HUB_VOL_PATH, 'mode': 'rw'},
        EDGE_MODULE_VOL_NAME: {'bind': LINUX_EDGE_MODULE_VOL_PATH, 'mode': 'rw'},
        UNIX_DOCKER_ENDPOINT: {'bind': UNIX_DOCKER_ENDPOINT, 'mode': 'rw'},
    }
    HOST_VOLUME_DICT = {
        'linux': {
            'tcp_port': LINUX_TCP_ENDPOINT_VOLUME_DICT,
            'unix_port': LINUX_UNIX_ENDPOINT_VOLUME_DICT
        },
        'windows': {
            'tcp_port': WINDOWS_VOLUME_DICT,
            'npipe': WINDOWS_VOLUME_DICT},
    }
    PORT_NONE_DICT = {}
    PORT_1234_DICT = {
        '1234/tcp': 1234
    }
    RESTART_POLICY_DICT = {'Name': 'unless-stopped'}

    def _get_env_dict(self, engine_os):
        result = self.ENV_COMMON_DICT.copy()
        if engine_os == 'linux':
            merge_dict = self.ENV_LINUX_DICT
        elif engine_os == 'windows':
            merge_dict = self.ENV_WINDOWS_DICT
        else:
            merge_dict = {}
        result.update(merge_dict)
        return result

    def _get_edge_hub_vol_path(self, engine_os):
        if engine_os == 'windows':
            return self.WINDOWS_EDGE_HUB_VOL_PATH
        elif engine_os == 'linux':
            return self.LINUX_EDGE_HUB_VOL_PATH
        return None

    def _get_edge_module_vol_path(self, engine_os):
        if engine_os == 'windows':
            return self.WINDOWS_EDGE_MODULE_VOL_PATH
        elif engine_os == 'linux':
            return self.LINUX_EDGE_MODULE_VOL_PATH
        return None

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_unavailable_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = False
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.start()

    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_prerequisites_docker_engine_invalid(self, mock_client):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        # arrange
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = 'blah'
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act, assert
        with self.assertRaises(EdgeDeploymentError):
            command.start()

    @mock.patch('edgectl.host.EdgeHostPlatform.get_supported_docker_engines')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_restarting_image_valid(self, mock_client, mock_supported_engines):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING.
            No local image id or image pull or container create and start
            should be called.
        """
        # arrange
        engine_os = 'linux'
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = engine_os
        mock_supported_engines.return_value = [engine_os]
        mock_client.status.return_value = 'restarting'
        mock_client.get_local_image_sha_id.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.start()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.get_local_image_sha_id.assert_not_called()
        mock_client.pull.assert_not_called()
        mock_client.create.assert_not_called()
        mock_client.start.assert_not_called()

    @mock.patch('edgectl.host.EdgeHostPlatform.get_supported_docker_engines')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_running_image_valid(self, mock_client, mock_supported_engines):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING.
            No local image id or image pull or container create and start
            should be called.
        """
        # arrange
        engine_os = 'linux'
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = engine_os
        mock_supported_engines.return_value = [engine_os]
        mock_client.status.return_value = 'running'
        mock_client.get_local_image_sha_id.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.start()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.get_local_image_sha_id.assert_not_called()
        mock_client.pull.assert_not_called()
        mock_client.create.assert_not_called()
        mock_client.start.assert_not_called()

    @mock.patch('edgectl.host.EdgeHostPlatform.get_supported_docker_engines')
    @mock.patch('edgectl.host.EdgeDockerClient', autospec=True)
    def test_edge_status_stopped_image_valid(self, mock_client, mock_supported_engines):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED.
            No local image id or image pull or container create and start
            should be called.
        """
        # arrange
        engine_os = 'linux'
        mock_client.check_availability.return_value = True
        mock_client.get_os_type.return_value = engine_os
        mock_supported_engines.return_value = [engine_os]
        mock_client.status.return_value = 'stopped'
        mock_client.get_local_image_sha_id.return_value = None
        config = _create_edge_configuration_valid()
        command = EdgeDeploymentCommandDocker.create_using_client(config, mock_client)

        # act
        command.start()

        # assert
        mock_client.status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_client.check_availability.assert_called()
        mock_client.get_os_type.assert_called()
        mock_client.get_local_image_sha_id.assert_not_called()
        mock_client.pull.assert_not_called()
        mock_client.create.assert_not_called()
        mock_client.start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    def test_edge_status_uninstalled_no_local_image_valid(self):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE.
            Image id and image pull and container create and start
            all should be called since Edge has not yet created.
        """
        edge_config = _create_edge_configuration_valid()
        self._test_create_options_helper(edge_config, 'linux', None)

    def test_edge_status_uninstalled_local_image_available_valid(self):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE.
            Image id check returns that there is an existing image downloaded
            and so image pull should not be called and container create and start
            should be called.
        """
        edge_config = _create_edge_configuration_valid()
        self._test_create_options_helper(edge_config, 'linux', '1234')

    def test_create_options_docker_engine_linux_docker_tcp_port_valid(self):
        """ Tests agent create options on a linux docker engine when docker URI is http based """
        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        self._test_create_options_helper(edge_config, 'linux')

    def test_create_options_docker_engine_linux_docker_unix_port_valid(self):
        """ Tests agent create options on a linux docker engine when docker URI is unix socket based """
        docker_uri = 'unix:///var/run/docker.sock'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        self._test_create_options_helper(edge_config, 'linux')

    def test_create_options_docker_engine_windows_docker_tcp_port_valid(self):
        """ Tests agent create options on a windows docker engine when docker URI is http based """
        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        self._test_create_options_helper(edge_config, 'windows')

    def test_create_options_docker_engine_windows_docker_npipe_valid(self):
        """ Tests agent create options on a windows docker engine when docker URI is npipe based """
        docker_uri = 'npipe://./pipe/docker_engine'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        self._test_create_options_helper(edge_config, 'windows')

    def test_create_upstream_protocol_valid(self):
        """ Tests setting a valid upstream protocol """
        edge_config = _create_edge_configuration_valid()
        edge_config.upstream_protocol = EdgeUpstreamProtocol.AMQPWS
        self._test_create_options_helper(edge_config, 'linux')

    def test_create_upstream_protocol_none(self):
        """ Tests setting none as upstream protocol """
        edge_config = _create_edge_configuration_valid()
        edge_config.upstream_protocol = EdgeUpstreamProtocol.NONE
        self._test_create_options_helper(edge_config, 'linux')

    def _test_create_options_helper(self, edge_config, engine_os, local_sha_id=None):
        # arrange
        with patch('edgectl.host.EdgeDockerClient', MagicMock(autospec=True)) as mock_client:
            with patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file',
                       MagicMock(return_value=self.MOCK_CA_CERT_DICT)):
                with patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file',
                           MagicMock(return_value=self.MOCK_CHAIN_CERT_DICT)):
                    with patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file',
                               MagicMock(return_value=self.MOCK_HUB_CERT_DICT)):
                        with patch('edgectl.host.EdgeHostPlatform.get_supported_docker_engines',
                                   MagicMock(return_value=[engine_os])):
                            mock_client.check_availability.return_value = True
                            mock_client.get_os_type.return_value = engine_os
                            mock_client.status.return_value = None
                            mock_client.get_local_image_sha_id.return_value = local_sha_id

                            env_dict = self._get_env_dict(engine_os)
                            docker_uri = edge_config.deployment_config.uri
                            env_dict['DockerUri'] = docker_uri
                            if docker_uri.startswith('unix'):
                                port_mapping_dict = self.PORT_NONE_DICT
                                volume_key = 'unix_port'
                            elif docker_uri.startswith('npipe'):
                                port_mapping_dict = self.PORT_NONE_DICT
                                volume_key = 'npipe'
                            else:
                                port_mapping_dict = self.PORT_1234_DICT
                                volume_key = 'tcp_port'

                            upstream_protocol = edge_config.upstream_protocol
                            if upstream_protocol is not None and upstream_protocol != EdgeUpstreamProtocol.NONE:
                                env_dict['UpstreamProtocol'] = edge_config.upstream_protocol.value
                            else:
                                env_dict['UpstreamProtocol'] = ''

                            edgehub_path = self._get_edge_hub_vol_path(engine_os)
                            edgemodule_path = self._get_edge_module_vol_path(engine_os)

                            command = EdgeDeploymentCommandDocker.create_using_client(edge_config, mock_client)

                            # act
                            command.start()

                            # assert
                            mock_client.create_network.assert_called_with(self.NETWORK_NAME)
                            mock_client.create_volume.assert_any_call(EDGE_HUB_VOL_NAME)
                            mock_client.create_volume.assert_any_call(EDGE_MODULE_VOL_NAME)
                            mock_client.copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                                            self.CA_CERT_FILE,
                                                                            edgemodule_path,
                                                                            '/test/' + self.CA_CERT_FILE)
                            mock_client.copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                                            self.CHAIN_CERT_FILE,
                                                                            edgehub_path,
                                                                            '/test/' + self.CHAIN_CERT_FILE)
                            mock_client.copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                                            self.HUB_CERT_FILE,
                                                                            edgehub_path,
                                                                            '/test/' + self.HUB_CERT_FILE)

                            if local_sha_id is None:
                                mock_client.pull.assert_called_with('testServer0/testImage:testTag',
                                                                    'testUsername0', 'testPassword0')
                            else:
                                mock_client.pull.assert_not_called()

                            mock_client.create.assert_called_with('testServer0/testImage:testTag',
                                                                  EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                                  True,
                                                                  env_dict,
                                                                  self.NETWORK_NAME,
                                                                  port_mapping_dict,
                                                                  self.HOST_VOLUME_DICT[engine_os][volume_key],
                                                                  self.DOCKER_LOG_CONFIG_DICT,
                                                                  self.HOST_MOUNTS_LIST_DICT[engine_os][volume_key],
                                                                  self.RESTART_POLICY_DICT)
                            mock_client.start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

class TestEdgeCommandFactory(unittest.TestCase):
    """Unit tests for EdgeCommandFactory APIs"""
    def test_supported_commands(self):
        """ Tests API get_supported_commands list """
        # arrange
        supported_commands = [
            'setup', 'start', 'stop', 'restart', 'status', 'login', 'update', 'uninstall'
        ]
        result = EdgeCommandFactory.get_supported_commands()
        check_set = set(supported_commands).difference(result)

        # act, assert
        self.assertEqual(0, len(check_set))

    def test_unsupported_command_invalid(self):
        """ Tests whether an unsupported command raises exception EdgeValueError """
        # arrange
        edge_config = EdgeHostConfig()
        # act, assert
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('blah', edge_config)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('', edge_config)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command(None, edge_config)

    def test_unsupported_edge_config_object_invalid(self):
        """ Tests whether an unsupported config object raises exception EdgeValueError """
        # act, assert
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('start', None)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('', 'blah')
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command(None, 2)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command(None, {})

    @mock.patch('edgectl.config.EdgeHostConfig.deployment_type')
    def test_unsupported_deployment_invalid(self, mock_deployment_type):
        """ Tests whether an unsupported command raises exception EdgeValueError """
        # arrange
        edge_config = EdgeHostConfig()
        mock_deployment_type.return_value = 'blah'

        # act, assert
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('start', edge_config)

def _create_edge_configuration_valid():
    edge_config = EdgeHostConfig()
    edge_config.connection_string = 'HostName=a;DeviceId=b;SharedAccessKey=c'
    edge_config.hostname = 'testHostName'
    edge_config.log_level = 'debug'
    deployment_config = EdgeDeploymentConfigDocker()
    deployment_config.add_registry('testServer0', 'testUsername0', 'testPassword0')
    deployment_config.add_registry('testServer1', 'testUsername1', 'testPassword1')
    deployment_config.edge_image = 'testServer0/testImage:testTag'
    deployment_config.uri = 'https://myhost:1234'
    deployment_config.logging_driver = 'testDriver'
    deployment_config.add_logging_option('testOpt0', '0')
    deployment_config.add_logging_option('testOpt1', '1')
    edge_config.deployment_config = deployment_config
    return edge_config

def _create_deployment_command(command, config=None):
    if config is None:
        config = _create_edge_configuration_valid()
    command = EdgeCommandFactory.create_command(command, config)
    return command

if __name__ == '__main__':
    test_classes = [
        TestEdgeDeploymentDockerLogin,
        TestEdgeDeploymentDockerStart,
        TestEdgeDeploymentDockerRestart,
        TestEdgeDeploymentDockerStop,
        TestEdgeDeploymentDockerSetup,
        TestEdgeDeploymentDockerUpdate,
        TestEdgeDeploymentDockerUninstall,
        TestEdgeDeploymentDockerStatus,
        TestEdgeCommandFactory
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
