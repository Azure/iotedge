""" Module defines base and factory classes needed to instantiate and execute deployment commands"""

# pylint: disable=R0903
# disables too few public methods
class EdgeDeploymentCommand(object):
    """
    Base class for all Edge deployment commands
    """
    EDGE_RUNTIME_STATUS_UNAVAILABLE = 'UNAVAILABLE'
    EDGE_RUNTIME_STATUS_RUNNING = 'RUNNING'
    EDGE_RUNTIME_STATUS_RESTARTING = 'RESTARTING'
    EDGE_RUNTIME_STATUS_STOPPED = 'STOPPED'

    def __init__(self, config_obj):
        self._config_obj = config_obj

    def start(self):
        """ API for starting a deployment already installed on the host """
        pass

    def stop(self):
        """ API for stopping a running deployment executing on the host """
        pass

    def restart(self):
        """ API for restarting a running or stopped deployment """
        pass

    def uninstall(self):
        """ API for uninstalling a deployment on the host """
        pass

    def setup(self):
        """ API for configuring and installing the Edge runtime on a host """
        pass

    def status(self):
        """ API for determining the status of the Edge runtime on a host """
        pass

    def update(self):
        """ API for updating the Edge runtime image on a host """
        pass

    def login(self):
        """ API to add registry credentials in order to download and install modules """
        pass

class EdgeCommand(object):
    """ Class implements the base class of the command pattern"""
    def __init__(self, obj):
        self._obj = obj

    def execute(self):
        """ API to execute a deployment command """
        pass

class EdgeSetupCommand(EdgeCommand):
    """ Executor class for Edge runtime setup """
    def execute(self):
        return self._obj.setup()

class EdgeStartCommand(EdgeCommand):
    """ Executor class for Edge runtime start """
    def execute(self):
        return self._obj.start()

class EdgeStopCommand(EdgeCommand):
    """ Executor class for Edge runtime and deployment stop """
    def execute(self):
        return self._obj.stop()

class EdgeRestartCommand(EdgeCommand):
    """ Executor class to restart the Edge runtime  """
    def execute(self):
        return self._obj.restart()

class EdgeStatusCommand(EdgeCommand):
    """ Executor class for retrieving the Edge runtime status  """
    def execute(self):
        return self._obj.status()

class EdgeUninstallCommand(EdgeCommand):
    """ Executor class for Edge runtime and deployment uninstall """
    def execute(self):
        return self._obj.uninstall()

class EdgeUpdateCommand(EdgeCommand):
    """ Executor class for Edge runtime update """
    def execute(self):
        return self._obj.update()

class EdgeLoginCommand(EdgeCommand):
    """ Executor class for Edge runtime and deployment registry credentials """
    def execute(self):
        return self._obj.login()
