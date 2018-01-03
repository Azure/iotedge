"""Implementation of tests for module `edgectl.config.default.py`."""
import os
import shutil
import tempfile
import unittest
from mock import patch
from mock import MagicMock
from edgectl.config import EdgeConstants
from edgectl.config import EdgeDefault
import edgectl.errors

# pylint: disable=R0904
class TestEdgeDefaultMethods(unittest.TestCase):
    """Unit tests for `config/default.py`."""

    def test_is_host_supported_valid(self):
        """Test valid host OS platforms returns True"""
        self.assertTrue(EdgeDefault.is_host_supported('windows'))
        self.assertTrue(EdgeDefault.is_host_supported('Windows'))
        self.assertTrue(EdgeDefault.is_host_supported('Linux'))
        self.assertTrue(EdgeDefault.is_host_supported('linux'))
        self.assertTrue(EdgeDefault.is_host_supported('Darwin'))
        self.assertTrue(EdgeDefault.is_host_supported('darwin'))

    def test_is_host_supported_invalid(self):
        """Test invalid host OS platforms returns False"""
        self.assertFalse(EdgeDefault.is_host_supported(''))
        self.assertFalse(EdgeDefault.is_host_supported('blah'))

    def test_is_host_supported_except(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.is_host_supported(None)

    def test_is_deploy_supported_valid(self):
        """Test valid host OS platform deployments returns True"""
        self.assertTrue(EdgeDefault.is_deployment_supported('windows', 'docker'))
        self.assertTrue(EdgeDefault.is_deployment_supported('linux', 'Docker'))
        self.assertTrue(EdgeDefault.is_deployment_supported('Darwin', 'docker'))

    def test_is_deploy_supported_inv(self):
        """Test invalid host OS platform deployments returns False"""
        self.assertFalse(EdgeDefault.is_deployment_supported('windows', 'blah'))
        self.assertFalse(EdgeDefault.is_deployment_supported('windows', ''))
        self.assertFalse(EdgeDefault.is_deployment_supported('blah', 'docker'))
        self.assertFalse(EdgeDefault.is_deployment_supported('', 'docker'))

    def test_is_deploy_supported_except(self):
        """
            Test None as host OS and deployment_type
            raises edgectl.errors.EdgeInvalidArgument
        """
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.is_deployment_supported('Linux', None)
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.is_deployment_supported(None, 'docker')

    def test_get_docker_ctr_type_valid(self):
        """Test valid host OS platform docker container types"""
        types = EdgeDefault.get_docker_container_types('windows')
        self.assertTrue(len(types) == 2)
        self.assertTrue('linux' in types)
        self.assertTrue('windows' in types)
        types = EdgeDefault.get_docker_container_types('darwin')
        self.assertTrue(len(types) == 1)
        self.assertTrue('linux' in types)
        types = EdgeDefault.get_docker_container_types('linux')
        self.assertTrue(len(types) == 1)
        self.assertTrue('linux' in types)

    def test_get_docker_ctr_type_inv(self):
        """Test invalid host OS platform docker container types"""
        types = EdgeDefault.get_docker_container_types('blah')
        self.assertTrue(len(types) == 0)
        types = EdgeDefault.get_docker_container_types('')
        self.assertTrue(len(types) == 0)

    def test_get_docker_ctr_type_except(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_docker_container_types(None)

    def test_get_config_dir_valid(self):
        """Test valid host OS platform returns a non empty directory"""
        dir_name = EdgeDefault.get_config_dir('windows')
        self.assertIsNotNone(dir_name)
        self.assertGreater(len(dir_name), 0)
        dir_name = EdgeDefault.get_config_dir('linux')
        self.assertIsNotNone(dir_name)
        self.assertGreater(len(dir_name), 0)
        dir_name = EdgeDefault.get_config_dir('darwin')
        self.assertIsNotNone(dir_name)
        self.assertGreater(len(dir_name), 0)

    def test_get_config_dir_invalid(self):
        """Test invalid host OS platform returns None"""
        dir_name = EdgeDefault.get_config_dir('blah')
        self.assertIsNone(dir_name)
        dir_name = EdgeDefault.get_config_dir('')
        self.assertIsNone(dir_name)

    def test_get_config_dir_exception(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_config_dir(None)

    def test_get_edge_diag_path_valid(self):
        """Test valid host OS platform returns a non empty string"""
        path = EdgeDefault.get_edge_ctl_diagnostic_path('windows')
        self.assertIsNotNone(path)
        self.assertEqual('%%USERPROFILE%%\\.iotedgectl', path)
        path = EdgeDefault.get_edge_ctl_diagnostic_path('linux')
        self.assertIsNotNone(path)
        self.assertEqual('$HOME/.iotedgectl', path)
        self.assertGreater(len(path), 0)
        path = EdgeDefault.get_edge_ctl_diagnostic_path('darwin')
        self.assertIsNotNone(path)
        self.assertEqual('$HOME/.iotedgectl', path)

    def test_get_edge_diag_path_invalid(self):
        """Test invalid host OS platform returns None"""
        path = EdgeDefault.get_edge_ctl_diagnostic_path('blah')
        self.assertIsNone(path)
        path = EdgeDefault.get_edge_ctl_diagnostic_path('')
        self.assertIsNone(path)

    def test_get_edge_diag_path_except(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_edge_ctl_diagnostic_path(None)

    def _get_edge_ctl_config_dir_helper(self, host, dirpath, test_config_file=None):
        test_path = None
        if dirpath:
            test_path = os.path.join(dirpath, '.iotedgectl')
        with patch('platform.system', MagicMock(return_value=host)):
            with patch('os.getenv', MagicMock(return_value=dirpath)):
                result_path = EdgeDefault.get_edge_ctl_config_dir()
                self.assertIsNotNone(result_path)
                self.assertEqual(test_path, result_path)
                if dirpath and test_config_file:
                    test_path = os.path.join(test_path, test_config_file)
                    result_path = EdgeDefault.get_meta_conf_file_path()
                    self.assertEqual(test_path, result_path)

    def test_get_edgectl_cfg_dir_valid(self):
        """
            Test valid host OS platform returns a non empty string
            with a valid expected dir path
        """
        dirpath = tempfile.mkdtemp()
        self._get_edge_ctl_config_dir_helper('windows', dirpath)
        self._get_edge_ctl_config_dir_helper('Darwin', dirpath)
        self._get_edge_ctl_config_dir_helper('linux', dirpath)
        shutil.rmtree(dirpath)

    def test_get_edgectl_cfg_dir_except(self):
        """
            Test invalid host OS platform and unset env variable
            raises exception edgectl.errors.EdgeValueError
        """
        dirpath = tempfile.mkdtemp()
        with self.assertRaises(edgectl.errors.EdgeValueError):
            self._get_edge_ctl_config_dir_helper('blah', dirpath)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            self._get_edge_ctl_config_dir_helper('linux', None)
        shutil.rmtree(dirpath)

    def test_get_edgectl_cfg_file_valid(self):
        """
        Test valid host OS platform returns a non empty string
        with the expected file path
        """
        dirpath = tempfile.mkdtemp()
        self._get_edge_ctl_config_dir_helper('windows', dirpath, 'config.json')
        self._get_edge_ctl_config_dir_helper('Darwin', dirpath, 'config.json')
        self._get_edge_ctl_config_dir_helper('linux', dirpath, 'config.json')
        shutil.rmtree(dirpath)

    def test_edgectl_cfg_file_except(self):
        """
        Test invalid host OS platform and unset env variable
        raises exception edgectl.errors.EdgeValueError
        """
        with self.assertRaises(edgectl.errors.EdgeValueError):
            self._get_edge_ctl_config_dir_helper('blah', 'blah', 'config.json')
        with self.assertRaises(edgectl.errors.EdgeValueError):
            self._get_edge_ctl_config_dir_helper('linux', None, 'config.json')

    def test_get_config_file_name_valid(self):
        """ Test if the config file name is valid and as expected"""
        test_name = EdgeDefault.get_config_file_name()
        self.assertEqual('config.json', test_name)

    def test_get_deployments_valid(self):
        """" Test deployments list for valid host OS platforms """
        test_deployments = EdgeDefault.get_supported_deployments('linux')
        self.assertIsNotNone(test_deployments)
        self.assertIn(EdgeConstants.DEPLOYMENT_DOCKER_KEY, test_deployments)
        test_deployments = EdgeDefault.get_supported_deployments('darwin')
        self.assertIsNotNone(test_deployments)
        self.assertIn(EdgeConstants.DEPLOYMENT_DOCKER_KEY, test_deployments)
        test_deployments = EdgeDefault.get_supported_deployments('windows')
        self.assertIsNotNone(test_deployments)
        self.assertIn(EdgeConstants.DEPLOYMENT_DOCKER_KEY, test_deployments)

    def test_get_deployments_invalid(self):
        """" Test empty deployments list for invalid host OS platforms """
        test_deployments = EdgeDefault.get_supported_deployments('blah')
        self.assertIsNotNone(test_deployments)
        self.assertEqual(0, len(test_deployments))
        test_deployments = EdgeDefault.get_supported_deployments('')
        self.assertIsNotNone(test_deployments)
        self.assertEqual(0, len(test_deployments))

    def test_get_deployments_except(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_supported_deployments(None)

    def test_cert_subject_dict_valid(self):
        """ Test if the certificate subject dict is valid and as expected"""
        test_dict = EdgeDefault.get_certificate_subject_dict()
        self.assertIn(EdgeConstants.SUBJECT_COUNTRY_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_COUNTRY_KEY])
        self.assertIn(EdgeConstants.SUBJECT_STATE_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_STATE_KEY])
        self.assertIn(EdgeConstants.SUBJECT_LOCALITY_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_LOCALITY_KEY])
        self.assertIn(EdgeConstants.SUBJECT_ORGANIZATION_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_ORGANIZATION_KEY])
        self.assertIn(EdgeConstants.SUBJECT_ORGANIZATION_UNIT_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_ORGANIZATION_UNIT_KEY])
        self.assertIn(EdgeConstants.SUBJECT_COMMON_NAME_KEY, test_dict)
        self.assertIsNotNone(test_dict[EdgeConstants.SUBJECT_COMMON_NAME_KEY])

    def test_docker_uri_valid(self):
        """ Get the docker URI for valid host and container types """
        test_uri = EdgeDefault.get_docker_uri('linux', 'linux')
        self.assertIsNotNone(test_uri)
        linux_test_uri = test_uri
        self.assertEqual('unix:///var/run/docker.sock', linux_test_uri)
        test_uri = EdgeDefault.get_docker_uri('darwin', 'linux')
        self.assertIsNotNone(test_uri)
        self.assertEqual(linux_test_uri, test_uri)
        test_uri = EdgeDefault.get_docker_uri('Windows', 'linux')
        self.assertIsNotNone(test_uri)
        self.assertEqual(linux_test_uri, test_uri)
        test_uri = EdgeDefault.get_docker_uri('Windows', 'Windows')
        self.assertIsNotNone(test_uri)
        self.assertEqual('npipe://./pipe/docker_engine', test_uri)

    def test_docker_uri_invalid(self):
        """ Test invalid host and container types returns None"""
        test_uri = EdgeDefault.get_docker_uri('blah', 'linux')
        self.assertIsNone(test_uri)
        test_uri = EdgeDefault.get_docker_uri('linux', 'blah')
        self.assertIsNone(test_uri)
        test_uri = EdgeDefault.get_docker_uri('linux', 'windows')
        self.assertIsNone(test_uri)

    def test_docker_uri_except(self):
        """ Test host and container as None raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_docker_uri(None, 'linux')
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_docker_uri('linux', None)

    def test_get_home_dir_valid(self):
        """Test valid host OS platform returns a non empty string"""
        path = EdgeDefault.get_home_dir('linux')
        self.assertIsNotNone(path)
        self.assertGreater(len(path), 0)
        path = EdgeDefault.get_home_dir('darwin')
        self.assertIsNotNone(path)
        self.assertGreater(len(path), 0)
        path = EdgeDefault.get_home_dir('windows')
        self.assertIsNotNone(path)
        self.assertGreater(len(path), 0)

    def test_get_home_dir_invalid(self):
        """Test invalid host OS platform returns None"""
        path = EdgeDefault.get_home_dir('blah')
        self.assertIsNone(path)
        path = EdgeDefault.get_home_dir('')
        self.assertIsNone(path)

    def test_get_home_dir_exception(self):
        """Test None as host OS raises edgectl.errors.EdgeInvalidArgument"""
        with self.assertRaises(edgectl.errors.EdgeInvalidArgument):
            EdgeDefault.get_home_dir(None)

    def test_def_settings_file_valid(self):
        """Test API returns a non empty string and a valid path that exists on the host"""
        test_path = EdgeDefault.get_default_settings_file_path()
        self.assertIsNotNone(test_path)
        self.assertTrue(os.path.exists(test_path))

    def test_def_settings_json_valid(self):
        """ Test if valid dict is returned after parsing defaults JSON configuration """
        test_dict = EdgeDefault.get_default_settings_json()
        self.assertIsNotNone(test_dict)

    def test_def_settings_json_except(self):
        """ Test if exceptions raised when unable to read the JSON config or parse it. """
        with patch('edgectl.config.EdgeDefault.get_default_settings_file_path',
                   MagicMock(return_value='bad_file.json')):
            with self.assertRaises(edgectl.errors.EdgeFileAccessError):
                EdgeDefault.get_default_settings_json()
        with patch('json.load', MagicMock(return_value=None)) as json_load_mock:
            json_load_mock.side_effect = ValueError()
            with self.assertRaises(edgectl.errors.EdgeFileParseError):
                EdgeDefault.get_default_settings_json()

    def test_runtime_log_levels_valid(self):
        """Test to validate the log level settings and default setting"""
        test_levels = EdgeDefault.get_runtime_log_levels()
        self.assertIsNotNone(test_levels)
        self.assertTrue(len(test_levels))
        test_level = EdgeDefault.get_default_runtime_log_level()
        self.assertIn(test_level, test_levels)

if __name__ == '__main__':
    SUITE = unittest.TestLoader().loadTestsFromTestCase(TestEdgeDefaultMethods)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
