using Microsoft.AspNetCore.Mvc;

namespace CityCore.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() 
            => Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }
}
