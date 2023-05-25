using WorkrsBackend.DTOs;

namespace WorkrsBackend.DataHandling
{
    public interface ISharedResourceHandler
    {
        public void AddClientToClientDHT(ClientDTO client);
        public void CreateClient(ClientDTO client);
        public bool ClientExists(Guid clientId);
        public ClientDTO? GetClientById(Guid clientId);
        public bool WorkerExists(Guid clientId);
        public WorkerDTO? GetWorkerById(Guid clientId);
        public ClientDTO? FindClientByUserName(string username);
        public void UpdateClientDHT(ClientDTO client);
        public void UpdateWorkerDHT(WorkerDTO worker);
        public WorkerDTO? GetAvailableWorker();
        public List<WorkerDTO> GetMyWorkers();
        public WorkerDTO? GetWorkerByJobId(Guid jobId);
        public Dictionary<string, ServerDTO> GetPrimaryServers();
        public void CreateServer(ServerDTO server);
        public ServerDTO? GetServerInfo(string serverName);
        public void AddWorkerToWorkerDHT(WorkerDTO worker);
        public void AddTask(ServiceTaskDTO task);
        public void UpdateTask(ServiceTaskDTO task);
        public ServiceTaskDTO? GetTaskFromId(Guid id);
        public List<ServiceTaskDTO> GetTaskForClient(Guid clientId);
        public List<ServiceTaskDTO> GetTasksFromStatus(ServiceTaskStatus status);
        public List<LocationDTO> GetLocations();
    }
}
