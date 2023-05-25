using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;
using WorkrsBackend.DataHandling;
using WorkrsBackend.DTOs;

namespace WorkrsBackend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]

    [Authorize]
    public class LocationController : ControllerBase
    {
        ISharedResourceHandler _sharedResourceHandler;
        IIdentity? _identity;

        public LocationController(ISharedResourceHandler sharedResourceHandler, IHttpContextAccessor httpContextAccessor)
        {
            _sharedResourceHandler = sharedResourceHandler;
            _identity = httpContextAccessor?.HttpContext?.User?.Identity;
        }

        [HttpGet]
        public ActionResult<List<LocationDTO>> Locations() 
        {
            if (_identity != null)
            {
                return Ok(_sharedResourceHandler.GetLocations());
            }
            return NotFound();
        }
    }
}
