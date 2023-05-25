using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;

namespace BackendTestProject
{
    public class DataAccessHandlerTest
    {
        private readonly IDataAccessHandler _sut;
        private readonly Mock<IDataAccessHandler> _DataAccessHandlerMock;
        public DataAccessHandlerTest()
        {
            _sut = new DataAccessHandler();
        }

        [Fact]

        public void GetTaskFromID_NoTasks_ExpectNull()
        {
            var testGuid = Guid.NewGuid();

            var result = _sut.GetTaskFromId(testGuid);
            if (result == null)
                Assert.True(true);
            else
                Assert.True(false);
        }

        [Fact]
        public void CreateNewClient_ClientKnown_ExpectClientFound()
        {
            ClientDTO expected = new ClientDTO(Guid.NewGuid(), "Morgan Freeman", "myServer", "myDatServer", "","","");

            _sut.AddClientToClientDHT(expected);
            var actual = _sut.FindClientByUserName(expected.Username);

            _sut.DeleteClientFromClientDHT(expected.ClientId);

            Assert.Equal(actual.ClientId, expected.ClientId);
            Assert.Equal(actual.Username, expected.Username);
            Assert.Equal(actual.ServerName, expected.ServerName);
            Assert.Equal(actual.DataServer, expected.DataServer);
        }

        public void UpdateClient_ClientKnown_ExpectClientFound()
        {

            ClientDTO expected = new ClientDTO(Guid.NewGuid(), "Taylor swift", "myServer", "myDatServer","","","");
            _sut.AddClientToClientDHT(expected);
            var actual = _sut.FindClientByUserName(expected.Username);

            _sut.DeleteClientFromClientDHT(expected.ClientId);

            Assert.Equal(expected.ClientId, actual.ClientId);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.ServerName, actual.ServerName);
            Assert.Equal(expected.DataServer, actual.DataServer);

        }

        [Fact]
        public void GetTaskForClient_ClientKnow_ExspectTaskFound()
        {

            var clientID = Guid.NewGuid();
            ClientDTO client = new ClientDTO(clientID, "test", "myServer", "myDatServer","","","");
            ServiceTaskDTO task = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testjob",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Completed,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(task);
            var result = _sut.GetTaskForClient(clientID);

            Assert.True(result.Any());
        }
        [Fact]
        public void GetTaskFromStatus_StatusCompleted_ExpectTaskFound()
        {
            var clientID = Guid.NewGuid();

            ServiceTaskDTO taskCompleted = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testjob",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Completed,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(taskCompleted);

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Completed);
            if (result.Exists(t => t.Id == taskCompleted.Id))
            {
                Assert.True(true);
            }
            else
                Assert.True(false);

        }

        [Fact]
        public void GetTaskFromStatus_StatusFailed_ExpectTaskFound()
        {
            var clientID = Guid.NewGuid();

            ServiceTaskDTO taskFailed = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testjob",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Failed,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(taskFailed);

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Failed);
            if (result.Exists(t => t.Id == taskFailed.Id))
            {
                Assert.True(true);
            }
            else
                Assert.True(false);
        }
        [Fact]
        public void GetTaskFromStatus_StatusCancel_ExpectTaskFound()
        {
            var clientID = Guid.NewGuid();

            ServiceTaskDTO taskCancel = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testJobCancel",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Cancel,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(taskCancel);

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Cancel);

            if (result.Exists(t => t.Id == taskCancel.Id))
            {
                Assert.True(true);
            }
            else
                Assert.True(false);
        }
        [Fact]
        public void GetTaskFromStatus_StatusStarting_ExpectTaskFound()
        {
            var clientID = Guid.NewGuid();

            ServiceTaskDTO taskStarting = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testjob",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Starting,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(taskStarting);

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Starting);
            if (result.Exists(t => t.Id == taskStarting.Id))
            {
                Assert.True(true);
            }
            else
                Assert.True(false);
        }
        [Fact]
        public void GetTaskFromStatus_StatusCreated_ExpectTaskNotFound()
        {

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Completed);
            if (result.Exists(t => t.Status == ServiceTaskStatus.Created))
            {
                Assert.True(false);
            }
            else
                Assert.True(true);
        }

        [Fact]
        public void GetTaskFromStatus_StatusInProgress_ExpectTaskNotFound()
        {

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.InProgress);
            if (result.Exists(t => t.Status == ServiceTaskStatus.Created))
            {
                Assert.True(false);
            }
            else
                Assert.True(true);
        }
        [Fact]
        public void UpdateTask_TaskCreated_ExpectUpdatedTaskFound()
        {
            var clientID = Guid.NewGuid();

            ServiceTaskDTO taskUpdateTest = new ServiceTaskDTO(Guid.NewGuid(),
                                                clientID,
                                                "testjobOld",
                                                "",
                                                DateTime.UtcNow,
                                                DateTime.UtcNow,
                                                ServiceTaskStatus.Starting,
                                                "/testJob",
                                                "/testBackup",
                                                "/testResult");

            _sut.AddTask(taskUpdateTest);

            taskUpdateTest.Name = "testUpdatedJob";

            _sut.UpdateTask(taskUpdateTest);

            var result = _sut.GetTasksFromStatus(ServiceTaskStatus.Starting);
            if (result.Exists(t => t.Name == "testUpdatedJob"))
            {
                Assert.True(true);
            }
            else
                Assert.True(false);
        }
    }
}
