using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EList.Filestorage.Api.Controllers
{
    /// <summary>Liveness/readiness for probes and ops.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api")]
    public class HealthController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult GetHealth() => Ok(new { status = "ok" });
    }
}
