"""Implementation of tests for module `edgectl.utils.certutil.py`."""
from __future__ import print_function
import sys
import unittest
from mock import mock, patch, mock_open, MagicMock
from OpenSSL import crypto
import edgectl.errors
from edgectl.utils import EdgeCertUtil
from edgectl.config import EdgeConstants as EC

if sys.version_info[0] < 3:
    OPEN_BUILTIN = '__builtin__.open'
else:
    OPEN_BUILTIN = 'builtins.open'

VALID_SUBJECT_DICT = {
    EC.SUBJECT_COUNTRY_KEY: 'TC',
    EC.SUBJECT_STATE_KEY: 'Test State',
    EC.SUBJECT_LOCALITY_KEY: 'Test Locality',
    EC.SUBJECT_ORGANIZATION_KEY: 'Test Organization',
    EC.SUBJECT_ORGANIZATION_UNIT_KEY: 'Test Unit',
    EC.SUBJECT_COMMON_NAME_KEY: 'Test CommonName'
}

INVALID_FILE = 'invalid_file'
CA_OWNER_CERT_FILE_NAME = 'test_ca_owner_cert.pem'
CA_CERT_FILE_NAME = 'test_ca_cert.pem'
CA_CHAIN_CERT_FILE_NAME = 'test_ca_chain_cert.pem'
CA_PRIVATE_KEY_FILE_NAME = 'test_ca_private.pem'

# pylint: disable=C0103
# disables invalid method name warning which is triggered because the test names are long
class TestEdgeCertUtilAPIIsValidCertSubject(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.is_valid_certificate_subject"""

    def test_certificate_subject_valid(self):
        """
        Test API validate_certificate_subject returns True when correct inputs are used
        """
        self.assertTrue(EdgeCertUtil.is_valid_certificate_subject(VALID_SUBJECT_DICT))

        string_val_64 = 'a' * 64
        string_val_128 = 'a' * 128
        valid_lengths_dict = {
            EC.SUBJECT_COUNTRY_KEY: ['AB'],
            EC.SUBJECT_STATE_KEY: ['', string_val_128],
            EC.SUBJECT_LOCALITY_KEY: ['', string_val_128],
            EC.SUBJECT_ORGANIZATION_KEY: ['', string_val_64],
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: ['', string_val_64],
            EC.SUBJECT_COMMON_NAME_KEY: [string_val_64],
        }
        for key in list(VALID_SUBJECT_DICT.keys()):
            test_dict = VALID_SUBJECT_DICT.copy()
            for test_case in list(valid_lengths_dict[key]):
                test_dict[key] = test_case
                self.assertTrue(EdgeCertUtil.is_valid_certificate_subject(test_dict), key)

    def test_certificate_subject_invalid(self):
        """
        Test API validate_certificate_subject returns False when incorrect inputs are used
        """
        # delete keys from dict
        for key in list(VALID_SUBJECT_DICT.keys()):
            test_dict = VALID_SUBJECT_DICT.copy()
            del test_dict[key]
            self.assertFalse(EdgeCertUtil.is_valid_certificate_subject(test_dict), key)

        # test with invalid values
        string_val_65 = 'a' * 65
        string_val_129 = 'a' * 129
        invalid_lengths_dict = {
            EC.SUBJECT_COUNTRY_KEY: [None, '', 'A', 'ABC'],
            EC.SUBJECT_STATE_KEY: [None, string_val_129],
            EC.SUBJECT_LOCALITY_KEY: [None, string_val_129],
            EC.SUBJECT_ORGANIZATION_KEY: [None, string_val_65],
            EC.SUBJECT_ORGANIZATION_UNIT_KEY: [None, string_val_65],
            EC.SUBJECT_COMMON_NAME_KEY: [None, '', string_val_65],
        }
        for key in list(VALID_SUBJECT_DICT.keys()):
            test_dict = VALID_SUBJECT_DICT.copy()
            for test_case in list(invalid_lengths_dict[key]):
                test_dict[key] = test_case
                self.assertFalse(EdgeCertUtil.is_valid_certificate_subject(test_dict), key)

class TestEdgeCertUtilAPICreateRootCACert(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.create_root_ca_cert"""

    def test_create_root_ca_cert_duplicate_ids_invalid(self):
        """
        Test API create_root_ca_cert raises exception when duplicate id's are used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)

    def test_create_root_ca_cert_validity_days_invalid(self):
        """
        Test API create_root_ca_cert raises exception when invalid validity day values are used
        """
        cert_util = EdgeCertUtil()
        for validity in [-1, 0, 1096]:
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.create_root_ca_cert('root',
                                              subject_dict=VALID_SUBJECT_DICT,
                                              validity_days_from_now=validity)

    def test_create_root_ca_cert_subject_dict_invalid(self):
        """
        Test API create_root_ca_cert raises exception when invalid cert dicts are used
        """
        cert_util = EdgeCertUtil()
        with patch('edgectl.utils.EdgeCertUtil.is_valid_certificate_subject',
                   MagicMock(return_value=False)):
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.create_root_ca_cert('root',
                                              subject_dict=VALID_SUBJECT_DICT)

    def test_create_root_ca_cert_passphrase_invalid(self):
        """
        Test API set_ca_cert raises exception when passphrase is invalid
        """
        cert_util = EdgeCertUtil()
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_root_ca_cert('root',
                                          subject_dict=VALID_SUBJECT_DICT,
                                          passphrase='')
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_root_ca_cert('root',
                                          subject_dict=VALID_SUBJECT_DICT,
                                          passphrase='123')
        bad_pass_1024 = 'a' * 1024
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_root_ca_cert('root',
                                          subject_dict=VALID_SUBJECT_DICT,
                                          passphrase=bad_pass_1024)

class TestEdgeCertUtilAPISetCACert(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.set_ca_cert"""

    def test_set_ca_cert_missing_args_invalid(self):
        """
        Test API set_ca_cert raises exception when all required args are not provided
        """
        cert_util = EdgeCertUtil()
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME)
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKEDPASSWORD')) as mocked_open:
                mocked_open.side_effect = IOError()

    @staticmethod
    def _check_if_file_exists_helper(file_name):
        if file_name == INVALID_FILE:
            return False
        return True

    def test_set_ca_cert_missing_cert_files_invalid(self):
        """
        Test API set_ca_cert raises exception when files found to not exist
        """
        cert_util = EdgeCertUtil()
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists') as mock_check_file:
            mock_check_file.side_effect = self._check_if_file_exists_helper
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=INVALID_FILE,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=INVALID_FILE,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=INVALID_FILE,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME)
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=INVALID_FILE)

    def test_set_ca_cert_passphrase_invalid(self):
        """
        Test API set_ca_cert raises exception when passphrase is invalid
        """
        cert_util = EdgeCertUtil()
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='')
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='123')
            bad_pass_1024 = 'a' * 1024
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase=bad_pass_1024)

    def test_set_ca_cert_open_failure_invalid(self):
        """
        Test API set_ca_cert raises exception when open() cert private key file fails
        """
        cert_util = EdgeCertUtil()
        with patch('edgectl.utils.EdgeUtils.check_if_file_exists', MagicMock(return_value=True)):
            with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')) as mocked_open:
                mocked_open.side_effect = IOError()
                with self.assertRaises(edgectl.errors.EdgeFileAccessError):
                    cert_util.set_ca_cert('root',
                                          ca_cert_file_path=CA_CERT_FILE_NAME,
                                          ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                          ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                          ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                          passphrase='1234')
                mocked_open.assert_called_with(CA_PRIVATE_KEY_FILE_NAME, 'rb')

    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_load_privatekey_failure_invalid(self, mock_util_chk, mock_load_pk):
        """
        Test API set_ca_cert raises exception when calling API load_privatekey
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')) as mocked_open:
            mock_load_pk.side_effect = crypto.Error()
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mocked_open.assert_called_with(CA_PRIVATE_KEY_FILE_NAME, 'rb')
            mock_load_pk.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED', passphrase='1234')

    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_check_type_error_invalid(self, mock_util_chk, mock_load_pk, mock_check_pk):
        """
        Test API set_ca_cert raises exception when private key check fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')) as mocked_open:
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.side_effect = TypeError()
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mocked_open.assert_called_with(CA_PRIVATE_KEY_FILE_NAME, 'rb')
            mock_load_pk.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED', passphrase='1234')

    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_check_crypto_error_invalid(self, mock_util_chk,
                                                    mock_load_pk, mock_check_pk):
        """
        Test API set_ca_cert raises exception when private key check fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')) as mocked_open:
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.side_effect = crypto.Error()
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mocked_open.assert_called_with(CA_PRIVATE_KEY_FILE_NAME, 'rb')
            mock_load_pk.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED', passphrase='1234')

    @mock.patch('OpenSSL.crypto.load_certificate')
    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_load_cert_failure_invalid(self, mock_util_chk, mock_load_pk,
                                                   mock_check_pk, mock_load_cert):
        """
        Test API set_ca_cert raises exception when loading certificate fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')):
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.return_value = True
            mock_load_cert.side_effect = crypto.Error()
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mock_load_cert.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED')

    @mock.patch('OpenSSL.crypto.load_certificate')
    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_load_cert_io_failure_invalid(self, mock_util_chk, mock_load_pk,
                                                      mock_check_pk, mock_load_cert):
        """
        Test API set_ca_cert raises exception when loading certificate fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')):
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.return_value = True
            mock_load_cert.side_effect = IOError()
            with self.assertRaises(edgectl.errors.EdgeFileAccessError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mock_load_cert.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED')

    # pylint: disable=R0913
    # disabling too many arguments warning
    @mock.patch('OpenSSL.crypto.X509.has_expired')
    @mock.patch('OpenSSL.crypto.load_certificate')
    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_load_expired_cert_invalid(self, mock_util_chk, mock_load_pk,
                                                   mock_check_pk, mock_load_cert, mock_expired):
        """
        Test API set_ca_cert raises exception when loading certificate fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')):
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.return_value = True
            mock_load_cert.return_value = crypto.X509()
            mock_expired.return_value = True
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')
            mock_load_cert.assert_called_with(crypto.FILETYPE_PEM, 'MOCKED')

    # pylint: disable=R0913
    # disabling too many arguments warning
    @mock.patch('OpenSSL.crypto.X509.has_expired')
    @mock.patch('OpenSSL.crypto.load_certificate')
    @mock.patch('OpenSSL.crypto.PKey.check')
    @mock.patch('OpenSSL.crypto.load_privatekey')
    @mock.patch('edgectl.utils.EdgeUtils.check_if_file_exists')
    def test_set_ca_cert_duplicate_id_invalid(self, mock_util_chk, mock_load_pk,
                                              mock_check_pk, mock_load_cert, mock_expired):
        """
        Test API set_ca_cert raises exception when loading certificate fails
        """
        cert_util = EdgeCertUtil()
        mock_util_chk.return_value = True
        with patch(OPEN_BUILTIN, mock_open(read_data='MOCKED')):
            mock_load_pk.return_value = crypto.PKey()
            mock_check_pk.return_value = True
            mock_load_cert.return_value = crypto.X509()
            mock_expired.return_value = False
            cert_util.set_ca_cert('root',
                                  ca_cert_file_path=CA_CERT_FILE_NAME,
                                  ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                  ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                  ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                  passphrase='1234')
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.set_ca_cert('root',
                                      ca_cert_file_path=CA_CERT_FILE_NAME,
                                      ca_root_cert_file_path=CA_OWNER_CERT_FILE_NAME,
                                      ca_root_chain_cert_file_path=CA_CHAIN_CERT_FILE_NAME,
                                      ca_private_key_file_path=CA_PRIVATE_KEY_FILE_NAME,
                                      passphrase='1234')

class TestEdgeCertUtilAPICreateIntCACert(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.create_intermediate_ca_cert"""

    def test_create_intermediate_ca_cert_duplicate_ids_invalid(self):
        """
        Test API create_intermediate_ca_cert raises exception when invalid validity day values used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('root', 'root', common_name='name')

    def test_create_intermediate_ca_cert_validity_days_invalid(self):
        """
        Test API create_intermediate_ca_cert raises exception when invalid validity day values used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        for validity in [-1, 0, 1096]:
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.create_intermediate_ca_cert('int', 'root', common_name='name',
                                                      validity_days_from_now=validity)

    def test_create_intermediate_ca_cert_passphrase_invalid(self):
        """
        Test API create_intermediate_ca_cert raises exception when passphrase is invalid
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name='name',
                                                  passphrase='')

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name='name',
                                                  passphrase='123')

        bad_pass_1024 = 'a' * 1024
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name='name',
                                                  passphrase=bad_pass_1024)

    def test_create_intermediate_ca_cert_common_name_invalid(self):
        """
        Test API create_intermediate_ca_cert raises exception when common name is invalid
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root')

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name=None)

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name='')

        bad_common_name = 'a' * 65
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_intermediate_ca_cert('int', 'root', common_name=bad_common_name)

class TestEdgeCertUtilAPICreateServerCert(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.create_server_cert"""

    def test_create_server_cert_duplicate_ids_invalid(self):
        """
        Test API create_server_cert raises exception when invalid validity day values used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('root', 'root', host_name='name')

    def test_create_server_cert_validity_days_invalid(self):
        """
        Test API create_server_cert raises exception when invalid validity day values used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        for validity in [-1, 0, 1096]:
            with self.assertRaises(edgectl.errors.EdgeValueError):
                cert_util.create_server_cert('server', 'root', host_name='name',
                                             validity_days_from_now=validity)

    def test_create_server_cert_passphrase_invalid(self):
        """
        Test API create_server_cert raises exception when passphrase is invalid
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('server', 'root', host_name='name', passphrase='')

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('server', 'root', host_name='name', passphrase='123')

        bad_pass = 'a' * 1024
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('server', 'root', host_name='name', passphrase=bad_pass)

    def test_create_server_cert_hostname_invalid(self):
        """
        Test API create_server_cert raises exception when hostname is invalid
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('int', 'root')

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('int', 'root', host_name=None)

        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('int', 'root', host_name='')

        bad_hostname = 'a' * 65
        with self.assertRaises(edgectl.errors.EdgeValueError):
            cert_util.create_server_cert('int', 'root', host_name=bad_hostname)

class TestEdgeCertUtilAPIExportCertArtifacts(unittest.TestCase):
    """Unit tests for API EdgeCertUtil.export_cert_artifacts_to_dir"""

    @mock.patch('edgectl.utils.EdgeUtils.check_if_directory_exists')
    def test_export_cert_artifacts_to_dir_incorrect_id_invalid(self, mock_chk_dir):
        """
        Test API export_cert_artifacts_to_dir raises exception when invalid id used
        """
        cert_util = EdgeCertUtil()
        with self.assertRaises(edgectl.errors.EdgeValueError):
            mock_chk_dir.return_value = True
            cert_util.export_cert_artifacts_to_dir('root', 'some_dir')

    @mock.patch('edgectl.utils.EdgeUtils.check_if_directory_exists')
    def test_export_cert_artifacts_to_dir_invalid_dir_invalid(self, mock_chk_dir):
        """
        Test API export_cert_artifacts_to_dir raises exception when invalid id used
        """
        cert_util = EdgeCertUtil()
        cert_util.create_root_ca_cert('root', subject_dict=VALID_SUBJECT_DICT)
        with self.assertRaises(edgectl.errors.EdgeValueError):
            mock_chk_dir.return_value = False
            cert_util.export_cert_artifacts_to_dir('root', 'some_dir')

if __name__ == '__main__':
    test_classes = [
        TestEdgeCertUtilAPIIsValidCertSubject,
        TestEdgeCertUtilAPICreateRootCACert,
        TestEdgeCertUtilAPISetCACert,
        TestEdgeCertUtilAPICreateIntCACert,
        TestEdgeCertUtilAPICreateServerCert,
        TestEdgeCertUtilAPIExportCertArtifacts,
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
