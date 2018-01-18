""" Implementation of the factory class to instantiate Edge deployment commands """
import logging as log
from edgectl.config import EdgeConstants as EC
from edgectl.deployment.commandbase import EdgeLoginCommand
from edgectl.deployment.commandbase import EdgeRestartCommand
from edgectl.deployment.commandbase import EdgeSetupCommand
from edgectl.deployment.commandbase import EdgeStartCommand
from edgectl.deployment.commandbase import EdgeStatusCommand
from edgectl.deployment.commandbase import EdgeStopCommand
from edgectl.deployment.commandbase import EdgeUninstallCommand
from edgectl.deployment.commandbase import EdgeUpdateCommand
from edgectl.deployment.deploymentdocker import EdgeDeploymentCommandDocker
from edgectl.host import EdgeHostPlatform
import edgectl.errors

class EdgeCommandFactory(object):
    """
    Factory class that implements the requisite APIs to create Edge commands
    for subsequent execution
    """
    _supported_commands = {'setup'     : EdgeSetupCommand,
                           'start'     : EdgeStartCommand,
                           'restart'   : EdgeRestartCommand,
                           'stop'      : EdgeStopCommand,
                           'status'    : EdgeStatusCommand,
                           'uninstall' : EdgeUninstallCommand,
                           'update'    : EdgeUpdateCommand,
                           'login'     : EdgeLoginCommand}

    @staticmethod
    def get_supported_commands():
        """ Returns a list of all the supported Edge commands """
        return list(EdgeCommandFactory._supported_commands.keys())

    @staticmethod
    def create_command(command, edge_config):
        """ API to create an Edge command
        Args:
            command (str): Edge command name
            edge_config (obj): A valid instance of the edgectl.config.EdgeHostConfig

        Returns:
            Instance of edgectl.deployment.EdgeCommand

        Raises:
            edgectl.errors.EdgeValueError if the command or deployment type is unsupported
        """
        result = None
        if command is None:
            msg = 'Command cannot be None'
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        if edge_config is None or isinstance(edge_config, edgectl.config.EdgeHostConfig) is False:
            msg = 'Invalid Edge config object'
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)

        deployment = edge_config.deployment_type
        if command not in list(EdgeCommandFactory._supported_commands.keys()):
            msg = 'Unsupported command: ' + command
            log.error(msg)
            raise edgectl.errors.EdgeValueError(msg)
        else:
            if EdgeHostPlatform.is_deployment_supported(deployment):
                if deployment == EC.DEPLOYMENT_DOCKER:
                    deployment_cmd_obj = EdgeDeploymentCommandDocker(edge_config)
                    result = EdgeCommandFactory._supported_commands[command](deployment_cmd_obj)
            else:
                msg = 'IoT Edge deployment not supported: {0}'.format(deployment)
                log.critical(msg)
                raise edgectl.errors.EdgeValueError(msg)

        return result
