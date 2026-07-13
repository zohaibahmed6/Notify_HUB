namespace NotifyHub.Domain.Entities;

/// §6/§8: generic admin-editable key-value runtime config (Quiet Hours, per-patient rate
/// limiting) — wrapped by NotifyHub.Infrastructure's SettingsService so parsing/validation
/// live in one place rather than scattered across controllers/consumers.
public class SystemSetting
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
