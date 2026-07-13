namespace NotifyHub.Api.Settings.Dtos;

public class SystemInfoDto
{
    public bool DatabaseConnected { get; set; }
    public int DispatcherPollIntervalSeconds { get; set; }
    public int EscalationPollIntervalSeconds { get; set; }
    public int ReminderPollIntervalSeconds { get; set; }
}
