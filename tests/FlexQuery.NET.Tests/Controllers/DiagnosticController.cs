using Microsoft.AspNetCore.Mvc;

namespace FlexQuery.NET.Tests.Controllers;

[ApiController]
[Route("api/diag")]
public class DiagnosticController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok("pong");
}