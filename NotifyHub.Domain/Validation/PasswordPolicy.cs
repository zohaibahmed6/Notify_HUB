using System.Text.RegularExpressions;

namespace NotifyHub.Domain.Validation;

/// Enforces §7's password policy: minimum 8 characters, high complexity
/// (must contain upper, lower, number, and symbol).
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static bool IsValid(string? password, out IReadOnlyList<string> failures)
    {
        var problems = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            problems.Add("Password is required.");
            failures = problems;
            return false;
        }

        if (password.Length < MinLength)
            problems.Add($"Password must be at least {MinLength} characters long.");
        if (!Regex.IsMatch(password, "[a-z]"))
            problems.Add("Password must contain a lowercase letter.");
        if (!Regex.IsMatch(password, "[A-Z]"))
            problems.Add("Password must contain an uppercase letter.");
        if (!Regex.IsMatch(password, "[0-9]"))
            problems.Add("Password must contain a number.");
        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            problems.Add("Password must contain a symbol.");

        failures = problems;
        return problems.Count == 0;
    }
}
