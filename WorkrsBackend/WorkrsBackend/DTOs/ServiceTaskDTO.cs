namespace WorkrsBackend.DTOs
{
    public enum ServiceTaskStatus
    {
        Created,
        Starting,
        InProgress,
        Canceled,
        Cancel,
        Failed,
        Completed,
        Stop,
        Stopped,
        Queued,
    }

    public class ServiceTaskDTO
    {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime LastActivity { get; set; }
        public ServiceTaskStatus Status { get; set; }
        public string SourcePath { get; set; }
        public string BackupPath { get; set; }
        public string ResultPath { get; set; }

        public ServiceTaskDTO(Guid id, Guid clientId, string name, string description, DateTime dateAdded, DateTime lastActivity, ServiceTaskStatus status, string sourcePath, string backupPath, string resultPath)
        {
            Id = id;
            ClientId = clientId;
            Name = name;
            Description = description;
            DateAdded = dateAdded;
            LastActivity = lastActivity;
            Status = status;
            SourcePath = sourcePath;
            BackupPath = backupPath;
            ResultPath = resultPath;
        }
        public ServiceTaskDTO(Guid id, Guid clientId, string name, string description, DateTime dateAdded, DateTime lastActivity, ServiceTaskStatus status):this()
        {
            Id = id;
            ClientId = clientId;
            Name = name;
            Description = description;
            DateAdded = dateAdded;
            LastActivity = lastActivity;
            Status = status;
        }
        public ServiceTaskDTO(Guid id, Guid clientId, string name, ServiceTaskStatus status):this()
        {
            Id = id;
            ClientId = clientId;
            Name = name;
            Status = status;
        }
        public ServiceTaskDTO()
        {
            SourcePath = "";
            BackupPath = "";
            ResultPath = "";
        }
    }
}
