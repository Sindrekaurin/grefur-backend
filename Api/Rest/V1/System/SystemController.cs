using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using System.Reflection;

namespace grefurBackend.Api.Rest.V1.Diagnostics;

[ApiController]
[Route("api/rest/v1/system")]
[Authorize]
public class SystemController : ControllerBase
{
    // Lightweight endpoint for the Footer's minute-by-minute ping
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping()
    {
        return Ok(new { success = true });
    }

    [HttpGet]
    public IActionResult GetApiUrls()
    {
        var process = Process.GetCurrentProcess();

        var controllers = Assembly.GetExecutingAssembly().GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => new
            {
                Controller = type.Name.Replace("Controller", ""),
                Routes = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes<HttpGetAttribute>().Any())
                    .Select(m => m.Name)
            })
            .ToList();

        var totalCpuUsage = process.TotalProcessorTime.TotalSeconds / Environment.ProcessorCount;

        return Ok(new
        {
            success = true,
            system = new
            {
                cpuSecondsUsed = totalCpuUsage,
                memoryMb = process.WorkingSet64 / (1024.0 * 1024.0),
                upTimeSeconds = (global::System.DateTime.Now - process.StartTime).TotalSeconds
            },
            message = "Grefur API Status",
            controllers = controllers
        });
    }

    [HttpGet("stats")]
    public IActionResult GetSystemStats()
    {
        var process = Process.GetCurrentProcess();
        return Ok(new
        {
            success = true,
            cpuSecondsUsed = process.TotalProcessorTime.TotalSeconds,
            memoryMb = process.WorkingSet64 / (1024.0 * 1024.0),
            upTimeSeconds = (global::System.DateTime.Now - process.StartTime).TotalSeconds,
            processorCount = global::System.Environment.ProcessorCount
        });
    }
}