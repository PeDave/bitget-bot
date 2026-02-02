using BitgetLab.Core.Models;
using System.Diagnostics;

namespace BitgetLab.Api.Services;

public interface ISystemServicesService
{
    Task<List<ServiceStatus>> GetServicesStatusAsync();
}

public class SystemServicesService : ISystemServicesService
{
    private readonly string[] _monitoredServices = new[]
    {
        "caddy",
        "n8n",
        "postgresql"
    };

    public async Task<List<ServiceStatus>> GetServicesStatusAsync()
    {
        var statuses = new List<ServiceStatus>();

        foreach (var serviceName in _monitoredServices)
        {
            var status = await GetServiceStatusAsync(serviceName);
            statuses.Add(status);
        }

        return statuses;
    }

    private async Task<ServiceStatus> GetServiceStatusAsync(string serviceName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"is-active {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var isActive = output.Trim() == "active";

            // Get more detailed status
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"show {serviceName} --property=SubState",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var subStateOutput = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var subState = subStateOutput.Replace("SubState=", "").Trim();

            return new ServiceStatus
            {
                ServiceName = serviceName,
                Status = isActive ? "active" : "inactive",
                IsRunning = isActive,
                SubState = subState
            };
        }
        catch (Exception)
        {
            return new ServiceStatus
            {
                ServiceName = serviceName,
                Status = "unknown",
                IsRunning = false,
                SubState = "unknown"
            };
        }
    }
}
