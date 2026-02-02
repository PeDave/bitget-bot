using BitgetLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BitgetLab.Api.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ISystemMetricsService _metricsService;
    private readonly ISystemServicesService _servicesService;

    public SystemController(
        ISystemMetricsService metricsService,
        ISystemServicesService servicesService)
    {
        _metricsService = metricsService;
        _servicesService = servicesService;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _metricsService.GetMetricsAsync();
        return Ok(metrics);
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
    {
        var services = await _servicesService.GetServicesStatusAsync();
        return Ok(services);
    }
}
