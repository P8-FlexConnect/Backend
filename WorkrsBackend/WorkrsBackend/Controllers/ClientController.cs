using Microsoft.AspNetCore.Authorization;
using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;

namespace WorkrsBackend.Controllers
{
    [Route("api/[controller]/[Action]")]
    [ApiController]
    [Authorize]
    public class ClientController : ControllerBase
    {
        ISharedResourceHandler _sharedResourceHandler;
        IIdentity? _identity;
        public ClientController(ISharedResourceHandler sharedResourceHandler, IHttpContextAccessor httpContextAccessor)
        {
            _sharedResourceHandler = sharedResourceHandler;
            _identity = httpContextAccessor?.HttpContext?.User?.Identity;
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult CreateClient(ClientDTO client)
        {
            _sharedResourceHandler.AddClientToClientDHT(client);
            return Ok();
        }

        [HttpGet]
        public ActionResult GetClient() 
        {
            if(_identity != null)
            {
                ClientDTO? c = _sharedResourceHandler.FindClientByUserName(_identity.Name);
                if (c != null)
                {
                    return Ok(c);
                }
            }

            return NotFound();
        }

    }
}