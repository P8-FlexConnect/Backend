using WorkrsBackend.DTOs;

namespace WorkrsBackend.DataHandling
{
    public interface IDataAccessHandler
    {
        public void AddClientToClientDHT(ClientDTO client);
        //public Guid CreateClient(string username);
        public ClientDTO? FindClientByUserName(string username);
        public void DeleteClientFromClientDHT(Guid id);
        public Dictionary<Guid, ClientDTO> GetClientDHT();
        public void UpdateClientDHT(ClientDTO client);
        public void UpdateWorkerDHT(WorkerDTO worker);
        public Dictionary<string, ServerDTO> GetPrimaryServers();
        public ServerDTO? GetServerInfo(string serverName);
        public Dictionary<Guid, WorkerDTO> GetWorkerDHT();
        public void AddWorkerToWorkerDHT(WorkerDTO worker);
        public void AddServerToServerDHT(ServerDTO serverName);
        public void UpdateServerDHT(ServerDTO serverName);
        public void AddTask(ServiceTaskDTO task);
        public void UpdateTask(ServiceTaskDTO task);
        public ServiceTaskDTO? GetTaskFromId(Guid id);
        public List<ServiceTaskDTO> GetTaskForClient(Guid clientId);
        public List<ServiceTaskDTO> GetTasksFromStatus(ServiceTaskStatus status);
        public List<LocationDTO> GetLocations();
    }
}