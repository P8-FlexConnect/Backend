using RabbitMQ.Client.Events;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Threading;
using WorkrsBackend.Config;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;
using WorkrsBackend.FTP;
using WorkrsBackend.RabbitMQ;

namespace WorkrsBackend
{
    public class ServiceLogic : IHostedService
    {
        readonly IRabbitMQHandler _rabbitMQHandler;
        readonly IServerConfig _serverConfig;
        readonly ISharedResourceHandler _dataAccessHandler;
        readonly FTPHandler _ftpHandler;
        Dictionary<Guid, TaskInProgress> _tasks = new();
        Dictionary<Guid, DateTime> _workerKeepAlive = new();
        object _lock = new object();
        double _mu = 0.000001;
        double _interval = 30000;
        int _seed = 120;
        Thread _updateThread;
        CancellationToken _CancellationToken;

        public ServiceLogic(IServerConfig serverConfig, ISharedResourceHandler dataAccessHandler, IRabbitMQHandler rabbitMQHandler)
        {
            _serverConfig = serverConfig;
            _dataAccessHandler = dataAccessHandler;
            _rabbitMQHandler = rabbitMQHandler;
            _ftpHandler = new FTPHandler();
            _updateThread = new Thread(ThreadLoop);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _CancellationToken = cancellationToken;
            _updateThread.Start();
            return Task.CompletedTask;
        }

        void ThreadLoop()
        {
            Init();
            Log.Information("Init completed");
            while (!_CancellationToken.IsCancellationRequested)
            {
                try
                {
                    Update();
                }
                catch (Exception ex)
                {
                    Log.Error("Update: " + ex.ToString());
                }
                Thread.Sleep(2000);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        void Init()
        {
            if(_serverConfig.Mode == (int)ServerMode.Primary)
            {
                _rabbitMQHandler.CreateClientRegisterConsumer(ClientRegistrationReceived);
                _rabbitMQHandler.CreateClientConnectConsumer(ClientConnectionReceived);
                _rabbitMQHandler.CreateWorkerRegisterConsumer(WorkerRegistrationReceived);
                _rabbitMQHandler.CreateWorkerConnectConsumer(WorkerConnectionReceived);
                _ftpHandler.Init("192.168.1.10", "p1user", "p1user");
                ClientDTO? c = _dataAccessHandler.FindClientByUserName("test");
                if (c == null)
                {
                    _dataAccessHandler.AddClientToClientDHT(new ClientDTO(Guid.NewGuid(), "test", "test", "P1", "P1", "Firstname", "Lastname"));
                }
            }
        }

        bool test = false;
        void Update()
        {
            _rabbitMQHandler.Update();
            if(test)
            {
                ClientDTO? c =_dataAccessHandler.FindClientByUserName("test");
                if(c != null)
                {
                    ServiceTaskDTO st = new ServiceTaskDTO(
                        Guid.NewGuid(),
                        c.ClientId,
                        "myTestTask",
                        "myTestTaskDesc",
                        DateTime.UtcNow, 
                        DateTime.UtcNow,
                        ServiceTaskStatus.Created,
                        "p1.source",
                        "p1.backup",
                        "p1.result");
                    _dataAccessHandler.AddTask(st);
                    st.Name = "test12";
                    _dataAccessHandler.UpdateTask(st);
                }
                test = false;
            }
            CheckKeepAliveForWorkers(_dataAccessHandler.GetMyWorkers(), 20);

            HandleStartTasks();
            HandleInProgressTasks();
            HandleCancelTasks();
            HandleStopTask();
            HandleQueuedTasks();
            Log.Debug("Update alive");
        }

        void CheckKeepAliveForWorkers(List<WorkerDTO> workers, int secondsMax)
        {
            Dictionary<Guid, DateTime> local = new Dictionary<Guid, DateTime>();
            lock (_lock)
            {
                foreach (KeyValuePair<Guid, DateTime> keyValuePair in _workerKeepAlive)
                {
                    if (DateTime.UtcNow - keyValuePair.Value < new TimeSpan(0, 0, secondsMax))
                        local.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }

            foreach(WorkerDTO worker in workers)
            {
                if(!local.ContainsKey(worker.WorkerId) && worker.Status != WorkerStatus.MIA)
                {
                    worker.Status = WorkerStatus.MIA;
                    worker.LANIp = "0.0.0.0";
                    _dataAccessHandler.UpdateWorkerDHT(worker);

                    var val = _tasks.Where(t => t.Value.Worker?.WorkerId == worker.WorkerId).FirstOrDefault();
                    if(val.Value != null)
                    {
                        _tasks[val.Key].Worker = null;
                    }
                    Log.Debug($"CheckKeepAliveForWorkers: Worker {worker.WorkerId} MIA");
                }
                else if(local.ContainsKey(worker.WorkerId) && worker.Status == WorkerStatus.MIA)
                {
                    ResetWorkerAvailable(worker);
                    Log.Debug($"CheckKeepAliveForWorkers: Worker {worker.WorkerId} Available");
                }
            }
        }

        void UpdateWorkerKeerpAlive(WorkerDTO worker)
        {
            lock(_lock)
            {
                Log.Debug($"UpdateWorkerKeerpAlive: {worker.WorkerId}");
                if (_workerKeepAlive.ContainsKey(worker.WorkerId))
                    _workerKeepAlive[worker.WorkerId] = DateTime.UtcNow;
                else
                    _workerKeepAlive.Add(worker.WorkerId, DateTime.UtcNow);
            }
        }

        void HandleStartTasks()
        {
            var jobs = _dataAccessHandler.GetTasksFromStatus(ServiceTaskStatus.Starting);
            if(jobs.Count > 0)
                Log.Debug($"HandleStartTasks, jobs to start: {jobs.Count}");
            foreach (var job in jobs)
            {
                StartJob(job);
            }
        }

        void HandleInProgressTasks()
        {
            var jobs = _dataAccessHandler.GetTasksFromStatus(ServiceTaskStatus.InProgress);
            foreach (var job in jobs)
            {
                if (_tasks.ContainsKey(job.Id))
                {
                    if (_tasks[job.Id].Worker == null)
                    {
                        RecoverJob(job);
                        Log.Debug($"HandleInProgressTasks, job recover: {job.Id}");
                    }
                }
                else
                {
                    RecoverJob(job);

                    Log.Debug($"HandleInProgressTasks, job recover: {job.Id}");
                }
            }
        }

        void HandleCancelTasks()
        {
            var jobs = _dataAccessHandler.GetTasksFromStatus(ServiceTaskStatus.Cancel);
            if(jobs.Count > 0)
                Log.Debug($"HandleCancelTasks, jobs to cancel: {jobs.Count}");
            foreach (var job in jobs)
            {
                TaskInProgress tp;
                if (_tasks.TryGetValue(job.Id, out tp))
                {
                    if(tp.Worker != null)
                        StopJob(tp.Worker.WorkerId, tp.ServiceTask);
                    else
                    {
                        var t = _dataAccessHandler.GetTaskFromId(tp.ServiceTask.Id);
                        t.Status = ServiceTaskStatus.Canceled;
                        _dataAccessHandler.UpdateTask(t);
                    }
                }
                else
                {
                    job.Status = ServiceTaskStatus.Canceled;
                    _dataAccessHandler.UpdateTask(job);
                }
            }
        }

        void HandleStopTask()
        {
            var jobs = _dataAccessHandler.GetTasksFromStatus(ServiceTaskStatus.Stop);
            if (jobs.Count > 0)
                Log.Debug($"HandleCancelTasks, jobs to cancel: {jobs.Count}");
            foreach(var job in jobs)
            {
                HandleStopTask(job);
            }
        }

        void HandleStopTask(ServiceTaskDTO job)
        {
            TaskInProgress tp;
            if (_tasks.TryGetValue(job.Id, out tp))
            {
                if (tp.Worker != null)
                    StopJob(tp.Worker.WorkerId, tp.ServiceTask);
                else
                {
                    var t = _dataAccessHandler.GetTaskFromId(tp.ServiceTask.Id);
                    t.Status = ServiceTaskStatus.Stopped;
                    _dataAccessHandler.UpdateTask(t);
                }
            }
            else
            {
                job.Status = ServiceTaskStatus.Stopped;
                _dataAccessHandler.UpdateTask(job);
            }
        }

        void HandleQueuedTasks()
        {
             var val = _dataAccessHandler.GetTasksFromStatus(ServiceTaskStatus.Queued);
            foreach(var job in val)
            {
                if (DateTime.UtcNow - job.LastActivity > TimeSpan.FromMinutes(2))
                {
                    job.Status = ServiceTaskStatus.Created;
                    var worker = _dataAccessHandler.GetWorkerByJobId(job.Id);
                    _dataAccessHandler.UpdateWorkerDHT(worker);
                    if(worker != null)
                    {
                        worker.Status = WorkerStatus.Available;
                        _dataAccessHandler.UpdateWorkerDHT(worker);
                    }    
                }
            }
        }

        void ClientRegistrationReceived(object? model, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var props = ea.BasicProperties;

                var client = _dataAccessHandler.FindClientByUserName(message);
                if (client == null)
                {
                    Log.Debug("ClientRegistrationReceived: unknown user");
                    return;
                }

                var replyProps = _rabbitMQHandler.GetBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                string response = JsonSerializer.Serialize(new RegisterClientResponseDTO(client.ClientId, _serverConfig.ServerName, _serverConfig.ServerName));
                _rabbitMQHandler.Publish("", props.ReplyTo, replyProps, response);
                Log.Debug("ClientRegistrationReceived: " + response);
            }
            catch(Exception ex)
            {
                Log.Error("ClientRegistrationReceived: " + ex.ToString());
            } 
        }

        void ClientConnectionReceived(object? model, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var clientId = Guid.Parse(message);
                AddClient(clientId);
                Log.Debug("ClientConnectionReceived: " + clientId);
            }
            catch (Exception ex)
            {
                Log.Error("ClientConnectionReceived: " + ex.ToString());
            }
        }

        void WorkerRegistrationReceived(object? model, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var props = ea.BasicProperties;

            var workerId = Guid.Parse(message);

            var replyProps = _rabbitMQHandler.GetBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;

            string response = JsonSerializer.Serialize(new RegisterWorkerResponseDTO(workerId, _serverConfig.ServerName));

            _rabbitMQHandler.Publish("", props.ReplyTo, replyProps, response);
            Log.Debug($"WorkerRegistrationReceived: {workerId}");
        }

        void WorkerConnectionReceived(object? model, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var workerId = Guid.Parse(message);
            AddWorker(workerId);
            Log.Debug($"WorkerConnectionReceived: {workerId}");
        }

        void AddClient(Guid clientId)
        {
            ClientDTO? c = _dataAccessHandler.GetClientById(clientId);
            if (c == null) return;
            c.ServerName = c.DataServer;
            _dataAccessHandler.UpdateClientDHT(c);
            _rabbitMQHandler.Connect(c.ClientId, HandleClientRequest);
        }

        void HandleClientRequest(object? model, BasicDeliverEventArgs ea)
        {
            Task.Run(() => {
                try
                {
                    ClientDTO? c = _dataAccessHandler.GetClientById(Guid.Parse(ea.ConsumerTag));
                    if (c != null)
                    {
                        if(ea.BasicProperties.Headers != null)
                        {
                            var props = _rabbitMQHandler.GetBasicProperties();
                            props.Headers = new Dictionary<string, object>();
                            props.Headers.Add("type", "");
                            string msgType = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]);
                            switch (msgType)
                            {
                                case "startNewTask":
                                    {
                                        var t = new ServiceTaskDTO(
                                        Guid.NewGuid(),
                                        c.ClientId,
                                        Encoding.UTF8.GetString(ea.Body.ToArray()),
                                        ServiceTaskStatus.Created);

                                        if(ea.BasicProperties.Headers.ContainsKey("mu"))
                                        {
                                            _mu = double.Parse(Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["mu"]));
                                            _interval = double.Parse(Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["interval"]));
                                            _seed = int.Parse(Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["seed"]));
                                            _rand = new Random(_seed);
                                        }

                                        string source = $"{c.ClientId}/{t.Id}/source/";
                                        string backup = $"{c.ClientId}/{t.Id}/backup/";
                                        string result = $"{c.ClientId}/{t.Id}/result/";
                                        _ftpHandler.CreateDirectory(source);
                                        _ftpHandler.CreateDirectory(backup);
                                        _ftpHandler.CreateDirectory(result);

                                        t.SourcePath = $"{_ftpHandler.HostName}:{_ftpHandler.Username}:{_ftpHandler.Password}:{source}{t.Name}.py";
                                        t.BackupPath = $"{_ftpHandler.HostName}:{_ftpHandler.Username}:{_ftpHandler.Password}:{backup}";
                                        t.ResultPath = $"{_ftpHandler.HostName}:{_ftpHandler.Username}:{_ftpHandler.Password}:{result}";
                                        _dataAccessHandler.AddTask(t);
                                        props.Headers["type"] = "startNewTask";
                                        string s = JsonSerializer.Serialize(t);
                                        Log.Debug($"HandleClientRequest_startNewTask, client:{c.ClientId}, Task id: {t.Id}");
                                        _rabbitMQHandler.Publish("client", $"{c.ClientId}", props, s);
                                    }
                                    break;
                                case "taskUploadCompleted":
                                    {
                                        var t = _dataAccessHandler.GetTaskFromId(Guid.Parse(Encoding.UTF8.GetString(ea.Body.ToArray())));
                                        t.Status = ServiceTaskStatus.Starting;
                                        _dataAccessHandler.UpdateTask(t);
                                        Log.Debug($"HandleClientRequest_taskUploadCompleted, client:{c.ClientId}, Task id: {t.Id}");
                                    }
                                    break;
                                case "getServiceTasks":
                                    {
                                        var t = _dataAccessHandler.GetTaskForClient(c.ClientId);
                                        props.Headers["type"] = "getServiceTasks";
                                        string s = JsonSerializer.Serialize(t);
                                        Log.Debug($"HandleClientRequest_getServiceTasks, client:{c.ClientId}");
                                        _rabbitMQHandler.Publish("client", $"{c.ClientId}", props, s);
                                    }
                                    break;
                                case "cancelServiceTask":
                                    {

                                        var t = _dataAccessHandler.GetTaskFromId(Guid.Parse(Encoding.UTF8.GetString(ea.Body.ToArray())));
                                        t.Status = ServiceTaskStatus.Cancel;
                                        _dataAccessHandler.UpdateTask(t);
                                        Log.Debug($"HandleClientRequest_cancelServiceTask, client:{c.ClientId}, Task:{t.Id}");
                                    }
                                    break;
                                default:
                                    {
                                        Log.Debug($"Unkown message tag from client: {c.ClientId}");
                                        break;
                                    }
                            }
                        }
                        else
                            Log.Debug("HandleClientRequest, Headers was set to NULL");
                    }
                    else
                        Log.Debug("HandleClientRequest, Unknown client");
                }
                catch (Exception ex)

                {
                    Log.Error("HandleClientRequest: " + ex.ToString());
                }
            });
        }

        void HandleWorkerRequest(object? model, BasicDeliverEventArgs ea)
        {
            Task.Run(() =>
            {
                WorkerDTO? w = _dataAccessHandler.GetWorkerById(Guid.Parse(ea.ConsumerTag));
                if (w != null)
                {
                    if (ea.BasicProperties.Headers != null)
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var props = _rabbitMQHandler.GetBasicProperties();
                        props.Headers = new Dictionary<string, object>();
                        props.Headers.Add("type", "");
                        string msgType = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["type"]);
                        switch (msgType)
                        {
                            case "startJob":
                                {
                                    Guid guid = Guid.Parse(message);
                                    if (_tasks.ContainsKey(guid))
                                    {
                                        TaskInProgress tp = _tasks[guid];
                                        var t = _dataAccessHandler.GetTaskFromId(tp.ServiceTask.Id);
                                        t.Status = ServiceTaskStatus.InProgress;
                                        _dataAccessHandler.UpdateTask(t);
                                        _tasks[guid].ServiceTask = t;
                                        Log.Debug($"Worker: {w.WorkerId} started task: {t.Id}");
                                    }
                                    else
                                    {
                                        props.Headers["type"] = "stopJob";
                                        Log.Debug($"HandleWorkerRequest_jobStarted, {w.WorkerId} forced to stopped");
                                        _rabbitMQHandler.Publish("worker", w.WorkerId.ToString(), props, "");
                                    }
                                }
                                break;
                            case "report":
                                {
                                    try
                                    {
                                        WorkerReportDTO report = JsonSerializer.Deserialize<WorkerReportDTO>(message);
                                        Log.Debug($"HandleWorkerRequest_report, Worker: {report.WorkerId}, serviceTask: {report.JobId}");
                                        
                                        if(w.LANIp != report.LANIp)
                                        {
                                            w.LANIp = report.LANIp;
                                            _dataAccessHandler.UpdateWorkerDHT(w);
                                        }    

                                        UpdateWorkerKeerpAlive(w);
                                        if (w.JobId != report.JobId && report.JobId != Guid.Empty)
                                        {
                                            Log.Debug($"HandleWorkerRequest_report, {report.WorkerId}, incorrect task!");
                                            StopJob(w.WorkerId, _dataAccessHandler.GetTaskFromId(report.JobId));
                                        }
                                        else if(w.JobId != Guid.Empty && report.JobId == Guid.Empty) 
                                        {
                                            var val = _tasks.Where(t => t.Value?.Worker?.JobId == w.JobId).FirstOrDefault().Value;
                                            if(val != null)
                                            {
                                                if(DateTime.UtcNow - val.ServiceTask.LastActivity > TimeSpan.FromMinutes(1))
                                                {
                                                    var t = _dataAccessHandler.GetTaskFromId(val.ServiceTask.Id);
                                                    t.Status = ServiceTaskStatus.Created;
                                                    _dataAccessHandler.UpdateTask(t);
                                                    ResetWorkerAvailable(w);
                                                    _tasks.Remove(t.Id);
                                                }
                                            }
                                        }
                                    }
                                    catch(Exception ex)
                                    {
                                        Log.Debug("exception");
                                    }
                                }
                                break;
                            case "jobDone":
                                {
                                    var t = _dataAccessHandler.GetTaskFromId(Guid.Parse(Encoding.UTF8.GetString(ea.Body.ToArray())));
                                    if(_tasks.ContainsKey(t.Id))
                                    {
                                        if(_tasks[t.Id].Worker?.WorkerId == w.WorkerId)
                                        {
                                            t.Status = ServiceTaskStatus.Completed;
                                            if (_tasks.ContainsKey(t.Id))
                                                _tasks.Remove(t.Id);
                                            _dataAccessHandler.UpdateTask(t);
                                            w.Status = WorkerStatus.Available;
                                            _dataAccessHandler.UpdateWorkerDHT(w);
                                            props.Headers["type"] = "jobDone";
                                            _rabbitMQHandler.Publish("worker", "log", props, "");
                                            Log.Debug($"HandleWorkerRequest_jobDone, task: {t.Id}");
                                        }
                                    }
                                    else
                                    {
                                        t.Status = ServiceTaskStatus.Failed;
                                        _dataAccessHandler.UpdateTask(t);
                                    }
                                }
                                break;
                            case "stopJob":
                                {
                                    TaskInProgress? tp;
                                    ResetWorkerAvailable(w);
                                    if (_tasks.TryGetValue(Guid.Parse(message), out tp))
                                    {
                                        var t = _dataAccessHandler.GetTaskFromId(tp.ServiceTask.Id);
                                        t.Status = ServiceTaskStatus.Stopped;
                                        _dataAccessHandler.UpdateTask(t);
                                        _tasks.Remove(t.Id);
                                        Log.Debug($"HandleWorkerRequest_stopJob, task: {t.Id}");
                                    }
                                }
                                break;
                            default:
                                Log.Debug($"Unkown msg type: {msgType}");
                                break;
                        }
                    }
                    else
                        Log.Debug("HandleWorkerRequest headers was set to NULL");
                }
                else
                    Log.Debug("HandleWorkerRequest, Unknown worker");
            });
        }

        void StopJob(Guid workerId, ServiceTaskDTO job)
        {
            var props = _rabbitMQHandler.GetBasicProperties();
            props.Headers = new Dictionary<string, object>();
            props.Headers.Add("type", "stopJob");

            var message = JsonSerializer.Serialize(job);
            _rabbitMQHandler.Publish("worker", workerId.ToString(), props, message);
            Log.Debug($"StopJob sent to worker; {workerId}, task: {job.Id}");
        }

        void RecoverJob(ServiceTaskDTO job)
        {

            var worker = _dataAccessHandler.GetAvailableWorker();
            if (worker == null)
                return;

            worker.Status = WorkerStatus.Busy;
            worker.JobId = job.Id;
            _dataAccessHandler.UpdateWorkerDHT(worker);
            if (!_tasks.ContainsKey(job.Id))
                _tasks.Add(job.Id, new TaskInProgress(job, worker));
            else
                _tasks[job.Id].Worker = worker;

            var props = _rabbitMQHandler.GetBasicProperties();
            props.Headers = new Dictionary<string, object>();
            props.Headers.Add("type", "recoverJob");

            //For the experiment:
            props.Headers.Add("fail", when_fault(_mu).ToString());
            props.Headers.Add("interval", _interval.ToString());
            //var interval = BestCheckpointInterval(_mu, _T, _overhead);
            //props.Headers.Add("interval", interval);

            var message = JsonSerializer.Serialize(job);
            _rabbitMQHandler.Publish("worker", worker.WorkerId.ToString(), props, message);
            Log.Debug($"RecoverJob sent to worker; {worker.WorkerId}, task: {job.Id}, fault: {props.Headers["fail"]}");
        }

        void StartJob(ServiceTaskDTO job)
        {
            if(!_tasks.ContainsKey(job.Id))
            {
                WorkerDTO worker = _dataAccessHandler.GetWorkerByJobId(job.Id);

                if (worker == null)
                    worker = _dataAccessHandler.GetAvailableWorker();
                
                if(worker == null || worker.Status == WorkerStatus.MIA)
                    return;

                worker.Status = WorkerStatus.Busy;
                worker.JobId = job.Id;
                _dataAccessHandler.UpdateWorkerDHT(worker);
                _tasks.Add(job.Id, new TaskInProgress(job, worker));
                var props = _rabbitMQHandler.GetBasicProperties();
                props.Headers = new Dictionary<string, object>();
                props.Headers.Add("type", "startJob");


                //For the experiment:
                //var interval = BestCheckpointInterval(_mu, _T, _overhead);
                props.Headers.Add("fail", when_fault(_mu).ToString());
                props.Headers.Add("interval", _interval.ToString());
                var message = JsonSerializer.Serialize(job);
                _rabbitMQHandler.Publish("worker", worker.WorkerId.ToString(), props, message);
                Log.Debug($"StartJob sent to worker; {worker.WorkerId}, task: {job.Id}, fault: {props.Headers["fail"]}");
            }
        }

        void ResetWorkerAvailable(WorkerDTO worker)
        {
            worker = _dataAccessHandler.GetWorkerById(worker.WorkerId);
            if(worker != null)
            {
                worker.Status = WorkerStatus.Available;
                worker.JobId = Guid.Empty;
                _dataAccessHandler.UpdateWorkerDHT(worker);
            }
        }

        void AddWorker(Guid workerId)
        {
            WorkerDTO worker = null;
            if (!_dataAccessHandler.WorkerExists(workerId))
            {
                worker = new WorkerDTO(workerId, WorkerStatus.Available, _serverConfig.ServerName);
                worker.FTPUser = "ftpuser";
                worker.FTPPassword = "ftpuser";
                _dataAccessHandler.AddWorkerToWorkerDHT(worker);
            }
            else
            {
                worker = _dataAccessHandler.GetWorkerById(workerId);
                worker.ServerName = _serverConfig.ServerName;
                _dataAccessHandler.UpdateWorkerDHT(worker);
            }
            UpdateWorkerKeerpAlive(worker);
            _rabbitMQHandler.Connect(workerId, HandleWorkerRequest);
        }



        /*------------------------------------------------ For experiments ------------------------------------------------*/

        Random _rand = new Random();
        //private const double _mu = 0.1; // chance of failure
        private const double _T = 100; // total expected execution time of script
        private const double _overhead = 1.05; // total overhead of checkpointing

        public double when_fault(double mu)
        {
            double r = _rand.NextDouble();
            return -Math.Log(r) / mu;
        }

        public double predict_execute_time2(double mu, double T)
        {
            return (Math.Exp(mu * T) - 1) / mu;
        }

        public int best_number_of_checkpoints(double mu, double T, double overhead)
        {
            double best_time = predict_execute_time2(mu, T) + 1;
            bool work = true;
            int N = 1;
            while (work)
            {
                double new_time =
                    N * predict_execute_time2(mu, T / N) +
                    (N - 1) * overhead;
                //Console.WriteLine("checkpoints {0,5} predicted execution time {1,8}", N, best_time);
                if (new_time > best_time)
                {
                    work = false;
                    N--;
                }
                else
                {
                    best_time = new_time;
                    N++;
                }
            }

            return (N);
        }

        public double BestCheckpointInterval(double mu, double T, double overhead)
        {
            var n = best_number_of_checkpoints((double)mu, (double)T, (double)overhead);
            return T / (n + 1);
        }
    }
}