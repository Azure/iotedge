import logging as log
import edgectl.commandbase as commandbase
from edgectl.default  import EdgeDefault
from edgectl.deploymentdocker import EdgeDeploymentCommandDocker

class EdgeCommandFactory(object):
    _supported_commands = {'setup'     : commandbase.EdgeSetupCommand,
                           'start'     : commandbase.EdgeStartCommand,
                           'restart'   : commandbase.EdgeRestartCommand,
                           'stop'      : commandbase.EdgeStopCommand,
                           'status'    : commandbase.EdgeStatusCommand,
                           'uninstall' : commandbase.EdgeUninstallCommand,
                           'update'    : commandbase.EdgeUpdateCommand,
                           'login'     : commandbase.EdgeLoginCommand}

    @staticmethod
    def create_command(command, edge_config):
        result = None
        deployment = edge_config.deployment_type
        if deployment not in EdgeDefault.get_supported_deployments():
            msg = 'Unsupported deployment: ' + deployment
            log.error(msg)
            raise ValueError(msg)
        elif command not in list(EdgeCommandFactory._supported_commands.keys()):
            msg = 'Unsupported command: ' + command
            log.error(msg)
            raise ValueError(msg)
        else:
            deployment_cmd_obj = EdgeDeploymentCommandDocker(edge_config)
            result = EdgeCommandFactory._supported_commands[command](deployment_cmd_obj)

        return result
