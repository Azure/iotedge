"""Implementation of tests for module `edgectl.deployment.deploymentdocker.py`."""
from __future__ import print_function
import platform
import time
import unittest
import edgectl.errors
from edgectl.host.dockerclient import EdgeDockerClient

class TestEdgeDockerClientSmoke(unittest.TestCase):
    IMAGE_NAME = 'nginx:latest'
    NETWORK_NAME = 'ctl_int_test_network'
    CONTAINER_NAME = 'ctl_int_test_container'
    VOLUME_NAME = 'ctl_int_int_test_mnt'
    LABEL_NAME = 'ctl_int_test_label'

    """Test for API EdgeDockerClient.check_availability"""
    def test_availability(self):
        """
            Tests call stack when API check_availability returns true
        """
        with EdgeDockerClient() as client:
            result = client.check_availability()
            self.assertTrue(result)

    def test_get_os_type(self):
        """Test for API EdgeDockerClient.get_os_type"""
        with EdgeDockerClient() as client:
            exception_raised = False
            try:
                os_type = client.get_os_type()
            except edgectl.errors.EdgeDeploymentError:
                exception_raised = True
            self.assertFalse(exception_raised)
            permitted_os_types = ['windows', 'linux']
            self.assertIn(os_type, permitted_os_types)

    def test_pull(self):
        """Test for API EdgeDockerClient.pull and EdgeDockerClient.get_local_image_sha_id"""
        with EdgeDockerClient() as client:
            exception_raised = False
            image_name = self.IMAGE_NAME
            try:
                local_sha_1 = client.get_local_image_sha_id(image_name)
                client.pull(image_name, None, None)
                local_sha_2 = client.get_local_image_sha_id(image_name)
                if local_sha_1 is None:
                    client.pull(image_name, None, None)
                    local_sha_1 = client.get_local_image_sha_id(image_name)
            except edgectl.errors.EdgeDeploymentError:
                exception_raised = True
            self.assertFalse(exception_raised)
            self.assertEqual(local_sha_1, local_sha_2)

    def _create_container(self, client):
        image_name = self.IMAGE_NAME
        os_type = client.get_os_type().lower()
        if os_type == 'linux':
            volume_path = '/{0}'.format(self.VOLUME_NAME)
        elif os_type == 'windows':
            volume_path = 'c:/{0}'.format(self.VOLUME_NAME)
        volume_dict = {}
        volume_dict[self.VOLUME_NAME] = {'bind': volume_path, 'mode': 'rw'}
        env_dict = {}
        env_dict['TEST_VOLUME_NAME'] = self.VOLUME_NAME
        client.pull(image_name, None, None)
        client.create_network(self.NETWORK_NAME)
        client.create_volume(self.VOLUME_NAME)
        client.create(image_name,
                      detach=True,
                      name=self.CONTAINER_NAME,
                      network=self.NETWORK_NAME,
                      volumes=volume_dict,
                      environment=env_dict)
        client.copy_file_to_volume(self.CONTAINER_NAME,
                                   'test_file_name.txt',
                                   volume_path,
                                   __file__)

    def _destroy_container(self, client):
        client.stop(self.CONTAINER_NAME)
        status = client.status(self.CONTAINER_NAME)
        self.assertEqual('exited', status)
        client.remove(self.CONTAINER_NAME)
        status = client.status(self.CONTAINER_NAME)
        self.assertIsNone(status)
        client.remove_volume(self.VOLUME_NAME)
        client.destroy_network(self.NETWORK_NAME)

    def test_create(self):
        """
            Tests container create and destroy
        """
        with EdgeDockerClient() as client:
            exception_raised = False
            try:
                status = client.status(self.CONTAINER_NAME)
                if status is not None:
                    self._destroy_container(client)
                self._create_container(client)
                status = client.status(self.CONTAINER_NAME)
                self.assertEqual('created', status)
                client.start(self.CONTAINER_NAME)
                status = client.status(self.CONTAINER_NAME)
                self.assertEqual('running', status)
                time.sleep(5)
                self._destroy_container(client)
            except edgectl.errors.EdgeDeploymentError as ex:
                print(ex)
                exception_raised = True
            self.assertFalse(exception_raised)

if __name__ == '__main__':
    test_classes = [
        TestEdgeDockerClientSmoke,
    ]
    suites_list = []
    for test_class in test_classes:
        suite = unittest.TestLoader().loadTestsFromTestCase(test_class)
        suites_list.append(suite)
    SUITE = unittest.TestSuite(suites_list)
    unittest.TextTestRunner(verbosity=2).run(SUITE)
