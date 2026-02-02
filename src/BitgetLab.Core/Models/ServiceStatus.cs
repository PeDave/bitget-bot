namespace BitgetLab.Core.Models;

/// <summary>
/// Service status info from systemctl
/// </summary>
public class ServiceStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // active, inactive, failed, etc.
    public bool IsRunning { get; set; }
    public string SubState { get; set; } = string.Empty; // running, dead, etc.
}
