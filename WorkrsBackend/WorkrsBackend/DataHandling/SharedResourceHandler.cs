﻿using WorkrsBackend.RabbitMQ;
using WorkrsBackend.DTOs;
using System.Text.Json;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using WorkrsBackend.Config;
using System.Linq;
using Serilog;

namespace WorkrsBackend.DataHandling
{
    public class SharedResourceHandler : ISharedResourceHandler
    {
        int serverCnt = 1;
        bool _lockClient = false;
        bool _lockWorker = false;
        bool _lockServer = false;
        public Dictionary<Guid, ClientDTO> _clientsDHT = new();
        public Dictionary<Guid, WorkerDTO> _workersDHT = new();
        public Dictionary<string, ServerDTO> _serverDHT = new();
        private ReaderWriterLockSlim _lockClientDHT = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim _lockWorkerDHT = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim _lockServerDHT = new ReaderWriterLockSlim();
        private ReaderWriterLockSlim _lockChangeQueue = new ReaderWriterLockSlim();
        IDataAccessHandler _dataAccessHandler;
        IServerConfig _serverConfig;
        IRabbitMQHandler _comHandler;
        List<Guid> _lockClientIds = new List<Guid>();
        List<Guid> _lockWorkerIds = new List<Guid>();
        List<Guid> _lockServerIds = new List<Guid>();
        Dictionary<Guid, SharedHashTableChangeDTO> _changeQueue = new Dictionary<Guid, SharedHashTableChangeDTO>();
        Dictionary<Guid, int> _changeQueueCount = new Dictionary<Guid, int>();

        public SharedResourceHandler(IDataAccessHandler dataAccessHandler, IRabbitMQHandler rabbitMQHandler, IServerConfig serverConfig)
        {
            _dataAccessHandler = dataAccessHandler;
            _comHandler = rabbitMQHandler;
            _serverConfig = serverConfig;

            _clientsDHT = _dataAccessHandler.GetClientDHT();
            _workersDHT = _dataAccessHandler.GetWorkerDHT();
            _serverDHT = _dataAccessHandler.GetPrimaryServers();
            Init();
        }

        void Init()
        {
            _comHandler.CreateMulticastChanges(HandleChange);
            _comHandler.CreateMulticastLockRequest(HandleRequestLock);
            _comHandler.CreateMulticastLockReply(HandleRequestLockReply);

            if(!_serverDHT.ContainsKey(_serverConfig.ServerName))
            {
                CreateServer(new ServerDTO(_serverConfig.ServerName, _serverConfig.BackupServer, (ServerMode)_serverConfig.Mode));
                CreateServer(new ServerDTO("p2", "b2", ServerMode.Secondary));

            }
        }

        void HandleChange(object? model, BasicDeliverEventArgs ea)
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            SharedHashTableChangeDTO ? change = JsonSerializer.Deserialize<SharedHashTableChangeDTO>(body);
            Log.Debug("Handle Change: " + body);
            if(change != null)
            {
                if (!string.IsNullOrEmpty(change.Change))
                {
                    switch (change.DataType)
                    {
                        case ChangeType.Client:
                            if (_lockClientIds.Remove(change.ChangeId))
                                HandleClientChange(change.Change);

                            if (_lockClientIds.Count == 0)
                            {
                                _lockClientDHT.EnterWriteLock();
                                _lockClient = false;
                                _lockClientDHT.ExitWriteLock();
                            }

                            break;
                        case ChangeType.Server:
                            if (_lockServerIds.Remove(change.ChangeId))
                                HandleServerChange(change.Change);

                            if (_lockServerIds.Count == 0)
                            {
                                _lockServerDHT.EnterWriteLock();
                                _lockServer = false;
                                _lockServerDHT.ExitWriteLock();
                            }
                            break;
                        case ChangeType.Worker:
                            if (_lockWorkerIds.Remove(change.ChangeId))
                                HandleWorkerChange(change.Change);

                            if (_lockWorkerIds.Count == 0)
                            {
                                _lockWorkerDHT.EnterWriteLock();
                                _lockWorker = false;
                                _lockWorkerDHT.ExitWriteLock();
                            }
                            break;
                    }
                }
                else
                {
                    switch (change.DataType)
                    {
                        case ChangeType.Client:
                            _lockClientIds.Remove(change.ChangeId);

                            if (_lockClientIds.Count == 0)
                            {
                                _lockClientDHT.EnterWriteLock();
                                _lockClient = false;
                                _lockClientDHT.ExitWriteLock();
                            }

                            break;
                        case ChangeType.Server:
                            _lockServerIds.Remove(change.ChangeId);
                            if (_lockServerIds.Count == 0)
                            {
                                _lockServerDHT.EnterWriteLock();
                                _lockServer = false;
                                _lockServerDHT.ExitWriteLock();
                            }

                            break;
                        case ChangeType.Worker:
                            _lockWorkerIds.Remove(change.ChangeId);

                            if (_lockWorkerIds.Count == 0)
                            {
                                _lockWorkerDHT.EnterWriteLock();
                                _lockWorker = false;
                                _lockWorkerDHT.ExitWriteLock();
                            }
                            break;
                    }
                }
            }
        }

        void HandleClientChange(string s)
        {
            ClientDTO? obj = JsonSerializer.Deserialize<ClientDTO?>(s);
            if(obj != null)
            {
                if (_clientsDHT.ContainsKey(obj.ClientId))
                {
                    _dataAccessHandler.UpdateClientDHT(obj);
                    _clientsDHT[obj.ClientId] = obj;
                }
                else
                {
                    _dataAccessHandler.AddClientToClientDHT(obj);
                    _clientsDHT.Add(obj.ClientId, obj);
                }
            }
        }

        void HandleServerChange(string s)
        {
            ServerDTO? obj = JsonSerializer.Deserialize<ServerDTO?>(s);
            if (obj != null)
            {
                if (_serverDHT.ContainsKey(obj.Name))
                {
                    _serverDHT[obj.Name] = obj;
                    _dataAccessHandler.UpdateServerDHT(obj);
                }
                else
                {
                    _serverDHT.Add(obj.Name, obj);
                    _dataAccessHandler.AddServerToServerDHT(obj);
                }
            }
        }

        void HandleWorkerChange(string s)
        {
            WorkerDTO? obj = JsonSerializer.Deserialize<WorkerDTO?>(s);
            if (obj != null)
            {
                if (_workersDHT.ContainsKey(obj.WorkerId))
                {
                    _dataAccessHandler.UpdateWorkerDHT(obj);
                    _workersDHT[obj.WorkerId] = obj;
                }
                else
                {
                    _dataAccessHandler.AddWorkerToWorkerDHT(obj);
                    _workersDHT.Add(obj.WorkerId, obj);
                }
            }
        }

        void MakeChange(object obj, ChangeType type)
        {
            SharedHashTableChangeDTO dto = new SharedHashTableChangeDTO();
            RequestLockDTO requestLockDTO = new RequestLockDTO();
            Guid guid = Guid.NewGuid();

            dto.DataType = type;
            dto.ChangeId = guid;
            dto.Change = JsonSerializer.Serialize(obj);

            requestLockDTO.LockType = type;
            requestLockDTO.ChangeId = guid;

            _lockChangeQueue.EnterWriteLock();
            _changeQueue.Add(guid, dto);
            _changeQueueCount.Add(guid, 0);
            _lockChangeQueue.ExitWriteLock();
            var props = _comHandler.GetBasicProperties();
            props.CorrelationId = guid.ToString();
            props.ReplyTo = _serverConfig.ServerName + "_multicastLockReply";
            _comHandler.Publish("multicast", "requestLock", props, JsonSerializer.Serialize(requestLockDTO));
        }

        void HandleRequestLock(object? model, BasicDeliverEventArgs ea)
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            RequestLockDTO? rlDTO = JsonSerializer.Deserialize<RequestLockDTO>(body);
            ea.Body = null;
            Log.Debug("Handle Request Lock" + body);
            if(rlDTO != null)
            {
                var props = _comHandler.GetBasicProperties();
                switch (rlDTO.LockType)
                {
                    case ChangeType.Client:
                        _lockClientDHT.EnterWriteLock();
                        _lockClient = true;
                        try
                        {
                            _lockClientIds.Add(rlDTO.ChangeId);
                            props.CorrelationId = ea.BasicProperties.CorrelationId;
                            _comHandler.Publish("", ea.BasicProperties.ReplyTo, props, "");
                        }
                        finally
                        {
                            _lockClientDHT.ExitWriteLock();
                        }
                        break;
                    case ChangeType.Worker:
                        _lockWorkerDHT.EnterWriteLock();
                        _lockWorker = true;
                        try
                        {
                            _lockWorkerIds.Add(rlDTO.ChangeId);
                            props.CorrelationId = ea.BasicProperties.CorrelationId;
                            _comHandler.Publish("", ea.BasicProperties.ReplyTo, props, "");
                        }
                        finally
                        {
                            _lockWorkerDHT.ExitWriteLock();
                        }
                        break;
                    case ChangeType.Server:
                        _lockServerDHT.EnterWriteLock();
                        _lockServer = true;
                        try
                        {
                            _lockServerIds.Add(rlDTO.ChangeId);

                            props.CorrelationId = ea.BasicProperties.CorrelationId;
                            _comHandler.Publish("", ea.BasicProperties.ReplyTo, props, "");
                        }
                        finally
                        {
                            _lockServerDHT.ExitWriteLock();
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        void HandleRequestLockReply(object? model, BasicDeliverEventArgs ea)
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            Guid guid = Guid.Parse(ea.BasicProperties.CorrelationId);
            Log.Debug("Handle Request Lock Reply: " + guid.ToString());
            if (_changeQueueCount.ContainsKey(guid))
            {
                _lockChangeQueue.EnterWriteLock();
                try
                {
                    int cnt = _changeQueueCount[guid];
                    cnt++;

                    if (cnt >= _serverDHT.Count)
                    {
                        SharedHashTableChangeDTO change = _changeQueue[guid];
                        var props = _comHandler.GetBasicProperties();
                        props.CorrelationId = guid.ToString();
                        _comHandler.Publish("multicast", "changes", props, JsonSerializer.Serialize(change));
                        _changeQueue.Remove(guid);
                        _changeQueueCount.Remove(guid);
                    }
                    else
                    {
                        _changeQueueCount[guid] = cnt;
                    }
                }
                finally
                {
                    _lockChangeQueue.ExitWriteLock();
                }
            }
        }

        public void AddClientToClientDHT(ClientDTO client)
        {
            MakeChange(client, ChangeType.Client);
        }

        public void CreateClient(ClientDTO client)
        {
            MakeChange(client, ChangeType.Client);
        }

        public ClientDTO? FindClientByUserName(string username)
        {
            while (_lockClient) ;
            _lockClientDHT.EnterReadLock();
            try
            {
                return _dataAccessHandler.FindClientByUserName(username);
            }
            finally { _lockClientDHT.ExitReadLock();}
            
        }

        public void UpdateClientDHT(ClientDTO client)
        {
            MakeChange(client, ChangeType.Client);
        }

        public void UpdateWorkerDHT(WorkerDTO worker)
        {
            MakeChange(worker, ChangeType.Worker);
        }

        public List<WorkerDTO> GetMyWorkers()
        {
            List<WorkerDTO> retVal = new List<WorkerDTO>();
            
            foreach(var keyValuePair in _workersDHT.Where(w => w.Value.ServerName == _serverConfig.ServerName).ToList())
            {
                retVal.Add(keyValuePair.Value);
            }

            return retVal;
        }

        public Dictionary<string, ServerDTO> GetPrimaryServers()
        {
            
            while (_lockClient) ;
            _lockClientDHT.EnterReadLock();
            try
            {
                return _dataAccessHandler.GetPrimaryServers();
            }
            finally { _lockClientDHT.ExitReadLock(); }
        }

        public ServerDTO? GetServerInfo(string serverName)
        {
            
            while (_lockClient) ;
            _lockClientDHT.EnterReadLock();
            try
            {
                return _dataAccessHandler.GetServerInfo(serverName);
            }
            finally { _lockClientDHT.ExitReadLock(); }
        }

        public void CreateServer(ServerDTO server)
        {
            MakeChange(server, ChangeType.Server);
        }

        public void AddWorkerToWorkerDHT(WorkerDTO worker)
        {
            MakeChange(worker, ChangeType.Worker);
        }

        public bool ClientExists(Guid clientId)
        {
            while (_lockClient) Thread.Sleep(20);
            _lockClientDHT.EnterReadLock();
            try
            {
                return _clientsDHT.ContainsKey(clientId);
            }
            finally
            {
                _lockClientDHT.ExitReadLock();
            }
        }

        public ClientDTO? GetClientById(Guid clientId)
        {
            ClientDTO? client = null;
            while (_lockClient) Thread.Sleep(20);
            _lockClientDHT.EnterReadLock();
            try
            {
                _clientsDHT.TryGetValue(clientId, out client);
            }
            finally
            {
                _lockClientDHT.ExitReadLock();
            }
            
            return client;
        }

        public bool WorkerExists(Guid workerId)
        {
            while (_lockWorker) Thread.Sleep(20);
            _lockWorkerDHT.EnterReadLock();
            try
            {
                return _workersDHT.ContainsKey(workerId);
            }
            finally
            {
                _lockWorkerDHT.ExitReadLock();
            }
        }

        public WorkerDTO? GetWorkerByJobId(Guid jobId)
        {
            WorkerDTO? worker = null;
            while (_lockWorker) ;
            _lockWorkerDHT.EnterReadLock();
            try
            {
                worker = _workersDHT.Where(w => w.Value.JobId == jobId)?.FirstOrDefault().Value;
            }
            finally
            {
                _lockWorkerDHT.ExitReadLock();
            }

            return worker;
        }

        public WorkerDTO? GetWorkerById(Guid workerId)
        {
            WorkerDTO? worker = null;
            while (_lockWorker);
            _lockWorkerDHT.EnterReadLock();
            try
            {
                _workersDHT.TryGetValue(workerId, out worker);
            }
            finally
            {
                _lockWorkerDHT.ExitReadLock();
            }

            return worker;
        }

        public void AddTask(ServiceTaskDTO task)
        {
            _dataAccessHandler.AddTask(task);
        }

        public void UpdateTask(ServiceTaskDTO task)
        {
            task.LastActivity = DateTime.UtcNow;
            _dataAccessHandler.UpdateTask(task);
        }

        public ServiceTaskDTO? GetTaskFromId(Guid taskId)
        {
            return _dataAccessHandler.GetTaskFromId(taskId);
        }

        public List<ServiceTaskDTO> GetTaskForClient(Guid clientId)
        {
            return _dataAccessHandler.GetTaskForClient(clientId);
        }

        public List<LocationDTO> GetLocations()
        {
            return _dataAccessHandler.GetLocations();
        }

        public WorkerDTO? GetAvailableWorker()
        {
            while (_lockWorker) Thread.Sleep(20);
            _lockWorkerDHT.EnterReadLock();
            try
            {
                var w = _workersDHT.Where(w => w.Value.Status == WorkerStatus.Available).FirstOrDefault().Value;

                return w;

            }
            catch (Exception)
            {

                return null;
            }
            finally { _lockWorkerDHT.ExitReadLock();}
        }

        public List<ServiceTaskDTO> GetTasksFromStatus(ServiceTaskStatus status)
        {
            return _dataAccessHandler.GetTasksFromStatus(status);        
        }
    }
}