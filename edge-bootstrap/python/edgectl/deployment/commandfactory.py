import logging as log
from edgectl.config import EdgeConstants as EC
from edgectl.deployment.commandbase import *
from edgectl.deployment.deploymentdocker import EdgeDeploymentCommandDocker
from edgectl.host import EdgeHostPlatform
import edgectl.errors

class EdgeCommandFactory(object):
    _supported_commands = {'setup'     : EdgeSetupCommand,
                           'start'     : EdgeStartCommand,
                           'restart'   : EdgeRestartCommand,
                           'stop'      : EdgeStopCommand,
                           'status'    : EdgeStatusCommand,
                           'uninstall' : EdgeUninstallCommand,
                           'update'    : EdgeUpdateCommand,
                           'login'     : EdgeLoginCommand}

    @staticmethod
    def create_command(command, edge_config):
        result = None
        deployment = edge_config.deployment_type
        if command not in list(EdgeCommandFactory._supported_commands.keys()):
            msg = 'Unsupported command: ' + command
            log.error(msg)
            raise ValueError(msg)
        else:
            if EdgeHostPlatform.is_deployment_supported(deployment):
                if deployment == EC.DEPLOYMENT_DOCKER:
                    deployment_cmd_obj = EdgeDeploymentCommandDocker(edge_config)
                    result = EdgeCommandFactory._supported_commands[command](deployment_cmd_obj)
            else:
                msg = 'IoT Edge deployment not supported: {0}'.format(deployment)
                log.critical(msg)
                raise edgectl.errors.EdgeDeploymentError(msg)

        return result
