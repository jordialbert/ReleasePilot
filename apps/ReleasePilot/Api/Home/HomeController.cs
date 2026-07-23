using Microsoft.AspNetCore.Mvc;

namespace ReleasePilot.Api.Home;

[ApiController]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult GetHome()
    {
        return Redirect("/docs");
    }
}
