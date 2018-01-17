"""Implementation of tests for module `edgectl.config.edgeconfig.py`."""
from __future__ import print_function
import shutil
import tempfile
import unittest
from edgectl.config import EdgeConstants
from edgectl.config import EdgeConfigDirInputSource
from edgectl.config import EdgeHostConfig
from edgectl.config import EdgeDeploymentConfig
from edgectl.config import EdgeCertConfig

CONFIG_OBJ_KEY = 'config_object'
CONFIG_ATTR_KEY = 'config_attr'
TEST_CASES_LIST_KEY = 'test_cases'
TEST_IP_KEY = 'input'
TEST_OP_KEY = 'output'

class TestEdgeConfigMethods(unittest.TestCase):
    """Unit test implementations for `edgectl.config.edgeconfig.py`."""
    def _valid_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeHostConfig()
        test_idx = 1
        for test_case in test_cases:
            setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_value = getattr(config_object, test_attr)
            msg = 'Running valid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            self.assertEqual(test_value, test_case[TEST_OP_KEY], msg)
            config_obj_str = str(config_object)
            self.assertIsNotNone(config_obj_str)
            self.assertNotEqual('', config_obj_str)
            config_obj_dict = config_object.to_dict()
            self.assertIsNotNone(config_obj_dict)
            self.assertNotEqual(0, len(list(config_obj_dict.keys())))
            config_obj_json_str = config_object.to_json()
            self.assertIsNotNone(config_obj_json_str)
            self.assertNotEqual('', config_obj_json_str)
            test_idx += 1

    def _invalid_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeHostConfig()
        test_idx = 1
        print('')
        for test_case in test_cases:
            msg = 'Running invalid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            print(msg)
            with self.assertRaises(ValueError):
                setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_idx += 1

    def test_schema_version_valid(self):
        """Tests getter and setter for schema version using valid input values"""
        test_attr = 'schema_version'
        test_cases = []
        test_cases.append({TEST_IP_KEY: '1', TEST_OP_KEY: '1'})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_schema_version_invalid(self):
        """ Tests getter and setter for schema version
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'schema_version'
        test_cases = []
        test_cases.append({TEST_IP_KEY: '2', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: 'a', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: '', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: None, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_config_dir_valid(self):
        """Tests getter and setter for the IoT Edge config dir using valid input values"""
        test_attr = 'config_dir'
        test_cases = []
        dirpath = tempfile.mkdtemp()
        test_cases.append({TEST_IP_KEY: dirpath, TEST_OP_KEY: dirpath})
        test_cases.append({TEST_IP_KEY: ' ' + dirpath + ' ', TEST_OP_KEY: dirpath})
        self._valid_test_cases_helper(test_attr, test_cases)
        shutil.rmtree(dirpath)

    def test_config_dir_invalid(self):
        """ Tests getter and setter for the IoT Edge config dir
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'config_dir'
        test_cases = []
        test_cases.append({TEST_IP_KEY: '', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: '   ', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: None, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_config_dir_source_valid(self):
        """Tests getter and setter for the IoT Edge config dir using valid input values"""
        test_attr = 'config_dir_source'
        test_cases = []
        test_cases.append({TEST_IP_KEY: EdgeConfigDirInputSource.ENV,
                           TEST_OP_KEY: EdgeConfigDirInputSource.ENV})
        test_cases.append({TEST_IP_KEY: EdgeConfigDirInputSource.USER_PROVIDED,
                           TEST_OP_KEY: EdgeConfigDirInputSource.USER_PROVIDED})
        test_cases.append({TEST_IP_KEY: EdgeConfigDirInputSource.DEFAULT,
                           TEST_OP_KEY: EdgeConfigDirInputSource.DEFAULT})
        test_cases.append({TEST_IP_KEY: EdgeConfigDirInputSource.NONE,
                           TEST_OP_KEY: EdgeConfigDirInputSource.NONE})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_config_dir_source_invalid(self):
        """ Tests getter and setter for the IoT Edge config dir source
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'config_dir_source'
        test_cases = []
        test_cases.append({TEST_IP_KEY: 'blah', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: '', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: 1, TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: None, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_home_dir_valid(self):
        """Tests getter and setter for the IoT Edge home dir using valid input values"""
        test_attr = 'home_dir'
        test_cases = []
        dirpath = tempfile.mkdtemp()
        test_cases.append({TEST_IP_KEY: dirpath, TEST_OP_KEY: dirpath})
        test_cases.append({TEST_IP_KEY: ' ' + dirpath + ' ', TEST_OP_KEY: dirpath})
        self._valid_test_cases_helper(test_attr, test_cases)
        shutil.rmtree(dirpath)

    def test_home_dir_invalid(self):
        """ Tests getter and setter for the IoT Edge home dir using invalid input values
            and verifies if an appropriate exception was raised.
        """
        test_attr = 'home_dir'
        test_cases = []
        test_cases.append({TEST_IP_KEY: '', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: '   ', TEST_OP_KEY: None})
        test_cases.append({TEST_IP_KEY: None, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_connection_string_valid(self):
        """ Tests getter and setter for the IoT Hub connection string
            using valid input values
        """
        test_attr = 'connection_string'
        test_cases = []
        test_strings = [
            'HostName=a;DeviceId=b;SharedAccessKey=c',
            'HostName=a;DeviceId=b;SharedAccessKey=c;',
            '   HostName=a;DeviceId=b;SharedAccessKey=c    ',
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string, TEST_OP_KEY: test_string.strip()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_connection_string_invalid(self):
        """ Tests getter and setter for the IoT Hub connection string
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'connection_string'
        test_cases = []
        test_strings = [
            '',
            '       ',
            None,
            'hostName=a;DeviceId=b;SharedAccessKey=c',
            'Hostname=a;DeviceId=b;SharedAccessKey=c',
            'hostname=a;DeviceId=b;SharedAccessKey=c',
            'HostNam=a;DeviceId=b;SharedAccessKey=c',
            'HostName=aDeviceId=b;SharedAccessKey=c',
            'HostName=;DeviceId=b;SharedAccessKey=c',
            'HostName;DeviceId=b;SharedAccessKey=c',
            'HostName=;DeviceId=b;SharedAccessKey=c',
            'HostName=a;deviceId=b;SharedAccessKey=c',
            'HostName=a;Deviceid=b;SharedAccessKey=c',
            'HostName=a;deviceid=b;SharedAccessKey=c',
            'HostName=a;DeviceI=b;SharedAccessKey=c',
            'HostName=a;DeviceId=bSharedAccessKey=c',
            'HostName=a;DeviceId=;SharedAccessKey=c',
            'HostName=a;DeviceId;SharedAccessKey=c',
            'HostName=a;DeviceId=b;sharedAccessKey=c',
            'HostName=a;DeviceId=b;SharedaccessKey=c',
            'HostName=a;DeviceId=b;SharedAccesskey=c',
            'HostName=a;DeviceId=b;sharedaccesskey=c',
            'HostName=a;DeviceId=b;SharedAccessKe=c',
            'HostName=a;DeviceId=b;SharedAccessKey=',
            'HostName=a;DeviceId=b;SharedAccessKey',
            'HostName=a;',
            'HostName=a;DeviceId=b;',
            'HostName=a;SharedAccessKey=c',
            'DeviceId=b;SharedAccessKey=c'
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_hostname_valid(self):
        """ Tests getter and setter for the IoT Edge Hub hostname
            using valid input values
        """
        test_attr = 'hostname'
        test_cases = []
        test_strings = [
            'fqdn hostname',
            '  fqdn hostname ',
            'FQDN HostName',
            # 64 chars
            '0123456789012345678901234567890123456789012345678901234567890123'
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string,
                               TEST_OP_KEY: test_string.strip().lower()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_hostname_invalid(self):
        """ Tests getter and setter for the IoT Edge Hub hostname
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'hostname'
        test_cases = []
        test_strings = [
            '',
            '       ',
            None,
            # 65 chars
            '01234567890123456789012345678901234567890123456789012345678901234'
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_log_level_valid(self):
        """ Tests getter and setter for the IoT Edge runtime log level
            using valid input values
        """
        test_attr = 'log_level'
        test_cases = []
        test_strings = [
            'info',
            ' info ',
            'debug',
            ' debug ',
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string, TEST_OP_KEY: test_string.strip()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_log_level_invalid(self):
        """ Tests getter and setter for the IoT Edge runtime log level
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'log_level'
        test_cases = []
        test_strings = [
            '',
            '       ',
            None,
            'blah'
        ]
        for test_string in test_strings:
            test_cases.append({TEST_IP_KEY: test_string, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_deployment_config_valid(self):
        """ Tests getter and setter for the IoT Edge deployment config object
            using valid input values
        """
        test_attr = 'deployment_config'
        test_cases = []
        config = EdgeDeploymentConfig(EdgeConstants.DEPLOYMENT_DOCKER)
        test_objects = [
            config,
        ]
        for test_input in test_objects:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: test_input})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_deployment_config_invalid(self):
        """ Tests getter and setter for the IoT Edge deployment config object
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'deployment_config'
        test_cases = []
        config = EdgeDeploymentConfig('blah')
        test_objects = [
            config,
            'blah',
            None,
            128,
            {'a': 'bad input'},
            ('E', 'd', 'g', 'e')
        ]
        for test_input in test_objects:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_certificate_config_valid(self):
        """ Tests getter and setter for the IoT Edge certificate config object
            using valid input values
        """
        test_attr = 'certificate_config'
        test_cases = []
        config = EdgeCertConfig()
        test_objects = [
            config,
        ]
        for test_input in test_objects:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: test_input})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_certificate_config_invalid(self):
        """ Tests getter and setter for the IoT Edge certificate config object
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'certificate_config'
        test_cases = []
        test_objects = [
            'blah',
            None,
            128,
            {'a': 'bad input'},
            ('E', 'd', 'g', 'e')
        ]
        for test_input in test_objects:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)


if __name__ == '__main__':
    SUITE = unittest.TestLoader().loadTestsFromTestCase(TestEdgeConfigMethods)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
