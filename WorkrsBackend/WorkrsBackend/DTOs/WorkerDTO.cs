namespace WorkrsBackend.DTOs
{
    public enum WorkerStatus
    {
        Available = 0,
        Reserved = 1,
        Busy = 1,
        Done = 2,
        MIA = 3,
    }

    public class WorkerDTO
    {
        public Guid WorkerId { get; set; }
        public WorkerStatus Status { get; set; }
        public string ServerName { get; set; }
        public string LANIp { get; set; }
        public Guid JobId { get; set; } = Guid.Empty;
        public string FTPUser { get; set; }
        public string FTPPassword { get; set; }

        public WorkerDTO()
        {
            LANIp = string.Empty;
        }
        public WorkerDTO(Guid workerId, WorkerStatus status, string serverName) :this()
        {
            WorkerId = workerId;
            Status = status;
            ServerName = serverName;
        }
        public WorkerDTO(Guid workerId, WorkerStatus status, string serverName, Guid jobId, string lanIp, string ftpUser, string ftpPassword)
        {
            WorkerId = workerId;
            Status = status;
            ServerName = serverName;
            JobId = jobId;
            LANIp = lanIp;
            FTPUser = ftpUser;
            FTPPassword = ftpPassword;
        }
    }
}
