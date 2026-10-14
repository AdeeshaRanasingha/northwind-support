using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(SupportDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var reachable = await db.Database.CanConnectAsync(ct);
        return StatusCode(reachable
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
    }
}
