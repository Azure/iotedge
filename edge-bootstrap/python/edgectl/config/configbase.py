""" Implements Azure IoT Edge configuration base classes"""

import json

class EdgeConfigBase(object):
    """ Edge configuration base class"""
    def __init__(self):
        self._config_dict = {}

    def to_dict(self):
        """ Return a dict representation of the configuration object"""
        return self._config_dict

    def __str__(self):
        return ''

    def to_json(self):
        """ Return a JSON representation of the config object"""
        return json.dumps(self.to_dict(), indent=2, sort_keys=True)

class EdgeDeploymentConfig(EdgeConfigBase):
    """ Edge deployment configuration base class"""
    def __init__(self, deployment_type_str):
        super(EdgeDeploymentConfig, self).__init__()
        self._deployment_type = deployment_type_str

    @property
    def deployment_type(self):
        """ Return deployment type string"""
        return self._deployment_type
