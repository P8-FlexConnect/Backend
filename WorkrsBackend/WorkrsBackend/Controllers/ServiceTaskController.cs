using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;
using System.Text;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;
using WorkrsBackend.FTP;

namespace WorkrsBackend.Controllers
{
    [Route("api/[controller]/[Action]")]
    [ApiController]
    [Authorize]
    public class ServiceTaskController : ControllerBase
    {
        ISharedResourceHandler _sharedResourceHandler;
        IIdentity? _identity;

        public ServiceTaskController(ISharedResourceHandler sharedResourceHandler, IHttpContextAccessor httpContextAccessor)
        {
            _sharedResourceHandler = sharedResourceHandler;
            _identity = httpContextAccessor?.HttpContext?.User?.Identity;
        }

        [HttpGet]
        public ActionResult<List<ServiceTaskDTO>> Tasks() 
        {
            if (_identity != null)
            {
                var username = _sharedResourceHandler.FindClientByUserName(_identity.Name);
                if (username != null)
                {
                    return Ok(_sharedResourceHandler.GetTaskForClient(username.ClientId));
                }
            }

            return NotFound();
        }

        [HttpGet]
        public ActionResult<ServiceTaskDTO> Task(Guid taskId)
        {
            var result = _sharedResourceHandler.GetTaskFromId(taskId);
            if (result != null) 
                return Ok(result);

            return NotFound();
        }

        [HttpPut]
        public IActionResult Cancel(ServiceTaskDTO task)
        {
            var result = _sharedResourceHandler.GetTaskFromId(task.Id);
            if (result != null)
            {
                if(result.Status < ServiceTaskStatus.Cancel )
                {
                    result.Status = ServiceTaskStatus.Cancel;
                    _sharedResourceHandler.UpdateTask(result);
                    return StatusCode(202);
                }
                return BadRequest();
            }

            return NotFound();
        }

        [HttpPut]
        public IActionResult Update(ServiceTaskDTO serviceTask) 
        {
            var task = _sharedResourceHandler.GetTaskFromId(serviceTask.Id);
            if (task != null)
            {
                task.Description = serviceTask.Description;
                task.Name = serviceTask.Name;
                _sharedResourceHandler.UpdateTask(task);
                return Ok();
            }

            return NotFound();
        }

        [HttpPut]
        public IActionResult Stop(Guid taskId) 
        {
            var task = _sharedResourceHandler.GetTaskFromId(taskId);
            if(task != null)
            {
                task.Status = ServiceTaskStatus.Stop;
                _sharedResourceHandler.UpdateTask(task);
                return Ok();

            }
            return NotFound();
        }

        [HttpPut]
        public ActionResult<PrepareDTO> Prepare(Guid taskId)
        {
            var worker = _sharedResourceHandler.GetAvailableWorker();

            if (worker != null)
            {
                var t = _sharedResourceHandler.GetTaskFromId(taskId);
                var c = _sharedResourceHandler.FindClientByUserName(_identity.Name);
                if(c != null && t != null && (t.Status != ServiceTaskStatus.Completed && t.Status != ServiceTaskStatus.InProgress)) 
                {
                    string source = $"{c.ClientId}/{t.Id}/source/";
                    string backup = $"{c.ClientId}/{t.Id}/backup/";
                    string result = $"{c.ClientId}/{t.Id}/result/";

                    t.SourcePath = $"{worker.LANIp}:{worker.FTPUser}:{worker.FTPPassword}:{source}{t.Name}.py";
                    t.BackupPath = $"{worker.LANIp}:{worker.FTPUser}:{worker.FTPPassword}:{backup}";
                    t.ResultPath = $"{worker.LANIp}:{worker.FTPUser}:{worker.FTPPassword}:{result}";
                    t.Status = ServiceTaskStatus.Queued;

                    PrepareDTO retval = new PrepareDTO()
                    {
                        Wifi = new WifiDTO() { Password = "Worker1AP", SSID = "Worker1AP" },
                        ServiceTask = t
                    };

                    worker.Status = WorkerStatus.Reserved;
                    worker.JobId = taskId;
                    _sharedResourceHandler.UpdateWorkerDHT(worker);
                    _sharedResourceHandler.UpdateTask(t);
                    return Ok(retval);
                }
            }
            return NotFound();
        }

        [HttpPut]
        public IActionResult Start(Guid taskId)
        {
            var task = _sharedResourceHandler.GetTaskFromId(taskId);
            if (task != null)
            {
                if(task.Status == ServiceTaskStatus.Queued)
                {
                    task.Status = ServiceTaskStatus.Starting;
                    _sharedResourceHandler.UpdateTask(task);
                    return Ok();
                }
                return BadRequest();
            }
            return NotFound();
        }

        [HttpPost]
        public IActionResult Create(string name, string description)
        {
            if(_identity != null)
            {
                var client = _sharedResourceHandler.FindClientByUserName(_identity.Name);
                if (client != null)
                {
                    var t = new ServiceTaskDTO(
                                        Guid.NewGuid(),
                                        client.ClientId,
                                        name,
                                        description,
                                        DateTime.UtcNow,
                                        DateTime.UtcNow,
                                        ServiceTaskStatus.Created);
                    _sharedResourceHandler.AddTask(t);
                    return StatusCode(201,t);
                }
            }

            return NotFound();
        }
    }
}
