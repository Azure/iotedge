"""Implementation of tests for module `edgectl.deployment.deploymentdocker.py`."""
from __future__ import print_function
import unittest
from mock import mock
import docker
from edgectl.deployment import EdgeCommandFactory
from edgectl.deployment import EdgeDeploymentCommand
from edgectl.config import EdgeHostConfig
from edgectl.config import EdgeDeploymentConfigDocker
from edgectl.errors import EdgeDeploymentError
from edgectl.errors import EdgeValueError

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
# pylint: disable=R0914
# disables too many local variables
# pylint: disable=C0302
# disables too many lines in module
class TestEdgeDeploymentDockerStatus(unittest.TestCase):
    """Unit tests for class EdgeDeploymentCommandDocker.status"""

    COMMAND_NAME = 'status'

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os, mock_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        result = command.execute()
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE, result)

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os, mock_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        result = command.execute()
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED, result)

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os, mock_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        result = command.execute()
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING, result)

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os, mock_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        result = command.execute()
        self.assertEqual(EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING, result)

class TestEdgeDeploymentDockerLogin(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.login"""

    COMMAND_NAME = 'login'
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_deployment_start.assert_called()

class TestEdgeDeploymentDockerUpdate(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.update"""

    COMMAND_NAME = 'update'
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os, mock_client_status):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_deployment_start.assert_called()

class TestEdgeDeploymentDockerStop(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.stop"""

    COMMAND_NAME = 'stop'

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                           mock_stop, mock_stop_by_label):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                       mock_stop, mock_stop_by_label):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)

    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                          mock_stop, mock_stop_by_label):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                       mock_stop, mock_stop_by_label):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)

class TestEdgeDeploymentDockerRestart(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.restart"""

    COMMAND_NAME = 'restart'

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.deployment.EdgeDeploymentCommandDocker.start')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os,
                                           mock_client_status, mock_deployment_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_deployment_start.assert_called()

    @mock.patch('edgectl.host.EdgeDockerClient.restart')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                       mock_stop, mock_stop_by_label, mock_client_restart):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client_restart.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.restart')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os, mock_client_status,
                                          mock_stop, mock_stop_by_label, mock_client_restart):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_not_called()
        mock_client_restart.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient.restart')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop,
                                       mock_stop_by_label, mock_client_restart):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_client_restart.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

class TestEdgeDeploymentDockerUninstall(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.uninstall"""

    COMMAND_NAME = 'uninstall'

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os,
                                           mock_client_status, mock_stop, mock_stop_by_label,
                                           mock_remove_by_label, mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_stop_by_label, mock_remove_by_label,
                                       mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os,
                                          mock_client_status, mock_stop, mock_remove,
                                          mock_stop_by_label, mock_remove_by_label,
                                          mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_stop_by_label, mock_remove_by_label,
                                       mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

class TestEdgeDeploymentDockerSetup(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.setup"""

    COMMAND_NAME = 'setup'

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_valid(self, mock_check_avail, mock_get_os,
                                           mock_client_status, mock_stop, mock_stop_by_label,
                                           mock_remove_by_label, mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = None
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_not_called()
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_stopped_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_stop_by_label, mock_remove_by_label,
                                       mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_STOPPED
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'stopped'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self, mock_check_avail, mock_get_os,
                                          mock_client_status, mock_stop, mock_remove,
                                          mock_stop_by_label, mock_remove_by_label,
                                          mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'restarting'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)

    @mock.patch('edgectl.host.EdgeDockerClient.remove_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.remove_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.stop_by_label')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.stop')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self, mock_check_avail, mock_get_os,
                                       mock_client_status, mock_stop, mock_remove,
                                       mock_stop_by_label, mock_remove_by_label,
                                       mock_remove_volume):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
        """
        mock_check_avail.return_value = True
        mock_get_os.return_value = 'linux'
        mock_client_status.return_value = 'running'
        command = _create_deployment_command(self.COMMAND_NAME)
        command.execute()
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_stop.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_stop_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_by_label.assert_called_with(EDGE_MODULES_LABEL)
        mock_remove_volume.assert_any_call(EDGE_MODULE_VOL_NAME, True)
        mock_remove_volume.assert_any_call(EDGE_HUB_VOL_NAME, True)


class TestEdgeDeploymentDockerStart(unittest.TestCase):
    """Unit tests for API EdgeDeploymentCommandDocker.start"""

    COMMAND_NAME = 'start'
    NETWORK_NAME = 'azure-iot-edge'
    EDGE_HUB_VOL_NAME = 'edgehub'
    EDGE_HUB_VOL_PATH = '/mnt/edgehub'
    EDGE_MODULE_VOL_NAME = 'edgemodule'
    EDGE_MODULE_VOL_PATH = '/mnt/edgemodule'
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
    }
    ENV_LINUX_DICT = {
        'EdgeHubVolumeName': EDGE_HUB_VOL_NAME,
        'EdgeHubVolumePath': EDGE_HUB_VOL_PATH,
        'EdgeModuleVolumeName': EDGE_MODULE_VOL_NAME,
        'EdgeModuleVolumePath': EDGE_MODULE_VOL_PATH,
        'EdgeModuleCACertificateFile': EDGE_MODULE_VOL_PATH + '/' + CA_CERT_FILE,
        'EdgeModuleHubServerCAChainCertificateFile': EDGE_HUB_VOL_PATH + '/' + CHAIN_CERT_FILE,
        'EdgeModuleHubServerCertificateFile': EDGE_HUB_VOL_PATH + '/' + HUB_CERT_FILE,
    }
    ENV_WINDOWS_DICT = {}

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
        'linux': LINUX_MOUNTS_LIST,
        'windows': WINDOWS_MOUNTS_LIST
    }
    WINDOWS_VOLUME_DICT = {}
    LINUX_TCP_ENDPOINT_VOLUME_DICT = {
        EDGE_HUB_VOL_NAME: {'bind': EDGE_HUB_VOL_PATH, 'mode': 'rw'},
        EDGE_MODULE_VOL_NAME: {'bind': EDGE_MODULE_VOL_PATH, 'mode': 'rw'},
    }
    LINUX_UNIX_ENDPOINT_VOLUME_DICT = {
        EDGE_HUB_VOL_NAME: {'bind': EDGE_HUB_VOL_PATH, 'mode': 'rw'},
        EDGE_MODULE_VOL_NAME: {'bind': EDGE_MODULE_VOL_PATH, 'mode': 'rw'},
        UNIX_DOCKER_ENDPOINT: {'bind': UNIX_DOCKER_ENDPOINT, 'mode': 'rw'},
    }
    HOST_VOLUME_DICT = {
        'linux': {'tcp_port': LINUX_TCP_ENDPOINT_VOLUME_DICT,
                  'unix_port': LINUX_UNIX_ENDPOINT_VOLUME_DICT},
        'windows': {'tcp_port': WINDOWS_VOLUME_DICT, 'unix_port': WINDOWS_VOLUME_DICT},
    }
    PORT_NONE_DICT = {}
    PORT_1234_DICT = {
        '1234/tcp': 1234
    }
    RESTART_POLICY_DICT = {'Name': 'always'}

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

    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_unavailable_invalid(self, mock_check_avail):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_prerequisites_docker_engine_invalid(self, mock_check_avail, mock_get_os):
        """ Test fails if prerequisites are not met and EdgeDeploymentError is raised """
        mock_check_avail.return_value = False
        mock_get_os.return_value = 'blah'
        command = _create_deployment_command(self.COMMAND_NAME)
        with self.assertRaises(EdgeDeploymentError):
            command.execute()

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_restarting_valid(self,
                                          mock_check_avail,
                                          mock_get_os,
                                          mock_client_status,
                                          mock_pull,
                                          mock_get_cont_image,
                                          mock_remove,
                                          mock_create_network,
                                          mock_create_volume,
                                          mock_get_ca_cert,
                                          mock_get_chain_cert,
                                          mock_get_cert_pfx,
                                          mock_copy_file_to_volume,
                                          mock_create,
                                          mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RESTARTING
            for docker engine linux with newer edge agent image
            with docker URI using tcp endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = 'restarting'
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_not_called()
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_not_called()
        mock_create_network.assert_not_called()
        mock_create_volume.assert_not_called()
        mock_create_volume.assert_not_called()
        mock_create.assert_not_called()
        mock_copy_file_to_volume.assert_not_called()
        mock_client_start.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_running_valid(self,
                                       mock_check_avail,
                                       mock_get_os,
                                       mock_client_status,
                                       mock_pull,
                                       mock_get_cont_image,
                                       mock_remove,
                                       mock_create_network,
                                       mock_create_volume,
                                       mock_get_ca_cert,
                                       mock_get_chain_cert,
                                       mock_get_cert_pfx,
                                       mock_copy_file_to_volume,
                                       mock_create,
                                       mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_RUNNING
            for docker engine linux with newer edge agent image
            with docker URI using tcp endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = 'running'
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_not_called()
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_not_called()
        mock_create_network.assert_not_called()
        mock_create_volume.assert_not_called()
        mock_create_volume.assert_not_called()
        mock_create.assert_not_called()
        mock_copy_file_to_volume.assert_not_called()
        mock_client_start.assert_not_called()

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_engine_linux_new_image_tcp_port_valid(self,
                                                                           mock_check_avail,
                                                                           mock_get_os,
                                                                           mock_client_status,
                                                                           mock_pull,
                                                                           mock_get_cont_image,
                                                                           mock_remove,
                                                                           mock_create_network,
                                                                           mock_create_volume,
                                                                           mock_get_ca_cert,
                                                                           mock_get_chain_cert,
                                                                           mock_get_cert_pfx,
                                                                           mock_copy_file_to_volume,
                                                                           mock_create,
                                                                           mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine linux with newer edge agent image
            with docker URI using tcp endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with('testServer0/testImage:testTag',
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_any_call(EDGE_HUB_VOL_NAME)
        mock_create_volume.assert_any_call(EDGE_MODULE_VOL_NAME)
        mock_create.assert_called_with('testServer0/testImage:testTag',
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_1234_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['tcp_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       self.HOST_MOUNTS_LIST_DICT[engine_os],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CA_CERT_FILE,
                                                 self.EDGE_MODULE_VOL_PATH,
                                                 '/test/' + self.CA_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CHAIN_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.CHAIN_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.HUB_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.HUB_CERT_FILE)
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_engine_winOS_new_image_tcp_port_valid(self,
                                                                           mock_check_avail,
                                                                           mock_get_os,
                                                                           mock_client_status,
                                                                           mock_pull,
                                                                           mock_get_cont_image,
                                                                           mock_remove,
                                                                           mock_create_network,
                                                                           mock_create_volume,
                                                                           mock_get_ca_cert,
                                                                           mock_get_chain_cert,
                                                                           mock_get_cert_pfx,
                                                                           mock_copy_file_to_volume,
                                                                           mock_create,
                                                                           mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine windows with newer edge agent image
            with docker URI using tcp endpoint
        """
        # test setup
        engine_os = 'windows'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'https://myhost:1234'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with('testServer0/testImage:testTag',
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_not_called()
        mock_create.assert_called_with('testServer0/testImage:testTag',
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_1234_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['tcp_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       [],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_not_called()
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_engine_winOS_new_image_npipe_ep_valid(self,
                                                                           mock_check_avail,
                                                                           mock_get_os,
                                                                           mock_client_status,
                                                                           mock_pull,
                                                                           mock_get_cont_image,
                                                                           mock_remove,
                                                                           mock_create_network,
                                                                           mock_create_volume,
                                                                           mock_get_ca_cert,
                                                                           mock_get_chain_cert,
                                                                           mock_get_cert_pfx,
                                                                           mock_copy_file_to_volume,
                                                                           mock_create,
                                                                           mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine windows with newer edge agent image
            with docker URI using tcp endpoint
        """
        # test setup
        engine_os = 'windows'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'npipe://./pipe/docker_engine'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with('testServer0/testImage:testTag',
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_not_called()
        mock_create.assert_called_with('testServer0/testImage:testTag',
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_NONE_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['tcp_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       self.HOST_MOUNTS_LIST_DICT[engine_os],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_not_called()
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_new_pulled_image_valid(self,
                                                            mock_check_avail,
                                                            mock_get_os,
                                                            mock_client_status,
                                                            mock_pull,
                                                            mock_get_cont_image,
                                                            mock_remove,
                                                            mock_create_network,
                                                            mock_create_volume,
                                                            mock_get_ca_cert,
                                                            mock_get_chain_cert,
                                                            mock_get_cert_pfx,
                                                            mock_copy_file_to_volume,
                                                            mock_create,
                                                            mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine linux with newer edge agent image
            with docker URI using unix endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = True
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT

        docker_uri = 'unix:///var/run/docker.sock'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with('testServer0/testImage:testTag',
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_not_called()
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_any_call(EDGE_HUB_VOL_NAME)
        mock_create_volume.assert_any_call(EDGE_MODULE_VOL_NAME)
        mock_create.assert_called_with('testServer0/testImage:testTag',
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_NONE_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['unix_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       self.HOST_MOUNTS_LIST_DICT[engine_os],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CA_CERT_FILE,
                                                 self.EDGE_MODULE_VOL_PATH,
                                                 '/test/' + self.CA_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CHAIN_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.CHAIN_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.HUB_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.HUB_CERT_FILE)
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_new_pulled_false_diff_image_exists_locally_valid(self,
                                                                                      mock_check_avail,
                                                                                      mock_get_os,
                                                                                      mock_client_status,
                                                                                      mock_pull,
                                                                                      mock_get_cont_image,
                                                                                      mock_remove,
                                                                                      mock_create_network,
                                                                                      mock_create_volume,
                                                                                      mock_get_ca_cert,
                                                                                      mock_get_chain_cert,
                                                                                      mock_get_cert_pfx,
                                                                                      mock_copy_file_to_volume,
                                                                                      mock_create,
                                                                                      mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine linux with an existing edge agent image
            with docker URI using unix endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = False
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT
        mock_get_cont_image.return_value = 'testServer0/testImage:testTag'

        docker_uri = 'unix:///var/run/docker.sock'
        diff_image_name = 'testServer0/testImage:testTagDiff'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.edge_image = diff_image_name
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with(diff_image_name,
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_any_call(EDGE_HUB_VOL_NAME)
        mock_create_volume.assert_any_call(EDGE_MODULE_VOL_NAME)
        mock_create.assert_called_with(diff_image_name,
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_NONE_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['unix_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       self.HOST_MOUNTS_LIST_DICT[engine_os],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CA_CERT_FILE,
                                                 self.EDGE_MODULE_VOL_PATH,
                                                 '/test/' + self.CA_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CHAIN_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.CHAIN_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.HUB_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.HUB_CERT_FILE)
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

    @mock.patch('edgectl.host.EdgeDockerClient.start')
    @mock.patch('edgectl.host.EdgeDockerClient.create')
    @mock.patch('edgectl.host.EdgeDockerClient.copy_file_to_volume')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_hub_cert_pfx_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_ca_chain_cert_file')
    @mock.patch('edgectl.host.EdgeHostPlatform.get_root_ca_cert_file')
    @mock.patch('edgectl.host.EdgeDockerClient.create_volume')
    @mock.patch('edgectl.host.EdgeDockerClient.create_network')
    @mock.patch('edgectl.host.EdgeDockerClient.remove')
    @mock.patch('edgectl.host.EdgeDockerClient.get_container_image')
    @mock.patch('edgectl.host.EdgeDockerClient.pull')
    @mock.patch('edgectl.host.EdgeDockerClient.status')
    @mock.patch('edgectl.host.EdgeDockerClient.get_os_type')
    @mock.patch('edgectl.host.EdgeDockerClient.check_availability')
    def test_edge_status_unavailable_engine_linux_same_image_no_contr__exists_unix_valid(self,
                                                                                         mock_check_avail,
                                                                                         mock_get_os,
                                                                                         mock_client_status,
                                                                                         mock_pull,
                                                                                         mock_get_cont_image,
                                                                                         mock_remove,
                                                                                         mock_create_network,
                                                                                         mock_create_volume,
                                                                                         mock_get_ca_cert,
                                                                                         mock_get_chain_cert,
                                                                                         mock_get_cert_pfx,
                                                                                         mock_copy_file_to_volume,
                                                                                         mock_create,
                                                                                         mock_client_start):
        """
            Tests call stack when Edge runtime status is
            EdgeDeploymentCommand.EDGE_RUNTIME_STATUS_UNAVAILABLE
            for docker engine linux with an existing edge agent image
            with docker URI using unix endpoint
        """
        # test setup
        engine_os = 'linux'
        mock_check_avail.return_value = True
        mock_get_os.return_value = engine_os
        mock_client_status.return_value = None
        mock_pull.return_value = False
        mock_get_ca_cert.return_value = self.MOCK_CA_CERT_DICT
        mock_get_chain_cert.return_value = self.MOCK_CHAIN_CERT_DICT
        mock_get_cert_pfx.return_value = self.MOCK_HUB_CERT_DICT
        mock_get_cont_image.return_value = None

        docker_uri = 'unix:///var/run/docker.sock'
        edge_config = _create_edge_configuration_valid()
        edge_config.deployment_config.uri = docker_uri
        env_dict = self._get_env_dict(engine_os)
        env_dict['DockerUri'] = docker_uri
        command = _create_deployment_command(self.COMMAND_NAME, edge_config)

        # execute test
        command.execute()

        # validate results
        mock_client_status.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_check_avail.assert_called()
        mock_get_os.assert_called()
        mock_pull.assert_called_with('testServer0/testImage:testTag',
                                     'testUsername0', 'testPassword0')
        mock_get_cont_image.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)
        mock_remove.assert_not_called()
        mock_create_network.assert_called_with(self.NETWORK_NAME)
        mock_create_volume.assert_any_call(EDGE_HUB_VOL_NAME)
        mock_create_volume.assert_any_call(EDGE_MODULE_VOL_NAME)
        mock_create.assert_called_with('testServer0/testImage:testTag',
                                       EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                       True,
                                       env_dict,
                                       self.NETWORK_NAME,
                                       self.PORT_NONE_DICT,
                                       self.HOST_VOLUME_DICT[engine_os]['unix_port'],
                                       self.DOCKER_LOG_CONFIG_DICT,
                                       self.HOST_MOUNTS_LIST_DICT[engine_os],
                                       self.RESTART_POLICY_DICT)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CA_CERT_FILE,
                                                 self.EDGE_MODULE_VOL_PATH,
                                                 '/test/' + self.CA_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.CHAIN_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.CHAIN_CERT_FILE)
        mock_copy_file_to_volume.assert_any_call(EDGE_AGENT_DOCKER_CONTAINER_NAME,
                                                 self.HUB_CERT_FILE,
                                                 self.EDGE_HUB_VOL_PATH,
                                                 '/test/' + self.HUB_CERT_FILE)
        mock_client_start.assert_called_with(EDGE_AGENT_DOCKER_CONTAINER_NAME)

class TestEdgeCommandFactory(unittest.TestCase):
    """Unit tests for EdgeCommandFactory APIs"""
    def test_supported_commands(self):
        """ Tests API get_supported_commands list """
        supported_commands = [
            'setup', 'start', 'stop', 'restart', 'status', 'login', 'update', 'uninstall'
        ]
        result = EdgeCommandFactory.get_supported_commands()
        check_set = set(supported_commands).difference(result)
        self.assertEqual(0, len(check_set))

    def test_unsupported_command_invalid(self):
        """ Tests whether an unsupported command raises exception EdgeValueError """
        edge_config = EdgeHostConfig()
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('blah', edge_config)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command('', edge_config)
        with(self.assertRaises(EdgeValueError)):
            EdgeCommandFactory.create_command(None, edge_config)

    def test_unsupported_edge_config_object_invalid(self):
        """ Tests whether an unsupported config object raises exception EdgeValueError """
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
        edge_config = EdgeHostConfig()
        mock_deployment_type.return_value = 'blah'
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
