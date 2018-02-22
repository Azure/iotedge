"""Implementation of tests for module `edgectl.config.certconfig.py`."""
from __future__ import print_function
import sys
import unittest
from mock import patch, mock_open, MagicMock
from edgectl.config import EdgeConstants as EC
from edgectl.config import EdgeDefault
from edgectl.config import EdgeCertConfig
import edgectl.errors

TEST_IP_KEY = 'input'
TEST_OP_KEY = 'output'
TEST_KW_ARGS = 'kwargs'
TEST_RESULT_KEY = 'result'
KWARG_OWNER_CERT = 'owner_ca_cert_file'
KWARG_DCA_CERT = 'device_ca_cert_file'
KWARG_DCA_CHAIN = 'device_ca_chain_cert_file'
KWARG_DCA_PK = 'device_ca_private_key_file'
KWARG_DCA_PASS = 'device_ca_passphrase'
KWARG_DCA_PASS_FILE = 'device_ca_passphrase_file'
KWARG_AGT_PASS = 'agent_ca_passphrase'
KWARG_AGT_PASS_FILE = 'agent_ca_passphrase_file'

# Test file names
OWNER_CERT_FILE_NAME = 'test_owner_ca_cert.pem'
DEVICE_CA_CERT_FILE_NAME = 'test_device_ca_cert.pem'
DEVICE_CA_CHAIN_CERT_FILE_NAME = 'test_device_ca_cert.pem'
DEVICE_CA_PRIVATE_KEY_FILE_NAME = 'test_device_ca_private.pem'
DEVICE_CA_PASS_FILE_NAME = 'device_ca_passphrase.txt'
AGENT_CA_PASS_FILE_NAME = 'agent_ca_passphrase.txt'


if sys.version_info[0] < 3:
    OPEN_BUILTIN = '__builtin__.open'
else:
    OPEN_BUILTIN = 'builtins.open'

# pylint: disable=C0103
# disables invalid method name warning which is triggered because the test names are long
class TestEdgeCertConfigPreInitialization(unittest.TestCase):
    """
        Unit test implementations for `edgectl.config.certconfig.py`
        before API set_options() is invoked to setup the various
        certificate options.
    """
    def test_no_security_options_set_api_str_returns_empty_string(self):
        """ Tests __str__ implementation before the cert config object is
            configured with certificate settings to operate the Edge.
        """
        config_object = EdgeCertConfig()
        result_str = str(config_object)
        self.assertEqual('', result_str)

    def test_no_security_options_set_api_to_dict_returns_empty_dict(self):
        """ Tests to_dict implementation before the cert config object is
            configured with certificate settings to operate the Edge.
        """
        config_object = EdgeCertConfig()
        result_dict = config_object.to_dict()
        self.assertEqual(0, len(list(result_dict[EC.CERTS_KEY].keys())))

    def test_no_security_options_set_api_use_self_signed_certificates_invalid(self):
        """ Tests API use_self_signed_certificates() implementation
            before the cert config object is configured with certificate
            settings to operate the Edge. This should raise a ValueError exception.
        """
        config_object = EdgeCertConfig()
        with self.assertRaises(ValueError):
            config_object.use_self_signed_certificates()

    def test_no_security_options_getter_properties_returns_default_values(self):
        """ Tests getter simplementations
            before the cert config object is configured with certificate
            settings to operate the Edge. These all getters
            should return their respective default values.
        """
        config_object = EdgeCertConfig()
        self.assertFalse(config_object.force_no_passwords)
        self.assertIsNone(config_object.agent_ca_passphrase)
        self.assertIsNone(config_object.agent_ca_passphrase_file_path)
        self.assertIsNone(config_object.device_ca_cert_file_path)
        self.assertIsNone(config_object.device_ca_chain_cert_file_path)
        self.assertIsNone(config_object.device_ca_passphrase)
        self.assertIsNone(config_object.device_ca_passphrase_file_path)
        self.assertIsNone(config_object.device_ca_private_key_file_path)
        self.assertEqual({}, config_object.certificate_subject_dict)


# pylint: disable=C0103
# disables invalid method name warning which is triggered because the test names are long
class TestEdgeCertConfigMethods(unittest.TestCase):
    """Unit test implementations for `edgectl.config.certconfig.py`."""

    def _validate_results_helper(self, result_config_object, test_case, msg=None):
        """ Get all the expected outputs and validate against the result EdgeCertConfig object"""
        self._validation_helper_force_no_pass(result_config_object, test_case, msg)
        self._validation_helper_certificate_options(result_config_object, test_case, msg)
        self._validation_helper_certificate_subject(result_config_object, test_case, msg)
        self._validation_helper_kwargs(result_config_object, test_case, msg)
        self._validation_helper_api_str(result_config_object, test_case, msg)
        self._validation_helper_api_to_dict(result_config_object, test_case, msg)

    def _validation_helper_force_no_pass(self, result_config_object, test_case, msg=None):
        # get expected force no password's setting
        op_force_no_pass = test_case[TEST_OP_KEY][EC.FORCENOPASSWD_KEY]
        # validate force no password setting
        self.assertEqual(op_force_no_pass, result_config_object.force_no_passwords, msg)

    def _validation_helper_certificate_options(self, result_config_object, test_case, msg=None):
        op_option = test_case[TEST_OP_KEY][EC.CERTS_OPTION_KEY]
        self.assertIn(op_option, [EC.SELFSIGNED_KEY, EC.PREINSTALL_KEY])
        if op_option == EC.SELFSIGNED_KEY:
            self.assertTrue(result_config_object.use_self_signed_certificates(), msg)
        else:
            self.assertFalse(result_config_object.use_self_signed_certificates(), msg)

    def _validation_helper_certificate_subject(self, result_config_object, test_case, msg=None):
        op_subj_dict = test_case[TEST_OP_KEY][EC.CERTS_SUBJECT_KEY]
        if op_subj_dict is None:
            op_subj_dict = EdgeDefault.get_certificate_subject_dict()

        result_subj_dict = result_config_object.certificate_subject_dict
        self.assertEqual(op_subj_dict, result_subj_dict, msg)


    def _validation_helper_kwargs(self, result_config_object, test_case, msg=None):
        # for self signed autogen test cases expected KW args are expected to be None
        op_kwargs = test_case[TEST_OP_KEY][TEST_KW_ARGS]
        if op_kwargs is None:
            op_kwargs = {
                KWARG_OWNER_CERT: None,
                KWARG_DCA_CERT: None,
                KWARG_DCA_CHAIN: None,
                KWARG_DCA_PK: None,
                KWARG_DCA_PASS: None,
                KWARG_DCA_PASS_FILE: None,
                KWARG_AGT_PASS: None,
                KWARG_AGT_PASS_FILE: None
            }
        # compare the other properties related to the device CA flow
        self.assertEqual(op_kwargs[KWARG_OWNER_CERT],
                         result_config_object.owner_ca_cert_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_DCA_CERT],
                         result_config_object.device_ca_cert_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_DCA_CHAIN],
                         result_config_object.device_ca_chain_cert_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_DCA_PK],
                         result_config_object.device_ca_private_key_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_DCA_PASS_FILE],
                         result_config_object.device_ca_passphrase_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_DCA_PASS],
                         result_config_object.device_ca_passphrase, msg)
        self.assertEqual(op_kwargs[KWARG_AGT_PASS_FILE],
                         result_config_object.agent_ca_passphrase_file_path, msg)
        self.assertEqual(op_kwargs[KWARG_AGT_PASS],
                         result_config_object.agent_ca_passphrase, msg)

    def _validation_helper_api_str(self, result_config_object, test_case, msg=None):
        result_str = str(result_config_object)
        self.assertIsNotNone(result_str)
        self.assertNotEqual('', result_str, msg)
        op_option = test_case[TEST_OP_KEY][EC.CERTS_OPTION_KEY]
        self.assertNotEqual(-1, result_str.find(op_option))

    def _validation_helper_api_to_dict(self, result_config_object, test_case, msg=None):
        config_obj_dict = result_config_object.to_dict()
        self.assertIsNotNone(config_obj_dict, msg)
        self.assertEqual(1, len(list(config_obj_dict.keys())), msg)
        self.assertIn(EC.CERTS_KEY, config_obj_dict, msg)

        op_option = test_case[TEST_OP_KEY][EC.CERTS_OPTION_KEY]
        res_dict = {
            EC.CERTS_OPTION_KEY: op_option,
            EC.CERTS_SUBJECT_KEY: result_config_object.certificate_subject_dict
        }
        if op_option == EC.SELFSIGNED_KEY:
            test_dict = {
                EC.FORCENOPASSWD_KEY: result_config_object.force_no_passwords,
                EC.DEVICE_CA_PASSPHRASE_FILE_KEY:
                    result_config_object.device_ca_passphrase_file_path,
                EC.AGENT_CA_PASSPHRASE_FILE_KEY:
                    result_config_object.agent_ca_passphrase_file_path
            }
        elif op_option == EC.PREINSTALL_KEY:
            test_dict = {
                EC.FORCENOPASSWD_KEY: result_config_object.force_no_passwords,
                EC.DEVICE_CA_PASSPHRASE_FILE_KEY:
                    result_config_object.device_ca_passphrase_file_path,
                EC.AGENT_CA_PASSPHRASE_FILE_KEY:
                    result_config_object.agent_ca_passphrase_file_path,
                EC.PREINSTALL_OWNER_CA_CERT_KEY:
                    result_config_object.owner_ca_cert_file_path,
                EC.PREINSTALL_DEVICE_CERT_KEY:
                    result_config_object.device_ca_cert_file_path,
                EC.PREINSTALL_DEVICE_CHAINCERT_KEY:
                    result_config_object.device_ca_chain_cert_file_path,
                EC.PREINSTALL_DEVICE_PRIVKEY_KEY:
                    result_config_object.device_ca_private_key_file_path
            }
        res_dict[op_option] = test_dict
        self.maxDiff = None
        self.assertEqual(res_dict, config_obj_dict[EC.CERTS_KEY])

    def _valid_test_cases_helper(self, test_type, test_cases):
        config_object = EdgeCertConfig()
        test_idx = 1
        for test_case in test_cases:
            # get all the inputs
            ip_force_no_pass = test_case[TEST_IP_KEY][EC.FORCENOPASSWD_KEY]
            ip_subj_dict = test_case[TEST_IP_KEY][EC.CERTS_SUBJECT_KEY]
            ip_kwargs = test_case[TEST_IP_KEY][TEST_KW_ARGS]

            # execute the test case
            if ip_kwargs:
                config_object.set_options(ip_force_no_pass, ip_subj_dict, **ip_kwargs)
            else:
                config_object.set_options(ip_force_no_pass, ip_subj_dict)

            # perform validations
            msg = 'Edge Cert Config valid tests for {0}. TC# {1}'.format(test_type, test_idx)
            self._validate_results_helper(config_object, test_case, msg)
            test_idx += 1

    def _invalid_test_cases_helper(self, test_type, test_cases):
        config_object = EdgeCertConfig()
        test_idx = 1
        print('')
        for test_case in test_cases:
            msg = 'Edge Cert Config invalid tests for {0}. TC# {1}'.format(test_type, test_idx)
            print(msg)
            # get all the inputs
            ip_force_no_pass = test_case[TEST_IP_KEY][EC.FORCENOPASSWD_KEY]
            ip_subj_dict = test_case[TEST_IP_KEY][EC.CERTS_SUBJECT_KEY]
            ip_kwargs = test_case[TEST_IP_KEY][TEST_KW_ARGS]
            with self.assertRaises(ValueError):
                # execute the test case
                if ip_kwargs:
                    config_object.set_options(ip_force_no_pass, ip_subj_dict, **ip_kwargs)
                else:
                    config_object.set_options(ip_force_no_pass, ip_subj_dict)
            test_idx += 1

    @staticmethod
    def _add_test_case(test_cases_list, force_no_pass, op_certs_option,
                       ip_kwargs_dict=None, op_kwargs_dict=None):
        test_case = {TEST_IP_KEY: {EC.FORCENOPASSWD_KEY: force_no_pass,
                                   EC.CERTS_SUBJECT_KEY: None,
                                   TEST_KW_ARGS: ip_kwargs_dict},
                     TEST_OP_KEY: {EC.FORCENOPASSWD_KEY: force_no_pass,
                                   EC.CERTS_OPTION_KEY: op_certs_option,
                                   EC.CERTS_SUBJECT_KEY: None,
                                   TEST_KW_ARGS: op_kwargs_dict}}
        test_cases_list.append(test_case)

    @staticmethod
    def _add_subj_test_case(test_cases_list, force_no_pass, op_certs_option,
                            subj_ip_dict, subj_op_dict, **kwargs):
        ip_kwargs_dict = None
        op_kwargs_dict = None
        if 'ip_kwargs_dict' in kwargs:
            ip_kwargs_dict = kwargs['ip_kwargs_dict']
            op_kwargs_dict = kwargs['op_kwargs_dict']

        test_case = {TEST_IP_KEY: {EC.FORCENOPASSWD_KEY: force_no_pass,
                                   EC.CERTS_SUBJECT_KEY: subj_ip_dict,
                                   TEST_KW_ARGS: ip_kwargs_dict},
                     TEST_OP_KEY: {EC.FORCENOPASSWD_KEY: force_no_pass,
                                   EC.CERTS_OPTION_KEY: op_certs_option,
                                   EC.CERTS_SUBJECT_KEY: subj_op_dict,
                                   TEST_KW_ARGS: op_kwargs_dict}}
        test_cases_list.append(test_case)

    ############################################################################
    # Self signed certificate option (flow) tests
    ############################################################################
    def test_self_signed_force_no_passphrase_invalid(self):
        """ Tests getter and setter for force no passwords.
            This test is for the auto generated self signed certificate option
            using invalid input values for force no passwords.
            Test verifies if an appropriate exception was raised.
        """
        test_cases = []
        self._add_test_case(test_cases, None, EC.SELFSIGNED_KEY)
        self._add_test_case(test_cases, '', EC.SELFSIGNED_KEY)
        self._add_test_case(test_cases, 'blah', EC.SELFSIGNED_KEY)
        self._add_test_case(test_cases, 23, EC.SELFSIGNED_KEY)
        self._invalid_test_cases_helper('self signed force no passphrase', test_cases)

    def test_self_signed_passphrase_valid(self):
        """ This test is for the auto generated self signed certificate flow
            using valid input values.
        """
        test_cases = []
        self._add_test_case(test_cases, True, EC.SELFSIGNED_KEY)
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY)
        self._valid_test_cases_helper('self signed force no passphrase', test_cases)

    def test_self_signed_with_kw_args_passphrase_valid(self):
        """
            This test is for the auto generated self signed certificate flow
            using valid kwarg input values.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: 'ABCD'
        }
        op_kwargs = {
            KWARG_OWNER_CERT: None,
            KWARG_DCA_CERT: None,
            KWARG_DCA_CHAIN: None,
            KWARG_DCA_PK: None,
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: 'ABCD',
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_AGT_PASS: 'ABCD',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
        }
        op_kwargs = {
            KWARG_OWNER_CERT: None,
            KWARG_DCA_CERT: None,
            KWARG_DCA_CHAIN: None,
            KWARG_DCA_PK: None,
            KWARG_DCA_PASS: 'MOCKEDPASSWORD',
            KWARG_AGT_PASS: 'ABCD',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME,
        }
        op_kwargs = {
            KWARG_OWNER_CERT: None,
            KWARG_DCA_CERT: None,
            KWARG_DCA_CHAIN: None,
            KWARG_DCA_PK: None,
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: 'MOCKEDPASSWORD',
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        op_kwargs = {
            KWARG_OWNER_CERT: None,
            KWARG_DCA_CERT: None,
            KWARG_DCA_CHAIN: None,
            KWARG_DCA_PK: None,
            KWARG_AGT_PASS: 'MOCKEDPASSWORD',
            KWARG_DCA_PASS: 'MOCKEDPASSWORD',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs, op_kwargs)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')):
                self._valid_test_cases_helper('self signed passphrase with kwargs', test_cases)

    def test_self_signed_with_kw_args_passphrase_args_invalid(self):
        """ This test is for the auto generated self signed certificate flow
            using invalid kwarg input values. Test validates if an appropriate
            exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_DCA_PASS: '1234',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_AGT_PASS: 'ABCD',
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_DCA_PASS: '1234'
        }
        self._add_test_case(test_cases, True, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, True, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_AGT_PASS: 'ABCD'
        }
        self._add_test_case(test_cases, True, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, True, EC.SELFSIGNED_KEY, ip_kwargs)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')):
                self._invalid_test_cases_helper('self signed passphrase with kwargs', test_cases)

    def test_self_signed_with_kw_args_passphrase_args_valid_failed_file_io(self):
        """ This test is for the auto generated self signed certificate flow
            using valid kwarg input values. The test is expected to fail during
            file IO. Test validates if an appropriate exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.SELFSIGNED_KEY, ip_kwargs)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')) as mocked_open:
                mocked_open.side_effect = IOError()
                for test_case in test_cases:
                    ip_force_no_pass = test_case[TEST_IP_KEY][EC.FORCENOPASSWD_KEY]
                    ip_kwargs = test_case[TEST_IP_KEY][TEST_KW_ARGS]
                    with self.assertRaises(edgectl.errors.EdgeFileAccessError):
                        config_object = EdgeCertConfig()
                        config_object.set_options(ip_force_no_pass, None, **ip_kwargs)


    def test_self_signed_certificate_subject_valid(self):
        """ Tests getter and setter for certificate subject.
            This test applicable for the auto generated self signed certificate flow.
        """
        test_cases = []
        subj_dict = {
            EC.SUBJECT_COUNTRY_KEY: 'TC',
            EC.SUBJECT_STATE_KEY: 'Test State',
            EC.SUBJECT_LOCALITY_KEY: 'Test Locality',
            EC.SUBJECT_ORGANIZATION_KEY: 'Test Organization',
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Test Unit',
            EC.SUBJECT_COMMON_NAME_KEY: 'Test CommonName'
        }
        self._add_subj_test_case(test_cases, False, EC.SELFSIGNED_KEY, subj_dict, subj_dict)
        self._valid_test_cases_helper('cert subj', test_cases)

    def test_self_signed_default_certificate_subject_chosen_valid(self):
        """ Tests getter and setter for certificate subject.
            Tests if certificate subject is None that expected defaults are chosen.
            This test applicable for the auto generated self signed certificate flow.
        """
        test_cases = []
        self._add_subj_test_case(test_cases, False, EC.SELFSIGNED_KEY,
                                 None, EdgeDefault.get_certificate_subject_dict())
        self._valid_test_cases_helper('cert subj defaults', test_cases)

    def test_self_signed_certificate_subject_country_field_valid(self):
        """ Tests getter and setter for certificate subject country code.
            Tests if certificate subject country code is always set as upper case.
            This test applicable for the auto generated self signed certificate flow.
        """
        test_cases = []
        subj_dict = {
            EC.SUBJECT_STATE_KEY: 'Test State',
            EC.SUBJECT_LOCALITY_KEY: 'Test Locality',
            EC.SUBJECT_ORGANIZATION_KEY: 'Test Organization',
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Test Unit',
            EC.SUBJECT_COMMON_NAME_KEY: 'Test CommonName'
        }
        # lower case country field get sets with upper case country code
        subj_ip_dict = subj_dict.copy()
        subj_ip_dict[EC.SUBJECT_COUNTRY_KEY] = 'uc'
        subj_op_dict = subj_dict.copy()
        subj_op_dict[EC.SUBJECT_COUNTRY_KEY] = 'UC'
        self._add_subj_test_case(test_cases, False, EC.SELFSIGNED_KEY, subj_ip_dict, subj_op_dict)
        self._valid_test_cases_helper('cert subj country code case', test_cases)

    def test_self_signed_certificate_subject_missing_fields_valid(self):
        """ Tests getter and setter for certificate subject field.
            Tests if certificate subject field is not set then expected defaults are chosen.
            This test applicable for the auto generated self signed certificate flow.
        """
        test_cases = []
        subj_dict = {
            EC.SUBJECT_COUNTRY_KEY: 'TC',
            EC.SUBJECT_STATE_KEY: 'Test State',
            EC.SUBJECT_LOCALITY_KEY: 'Test Locality',
            EC.SUBJECT_ORGANIZATION_KEY: 'Test Organization',
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Test Unit',
            EC.SUBJECT_COMMON_NAME_KEY: 'Test CommonName'
        }
        default_subj_dict = EdgeDefault.get_certificate_subject_dict()

        all_subj_keys = [
            EC.SUBJECT_COUNTRY_KEY,
            EC.SUBJECT_STATE_KEY,
            EC.SUBJECT_LOCALITY_KEY,
            EC.SUBJECT_ORGANIZATION_KEY,
            EC.SUBJECT_ORGANIZATION_UNIT_KEY,
            EC.SUBJECT_COMMON_NAME_KEY
        ]

        for subj_key in all_subj_keys:
            subj_ip_dict = subj_dict.copy()
            del subj_ip_dict[subj_key]
            subj_op_dict = subj_dict.copy()
            subj_op_dict[subj_key] = default_subj_dict[subj_key]
            self._add_subj_test_case(test_cases, False, EC.SELFSIGNED_KEY,
                                     subj_ip_dict, subj_op_dict)
        self._valid_test_cases_helper('cert subj missing field', test_cases)

    def test_self_signed_certificate_subject_invalid(self):
        """ Tests getter and setter for certificate subject field country code
            which has to be 2 letters long.
            This test is for the auto generated self signed certificate flow
            using invalid input values. Test verifies if an appropriate
            exception was raised.
        """
        test_cases = []
        subj_dict = {
            EC.SUBJECT_COUNTRY_KEY: 'BAD COUNTRY CODE',
        }
        self._add_subj_test_case(test_cases, False, EC.SELFSIGNED_KEY, subj_dict, None)
        self._invalid_test_cases_helper('cert subject invalid country code', test_cases)

    ############################################################################
    # Pre installed (Device CA) certificate options (flow) tests
    ############################################################################
    def test_preinstalled_force_no_passphrase_invalid(self):
        """ Tests getter and setter for force no passwords.
            This test is for the preinstalled certificate option
            using invalid input values for force no passwords.
            Test verifies if an appropriate exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
        }
        self._add_test_case(test_cases, None, EC.SELFSIGNED_KEY, ip_kwargs)
        self._add_test_case(test_cases, '', EC.SELFSIGNED_KEY, ip_kwargs)
        self._add_test_case(test_cases, 'blah', EC.SELFSIGNED_KEY, ip_kwargs)
        self._add_test_case(test_cases, 23, EC.SELFSIGNED_KEY, ip_kwargs)
        self._invalid_test_cases_helper('preinstalled force no passphrase', test_cases)

    def test_preinstalled_invalid(self):
        """ This test is for the preinstalled certificate settings
            using invalid input values. Test verifies if an appropriate
            exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            self._invalid_test_cases_helper('preinstall with kwargs', test_cases)

    def test_preinstalled_passphrase_invalid(self):
        """ This test is for the preinstalled certificate settings
            using invalid input values. Test verifies if an appropriate
            exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: '1234',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS: 'ABCD',
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS: 'ABCD'
        }
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')):
                self._invalid_test_cases_helper('preinstall with kwargs passphrase', test_cases)

    def test_preinstalled_valid(self):
        """ Tests getter and setter for preinstalled flow using valid inputs.
            This test uses various inputs to test passphrase and passphrase
            files for the device and agent CA private keys.
        """
        test_cases = []
        # device ca passphrase tests
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: None,
            KWARG_AGT_PASS: None,
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: '1234'
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: None,
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: 'MOCKEDPASSWORD',
            KWARG_AGT_PASS: None,
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        # agent passphrase tests
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS: '1234'
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: None,
            KWARG_AGT_PASS: '1234',
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME,
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: None,
            KWARG_AGT_PASS: 'MOCKEDPASSWORD',
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        # device and agent passphrase tests
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: 'ABCD'
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: '1234',
            KWARG_AGT_PASS: 'ABCD',
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: 'MOCKEDPASSWORD',
            KWARG_AGT_PASS: 'MOCKEDPASSWORD',
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME

        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, op_kwargs)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')):
                self._valid_test_cases_helper('force no pass', test_cases)

    def test_preinstalled_device_ca_cert_file_does_not_exist_returns_true_invalid(self):
        """ Tests getter and setter for device certificate flow using valid inputs.
            This test mocks a file does not exist condition and the expected
            result is that a valid exception is raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME
        }
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, None)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME,
        }
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, None)
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=False)):
            self._invalid_test_cases_helper('check_if_file_exists returns false', test_cases)

    def test_preinstalled_with_kw_args_passphrase_args_valid_failed_file_io(self):
        """ This test is for the preinstalled certificate flow
            using valid kwarg input values. The test is expected to fail during
            file IO. Test validates if an appropriate exception was raised.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS_FILE: DEVICE_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, True, EC.PREINSTALL_KEY, ip_kwargs, None)

        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_AGT_PASS_FILE: AGENT_CA_PASS_FILE_NAME
        }
        self._add_test_case(test_cases, False, EC.PREINSTALL_KEY, ip_kwargs, None)

        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')) as mocked_open:
                mocked_open.side_effect = IOError()
                for test_case in test_cases:
                    ip_force_no_pass = test_case[TEST_IP_KEY][EC.FORCENOPASSWD_KEY]
                    ip_kwargs = test_case[TEST_IP_KEY][TEST_KW_ARGS]
                    with self.assertRaises(edgectl.errors.EdgeFileAccessError):
                        config_object = EdgeCertConfig()
                        config_object.set_options(ip_force_no_pass, None, **ip_kwargs)

    def test_preinstalled_certificate_subject_valid(self):
        """ Tests getter and setter for certificate subject.
            This test applicable for the preinstalled certificate flow.
            Regardless of setting a certificate subject, its getter should
            return None.
        """
        test_cases = []
        ip_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME
        }
        op_kwargs = {
            KWARG_OWNER_CERT: OWNER_CERT_FILE_NAME,
            KWARG_DCA_CERT: DEVICE_CA_CERT_FILE_NAME,
            KWARG_DCA_CHAIN: DEVICE_CA_CHAIN_CERT_FILE_NAME,
            KWARG_DCA_PK: DEVICE_CA_PRIVATE_KEY_FILE_NAME,
            KWARG_DCA_PASS: None,
            KWARG_AGT_PASS: None,
            KWARG_DCA_PASS_FILE: None,
            KWARG_AGT_PASS_FILE: None
        }
        subj_dict = {
            EC.SUBJECT_COUNTRY_KEY: 'TC',
            EC.SUBJECT_STATE_KEY: 'Test State',
            EC.SUBJECT_LOCALITY_KEY: 'Test Locality',
            EC.SUBJECT_ORGANIZATION_KEY: 'Test Organization',
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Test Unit',
            EC.SUBJECT_COMMON_NAME_KEY: 'Test CommonName'
        }
        self._add_subj_test_case(test_cases, False, EC.PREINSTALL_KEY, subj_dict, subj_dict,
                                 ip_kwargs_dict=ip_kwargs, op_kwargs_dict=op_kwargs)
        default_subj_dict = EdgeDefault.get_certificate_subject_dict()
        self._add_subj_test_case(test_cases, False, EC.PREINSTALL_KEY, None, default_subj_dict,
                                 ip_kwargs_dict=ip_kwargs, op_kwargs_dict=op_kwargs)
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            self._valid_test_cases_helper('cert subj', test_cases)

if __name__ == '__main__':
    test_classes = [
        TestEdgeCertConfigPreInitialization,
        TestEdgeCertConfigMethods,
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
