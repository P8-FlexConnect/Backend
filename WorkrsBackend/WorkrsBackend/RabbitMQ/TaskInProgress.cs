using WorkrsBackend.DTOs;

namespace WorkrsBackend.RabbitMQ
{
    public class TaskInProgress
    {
        public ServiceTaskDTO ServiceTask { get; set; }
        public WorkerDTO? Worker { get; set; }

        public TaskInProgress(ServiceTaskDTO serviceTask, WorkerDTO worker)
        {
            ServiceTask = serviceTask;
            Worker = worker;
        }
    }
}
