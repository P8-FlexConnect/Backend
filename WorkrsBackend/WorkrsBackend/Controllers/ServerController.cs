using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;

namespace WorkrsBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize]
    public class ServerController : ControllerBase
    {
        ISharedResourceHandler _sharedResourceHandler;
        public ServerController(ISharedResourceHandler sharedResourceHandler)
        {
            _sharedResourceHandler = sharedResourceHandler;
        }

        [HttpGet]
        public IActionResult PrimaryServers()
        {
            var val = _sharedResourceHandler.GetPrimaryServers();
            List<ServerDTO> servers = new List<ServerDTO>();
            foreach (var server in val) 
            {
                servers.Add(server.Value);
            }
            return Ok(servers);
        }

        [HttpPost]
        public IActionResult Create(ServerDTO newServer) 
        {
            _sharedResourceHandler.CreateServer(newServer);
            return Ok();
        }
    }
}
