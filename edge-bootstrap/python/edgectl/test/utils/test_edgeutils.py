"""Implementation of tests for module `edgectl.utils.edgeutils.py`."""
from __future__ import print_function
import errno
import re
import stat
import unittest
import mock
from edgectl.utils import EdgeUtils


# pylint: disable=C0103
# disables invalid method name warning which is triggered because the test names are long
# pylint: disable=R0201
# disables Method could be a function (no-self-use)
# pylint: disable=W0212
# disables Access to a protected member
# pylint: disable=R0904
# disables Too many public methods
class TestEdgeUtilAPIs(unittest.TestCase):
    """Unit tests for EdgeUtils APIs"""

    @mock.patch('shutil.rmtree')
    @mock.patch('os.path.exists')
    def test_delete_dir_when_dir_exists(self, mock_exists, mock_rmtree):
        """ Test a valid invocation of API delete_dir when dir to be deleted exists"""
        # arrange
        dir_path = 'blah'
        mock_exists.return_value = True

        # act
        EdgeUtils.delete_dir(dir_path)

        # assert
        mock_exists.assert_called_with(dir_path)
        mock_rmtree.assert_called_with(dir_path, onerror=EdgeUtils._remove_readonly_callback)

    @mock.patch('os.unlink')
    @mock.patch('os.chmod')
    def test_delete_dir_execute_onerror_callback(self, mock_chmod, mock_unlink):
        """ Test rmtree onerror callback invocation"""
        # arrange
        dir_path = 'blah'
        ignored = 0

        # act
        EdgeUtils._remove_readonly_callback(ignored, dir_path, ignored)

        # assert
        mock_chmod.assert_called_with(dir_path, stat.S_IWRITE)
        mock_unlink.assert_called_with(dir_path)

    @mock.patch('shutil.rmtree')
    @mock.patch('os.path.exists')
    def test_delete_dir_when_dir_does_not_exist(self, mock_exists, mock_rmtree):
        """ Test a valid invocation of API delete_dir when dir to be deleted does not exist"""
        # arrange
        dir_path = 'blah'
        mock_exists.return_value = False

        # act
        EdgeUtils.delete_dir(dir_path)

        # assert
        mock_exists.assert_called_with(dir_path)
        mock_rmtree.assert_not_called()

    @mock.patch('shutil.rmtree')
    @mock.patch('os.path.exists')
    def test_delete_dir_raises_oserror_when_rmtree_fails(self, mock_exists, mock_rmtree):
        """ Tests invocation of API delete_dir raises OSError when rmtree raises OSError"""
        # arrange
        dir_path = 'blah'
        mock_exists.return_value = True
        mock_rmtree.side_effect = OSError('rmtree error')

        # act, assert
        with self.assertRaises(OSError):
            EdgeUtils.delete_dir(dir_path)

    @mock.patch('os.makedirs')
    def test_mkdir_if_needed_when_dir_does_not_exist(self, mock_mkdirs):
        """ Test a valid invocation of API mkdir_if_needed when dir to be made does not exist """
        # arrange
        dir_path = 'blah'

        # act
        EdgeUtils.mkdir_if_needed(dir_path)

        # assert
        mock_mkdirs.assert_called_with(dir_path)

    @mock.patch('os.makedirs')
    def test_mkdir_if_needed_when_dir_exists(self, mock_mkdirs):
        """ Test a valid invocation of API mkdir_if_needed when dir to be made already exists """
        # arrange
        dir_path = 'blah'
        mock_mkdirs.side_effect = OSError(errno.EEXIST, 'Directory exists.')

        # act
        EdgeUtils.mkdir_if_needed(dir_path)

        # assert
        mock_mkdirs.assert_called_with(dir_path)

    @mock.patch('os.makedirs')
    def test_mkdir_if_needed_raises_oserror_when_mkdir_fails(self, mock_mkdirs):
        """ Tests invocation of API mkdir_if_needed raises OSError when makedirs raises OSError"""
        # arrange
        dir_path = 'blah'
        mock_mkdirs.side_effect = OSError(errno.EACCES, 'Directory permission error')

        # act, assert
        with self.assertRaises(OSError):
            EdgeUtils.mkdir_if_needed(dir_path)

    @mock.patch('os.path.isfile')
    @mock.patch('os.path.exists')
    def test_check_if_file_exists_returns_true(self, mock_exists, mock_isfile):
        """ Test a valid invocation of API check_if_file_exists """
        # arrange #1
        file_path = 'blah'
        mock_exists.return_value = True
        mock_isfile.return_value = True

        # act
        result = EdgeUtils.check_if_file_exists(file_path)

        # assert
        mock_exists.assert_called_with(file_path)
        mock_isfile.assert_called_with(file_path)
        self.assertTrue(result)

    @mock.patch('os.path.isfile')
    @mock.patch('os.path.exists')
    def test_check_if_file_exists_returns_false_if_exists_returns_false(self,
                                                                        mock_exists, mock_isfile):
        """ Test a valid invocation of API check_if_file_exists """

        # arrange
        file_path = 'blah'
        mock_exists.return_value = False

        # act
        result = EdgeUtils.check_if_file_exists(file_path)

        # assert
        mock_exists.assert_called_with(file_path)
        mock_isfile.assert_not_called()
        self.assertFalse(result)

    @mock.patch('os.path.isfile')
    @mock.patch('os.path.exists')
    def test_check_if_file_exists_returns_false_if_isfile_returns_false(self,
                                                                        mock_exists, mock_isfile):
        """ Test a valid invocation of API check_if_file_exists """

        # arrange
        file_path = 'blah'
        mock_exists.return_value = True
        mock_isfile.return_value = False

        # act
        result = EdgeUtils.check_if_file_exists(file_path)

        # assert
        mock_exists.assert_called_with(file_path)
        mock_isfile.assert_called_with(file_path)
        self.assertFalse(result)

    @mock.patch('os.path.isfile')
    @mock.patch('os.path.exists')
    def test_check_if_file_exists_returns_false_path_is_none(self, mock_exists, mock_isfile):
        """ Test a valid invocation of API check_if_file_exists """

        # arrange
        file_path = None

        # act
        result = EdgeUtils.check_if_file_exists(file_path)

        # assert
        mock_exists.assert_not_called()
        mock_isfile.assert_not_called()
        self.assertFalse(result)

    @mock.patch('os.path.isdir')
    @mock.patch('os.path.exists')
    def test_check_if_dir_exists_returns_true(self, mock_exists, mock_isdir):
        """ Test a valid invocation of API check_if_directory_exists """
        # arrange #1
        dir_path = 'blah'
        mock_exists.return_value = True
        mock_isdir.return_value = True

        # act
        result = EdgeUtils.check_if_directory_exists(dir_path)

        # assert
        mock_exists.assert_called_with(dir_path)
        mock_isdir.assert_called_with(dir_path)
        self.assertTrue(result)

    @mock.patch('os.path.isdir')
    @mock.patch('os.path.exists')
    def test_check_if_dir_exists_returns_false_if_exists_returns_false(self,
                                                                       mock_exists, mock_isdir):
        """ Test a valid invocation of API check_if_directory_exists """

        # arrange
        dir_path = 'blah'
        mock_exists.return_value = False

        # act
        result = EdgeUtils.check_if_directory_exists(dir_path)

        # assert
        mock_exists.assert_called_with(dir_path)
        mock_isdir.assert_not_called()
        self.assertFalse(result)

    @mock.patch('os.path.isdir')
    @mock.patch('os.path.exists')
    def test_check_if_dir_exists_returns_false_if_isdir_returns_false(self,
                                                                      mock_exists, mock_isdir):
        """ Test a valid invocation of API check_if_directory_exists """

        # arrange
        dir_path = 'blah'
        mock_exists.return_value = True
        mock_isdir.return_value = False

        # act
        result = EdgeUtils.check_if_directory_exists(dir_path)

        # assert
        mock_exists.assert_called_with(dir_path)
        mock_isdir.assert_called_with(dir_path)
        self.assertFalse(result)

    @mock.patch('os.path.isdir')
    @mock.patch('os.path.exists')
    def test_check_if_dir_exists_returns_false_path_is_none(self, mock_exists, mock_isdir):
        """ Test a valid invocation of API check_if_directory_exists """

        # arrange
        dir_path = None

        # act
        result = EdgeUtils.check_if_directory_exists(dir_path)

        # assert
        mock_exists.assert_not_called()
        mock_isdir.assert_not_called()
        self.assertFalse(result)

    @mock.patch('shutil.copy2')
    def test_copy_files_valid(self, mock_copy):
        """ Test a valid invocation of API copy_files """
        # arrange
        src_path = 'src'
        dest_path = 'dest'

        # act
        EdgeUtils.copy_files(src_path, dest_path)

        # assert
        mock_copy.assert_called_with(src_path, dest_path)

    @mock.patch('shutil.copy2')
    def test_copy_files_raises_oserror_when_cop2_raises_oserror(self, mock_copy):
        """ Tests invocation of API copy_files raises OSError when copy2 raises OSError"""
        # arrange
        src_path = 'src'
        dest_path = 'dest'
        mock_copy.side_effect = OSError('copy2 OS error')

        # act, assert
        with self.assertRaises(OSError):
            EdgeUtils.copy_files(src_path, dest_path)

    @mock.patch('socket.getfqdn')
    def test_get_hostname_valid(self, mock_hostname):
        """ Test a valid invocation of API get_hostname """
        # arrange
        hostname = 'test_hostname'
        mock_hostname.return_value = hostname
        # act
        result = EdgeUtils.get_hostname()

        # assert
        mock_hostname.assert_called_with()
        self.assertEqual(hostname, result)

    @mock.patch('socket.getfqdn')
    def test_get_hostname_raises_ioerror_when_getfqdn_raises_ioerror(self, mock_hostname):
        """ Tests invocation of API get_hostname raises IOError when getfqdn raises IOError"""
        # arrange
        mock_hostname.side_effect = IOError('getfqdn IO error')

        # act, assert
        with self.assertRaises(IOError):
            EdgeUtils.get_hostname()

    def test_sanitize_registry(self):
        """ Test a valid invocation of API sanitize_registry_data """
        result = EdgeUtils.sanitize_registry_data('test_address', 'test_username', 'test_password')
        pattern = re.compile(r'^Address: test_address, Username: test_username, Password:[\*]+$')
        self.assertTrue(pattern.match(result))

    def test_sanitize_connection_string(self):
        """ Test a valid invocation of API sanitize_connection_string """
        result = EdgeUtils.sanitize_connection_string('HostName=aa;DeviceId=bb;SharedAccessKey=cc')
        pattern = re.compile(r'^HostName=aa;DeviceId=bb;SharedAccessKey=[\*]+$')
        self.assertTrue(pattern.match(result))

        result = EdgeUtils.sanitize_connection_string('HostName=aa;DeviceId=bb;sharedaccesskey=cc')
        pattern = re.compile(r'^HostName=aa;DeviceId=bb;sharedaccesskey=[\*]+$')
        self.assertTrue(pattern.match(result))

        result = EdgeUtils.sanitize_connection_string('HostName=aaa;DeviceId=bbb')
        pattern = re.compile(r'^HostName=aaa;DeviceId=bbb+$')
        self.assertTrue(pattern.match(result))

    @mock.patch('getpass.getpass')
    def test_prompt_password(self, mock_getpass):
        """ Test a valid invocation of API prompt_password """
        valid_pass = 'aaaaa'
        mock_getpass.return_value = valid_pass
        result = EdgeUtils.prompt_password('test password', 5, 8)
        self.assertTrue(valid_pass, result)

if __name__ == '__main__':
    test_classes = [
        TestEdgeUtilAPIs,
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
