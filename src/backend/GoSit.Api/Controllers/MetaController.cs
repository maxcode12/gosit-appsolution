using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoSit.Api.Controllers;

/// <summary>
/// Lightweight service metadata endpoint used for smoke tests and deployment verification.
/// </summary>
[ApiController]
[Route("api/v1/meta")]
[AllowAnonymous]
public sealed class MetaController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        return Ok(new
        {
            service = "gosit-api",
            version,
            environment = environment.EnvironmentName,
        });
    }
}
