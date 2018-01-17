"""Implementation of tests for module `edgectl.config.dockerconfig.py`."""
from __future__ import print_function
import unittest
from edgectl.config import EdgeConstants as EC
from edgectl.config import EdgeDeploymentConfigDocker

CONFIG_OBJ_KEY = 'config_object'
CONFIG_ATTR_KEY = 'config_attr'
TEST_CASES_LIST_KEY = 'test_cases'
TEST_IP_KEY = 'input'
TEST_OP_KEY = 'output'
PROTO_KEY = 'protocol'
ENDPOINT_KEY = 'endpoint'
PORT_KEY = 'port'
REPO_KEY = 'repo'
IMAGE_NAME_KEY = 'image_name'
TAG_KEY = 'image_tag'

class TestDockerConfigMethods(unittest.TestCase):
    """Unit test implementations for `edgectl.config.dockerconfig.py`."""
    def _valid_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        for test_case in test_cases:
            setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_value = getattr(config_object, test_attr)
            msg = 'Edge Docker Config valid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            self.assertEqual(test_value, test_case[TEST_OP_KEY], msg)
            config_obj_str = str(config_object)
            self.assertIsNotNone(config_obj_str)
            self.assertNotEqual('', config_obj_str)
            config_obj_dict = config_object.to_dict()
            self.assertIsNotNone(config_obj_dict)
            self.assertNotEqual(0, len(list(config_obj_dict.keys())))
            test_idx += 1

    def _invalid_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        print('')
        for test_case in test_cases:
            msg = 'Edge Docker Config invalid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            print(msg)
            with self.assertRaises(ValueError):
                setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_idx += 1

    def test_uri_valid(self):
        """Tests getter and setter for docker URI using valid input values"""
        test_attr = 'uri'
        test_cases = []
        test_inputs = [
            'tcp://0.0.0.0:2375',
            'tcp://localhost:2375',
            '   tcp://localhost:2375   ',
            'http://0.0.0.0:2375',
            'http://localhost:2375',
            '   http://localhost:2375   ',
            'https://0.0.0.0:2375',
            'https://localhost:2375',
            '   https://localhost:2375   ',
            'unix:///var/run/docker.sock',
            '   unix:///var/run/docker.sock   ',
            'npipe://./pipe/docker_engine',
            '   npipe://./pipe/docker_engine   '
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input,
                               TEST_OP_KEY: test_input.strip()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def _valid_uri_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        for test_case in test_cases:
            msg = 'Edge Docker Config URI valid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_case_output = test_case[TEST_OP_KEY]
            test_value = getattr(config_object, 'uri_protocol')
            self.assertEqual(test_value, test_case_output[PROTO_KEY], msg)
            test_value = getattr(config_object, 'uri_endpoint')
            self.assertEqual(test_value, test_case_output[ENDPOINT_KEY], msg)
            test_value = getattr(config_object, 'uri_port')
            self.assertEqual(test_value, test_case_output[PORT_KEY], msg)
            test_idx += 1

    def test_uri_parameters_valid(self):
        """Tests getter and setter for docker URI components such as protocol,
           port, endpoint using valid input values"""
        test_attr = 'uri'
        test_cases = []
        test_inputs = [
            {TEST_IP_KEY: 'tcp://0.0.0.0:2375',
             TEST_OP_KEY: {PROTO_KEY:'tcp://', ENDPOINT_KEY: '0.0.0.0', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'tcp://localhost:2375',
             TEST_OP_KEY: {PROTO_KEY:'tcp://', ENDPOINT_KEY: 'localhost', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'http://0.0.0.0:2375',
             TEST_OP_KEY: {PROTO_KEY:'http://', ENDPOINT_KEY: '0.0.0.0', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'http://localhost:2375',
             TEST_OP_KEY: {PROTO_KEY:'http://', ENDPOINT_KEY: 'localhost', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'https://0.0.0.0:2375',
             TEST_OP_KEY: {PROTO_KEY:'https://', ENDPOINT_KEY: '0.0.0.0', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'https://localhost:2375',
             TEST_OP_KEY: {PROTO_KEY:'https://', ENDPOINT_KEY: 'localhost', PORT_KEY: '2375'}},
            {TEST_IP_KEY: 'unix:///var/run/docker.sock',
             TEST_OP_KEY: {PROTO_KEY:'unix://',
                           ENDPOINT_KEY: '/var/run/docker.sock', PORT_KEY: ''}},
            {TEST_IP_KEY: 'npipe://./pipe/docker_engine',
             TEST_OP_KEY: {PROTO_KEY:'npipe://',
                           ENDPOINT_KEY: EC.DOCKER_ENGINE_WINDOWS_ENDPOINT,
                           PORT_KEY: ''}},
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input[TEST_IP_KEY],
                               TEST_OP_KEY: test_input[TEST_OP_KEY]})
        self._valid_uri_test_cases_helper(test_attr, test_cases)

    def test_uri_invalid(self):
        """ Tests getter and setter for docker URI
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'uri'
        test_cases = []
        test_inputs = [
            None,
            '',
            'blah',
            'blah:',
            'blah://',
            'tcp',
            'tcp:',
            'tcp:/',
            'tcp:/localhost:2375',
            'tcp://',
            'tcp://localhost',
            'http',
            'http:',
            'http:/',
            'http:/localhost:2375',
            'http://',
            'http://localhost',
            'https',
            'https:',
            'https:/',
            'https:/localhost:2375',
            'https://',
            'https://localhost',
            'unix',
            'unix:',
            'unix:/',
            'unix:/localhost:2375',
            'unix://',
            'npipe',
            'npipe:',
            'npipe:/',
            'npipe:/localhost:2375',
            'npipe://'
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_edge_image_valid(self):
        """ Tests getter and setter for the Edge image
            using valid input values.
        """
        test_attr = 'edge_image'
        test_cases = []
        test_inputs = [
            'microsoft/azureiotedge-agent:1.0-preview',
            'test_repo/test_image_name:test-tag',
            'test_repo/sub_project/test_image_name:test-tag',
            '  test_repo/test_image_name:1234  '
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input,
                               TEST_OP_KEY: test_input.strip()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_edge_image_invalid(self):
        """ Tests getter and setter for the Edge image
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'edge_image'
        test_cases = []
        test_inputs = [
            None,
            '',
            '            ',
            'test_repo',
            'test_repo/',
            'test_repo/test_image_name',
            'test_repo/test_image_name:',
            'test_repo/test_image_name:test_tag',
            'test_repo/test_image_name:test-tag$',
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input,
                               TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def _valid_registry_test_cases_helper(self, test_attr, test_cases):
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        for test_case in test_cases:
            msg = 'Edge Image valid tests for {0}. TC# {1}'.format(test_attr, test_idx)
            setattr(config_object, test_attr, test_case[TEST_IP_KEY])
            test_case_output = test_case[TEST_OP_KEY]
            test_value = getattr(config_object, 'edge_image_repository')
            self.assertEqual(test_value, test_case_output[REPO_KEY], msg)
            test_value = getattr(config_object, 'edge_image_name')
            self.assertEqual(test_value, test_case_output[IMAGE_NAME_KEY], msg)
            test_value = getattr(config_object, 'edge_image_tag')
            self.assertEqual(test_value, test_case_output[TAG_KEY], msg)
            test_idx += 1

    def test_registry_parameters_valid(self):
        """Tests getter and setter for docker URI components such as protocol,
           port, endpoint using valid input values"""
        test_attr = 'edge_image'
        test_cases = []
        test_inputs = [
            {TEST_IP_KEY: 'test_repo/test_image_name:1234',
             TEST_OP_KEY: {REPO_KEY:'test_repo',
                           IMAGE_NAME_KEY:'test_image_name', TAG_KEY: '1234'}},
            {TEST_IP_KEY: 'test_repo/sub_project/test_image_name:1234',
             TEST_OP_KEY: {REPO_KEY:'test_repo',
                           IMAGE_NAME_KEY: 'sub_project/test_image_name', TAG_KEY: '1234'}}
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input[TEST_IP_KEY],
                               TEST_OP_KEY: test_input[TEST_OP_KEY]})
        self._valid_registry_test_cases_helper(test_attr, test_cases)

    def test_registry_valid(self):
        """ Tests getter and setter for the Docker registry list
            using valid input values.
        """
        test_val_data = {
            'test_reg_1': {EC.REGISTRY_ADDRESS_KEY: 'test_reg_1',
                           EC.REGISTRY_USERNAME_KEY:'',
                           EC.REGISTRY_PASSWORD_KEY: ''},
            'test_reg_2': {EC.REGISTRY_ADDRESS_KEY: 'test_reg_2',
                           EC.REGISTRY_USERNAME_KEY:'username_2',
                           EC.REGISTRY_PASSWORD_KEY: ''},
            'test_reg_3': {EC.REGISTRY_ADDRESS_KEY: 'test_reg_3',
                           EC.REGISTRY_USERNAME_KEY:'username_3',
                           EC.REGISTRY_PASSWORD_KEY: 'password_3'},
            'test_reg_4': {EC.REGISTRY_ADDRESS_KEY: 'test_reg_4',
                           EC.REGISTRY_USERNAME_KEY:'',
                           EC.REGISTRY_PASSWORD_KEY: ''},
            'test_reg_5': {EC.REGISTRY_ADDRESS_KEY: 'test_reg_5',
                           EC.REGISTRY_USERNAME_KEY:'',
                           EC.REGISTRY_PASSWORD_KEY: ''},
        }
        config_object = EdgeDeploymentConfigDocker()
        self.assertEqual(0, len(config_object.registries))
        config_object.add_registry('test_reg_1')
        self.assertEqual(1, len(config_object.registries))
        config_object.add_registry('test_reg_2', 'username_2')
        self.assertEqual(2, len(config_object.registries))
        config_object.add_registry('test_reg_3', 'username_3', 'password_3')
        self.assertEqual(3, len(config_object.registries))
        config_object.add_registry('test_reg_4', 'username_4', 'password_4')
        self.assertEqual(4, len(config_object.registries))
        config_object.add_registry('test_reg_4',)
        self.assertEqual(4, len(config_object.registries))
        config_object.add_registry('  test_reg_5  ')
        self.assertEqual(5, len(config_object.registries))
        registries = config_object.registries
        for reg in registries:
            test_id = reg[EC.REGISTRY_ADDRESS_KEY]
            test_dict = test_val_data[test_id]
            self.assertEqual(test_dict[EC.REGISTRY_ADDRESS_KEY], reg[EC.REGISTRY_ADDRESS_KEY])
            self.assertEqual(test_dict[EC.REGISTRY_USERNAME_KEY], reg[EC.REGISTRY_USERNAME_KEY])
            self.assertEqual(test_dict[EC.REGISTRY_PASSWORD_KEY], reg[EC.REGISTRY_PASSWORD_KEY])
            config_obj_str = str(config_object)
            self.assertIsNotNone(config_obj_str)
            self.assertNotEqual('', config_obj_str)

    def test_registry_invalid(self):
        """ Tests getter and setter for the Edge image
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_inputs = [
            {EC.REGISTRY_ADDRESS_KEY: None,
             EC.REGISTRY_USERNAME_KEY: None,
             EC.REGISTRY_PASSWORD_KEY: None},
            {EC.REGISTRY_ADDRESS_KEY: '',
             EC.REGISTRY_USERNAME_KEY: None,
             EC.REGISTRY_PASSWORD_KEY: None},
            {EC.REGISTRY_ADDRESS_KEY: '   ',
             EC.REGISTRY_USERNAME_KEY: None,
             EC.REGISTRY_PASSWORD_KEY: None},
            {EC.REGISTRY_ADDRESS_KEY: 'test',
             EC.REGISTRY_USERNAME_KEY: None,
             EC.REGISTRY_PASSWORD_KEY: None},
            {EC.REGISTRY_ADDRESS_KEY: 'test',
             EC.REGISTRY_USERNAME_KEY: 'blah',
             EC.REGISTRY_PASSWORD_KEY: None},
            {EC.REGISTRY_ADDRESS_KEY: 'test',
             EC.REGISTRY_USERNAME_KEY: None,
             EC.REGISTRY_PASSWORD_KEY: 'blah'}
        ]
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        print('')
        for test_input in test_inputs:
            print('Edge Docker registry invalid TC# {0}'.format(test_idx))
            with self.assertRaises(ValueError):
                config_object.add_registry(test_input[EC.REGISTRY_ADDRESS_KEY],
                                           test_input[EC.REGISTRY_USERNAME_KEY],
                                           test_input[EC.REGISTRY_PASSWORD_KEY])
            self.assertEqual(0, len(config_object.registries))
            test_idx += 1

    def test_logging_driver_valid(self):
        """Tests getter and setter for docker logging driver using valid input values"""
        test_attr = 'logging_driver'
        test_cases = []
        test_inputs = [
            'json-file',
            'journald',
            '   journald   ',
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input,
                               TEST_OP_KEY: test_input.strip()})
        self._valid_test_cases_helper(test_attr, test_cases)

    def test_logging_driver_invalid(self):
        """ Tests getter and setter for docker logging driver
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_attr = 'logging_driver'
        test_cases = []
        test_inputs = [
            None,
            '',
        ]
        for test_input in test_inputs:
            test_cases.append({TEST_IP_KEY: test_input, TEST_OP_KEY: None})
        self._invalid_test_cases_helper(test_attr, test_cases)

    def test_logging_options_valid(self):
        """ Tests getter and setter for the docker logging options
            using valid input values.
        """
        test_inputs = [
            ('op1', 'val1'),
            ('op2', 'val2'),
            ('op3', 'val3'),
        ]
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        print('')
        for test_input in test_inputs:
            msg = 'Edge Docker logging options valid TC# {0}'.format(test_idx)
            config_object.add_logging_option(test_input[0], test_input[1])
            self.assertEqual(test_idx, len(config_object.logging_options.keys()), msg)
            self.assertTrue(test_input[0] in list(config_object.logging_options.keys()), msg)
            self.assertEqual(test_input[1], config_object.logging_options[test_input[0]], msg)
            config_obj_str = str(config_object)
            self.assertIsNotNone(config_obj_str)
            self.assertNotEqual('', config_obj_str)
            test_idx += 1
        config_object.add_logging_option('  opt4  ', '  val4 ')
        self.assertTrue('opt4' in list(config_object.logging_options.keys()))
        self.assertEqual('val4', config_object.logging_options['opt4'])

    def test_logging_options_invalid(self):
        """ Tests getter and setter for docker logging options
            using invalid input values and verifies if an appropriate
            exception was raised.
        """
        test_inputs = [
            (None, None),
            ('', None),
            (None, ''),
            ('', ''),
            ('', 'blah'),
            ('blah', ''),

        ]
        config_object = EdgeDeploymentConfigDocker()
        test_idx = 1
        print('')
        for test_input in test_inputs:
            print('Edge Docker logging options invalid TC# {0}'.format(test_idx))
            with self.assertRaises(ValueError):
                config_object.add_logging_option(test_input[0], test_input[1])
            self.assertEqual(0, len(config_object.registries))
            test_idx += 1
        config_object.add_logging_option('duplicate_opt', 'blah')
        with self.assertRaises(ValueError):
            config_object.add_logging_option('duplicate_opt', 'val')
        self.assertEqual(1, len(list(config_object.logging_options.keys())))

if __name__ == '__main__':
    SUITE = unittest.TestLoader().loadTestsFromTestCase(TestDockerConfigMethods)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
