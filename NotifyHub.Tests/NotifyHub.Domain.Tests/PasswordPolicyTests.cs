using NotifyHub.Domain.Validation;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Str0ng&Pass")]
    [InlineData("C0mplex#Word")]
    public void IsValid_AcceptsCompliantPasswords(string password)
    {
        var valid = PasswordPolicy.IsValid(password, out var failures);

        Assert.True(valid);
        Assert.Empty(failures);
    }

    [Fact]
    public void IsValid_RejectsNullOrEmpty()
    {
        var valid = PasswordPolicy.IsValid(null, out var failures);

        Assert.False(valid);
        Assert.Single(failures);
    }

    [Fact]
    public void IsValid_RejectsTooShort()
    {
        var valid = PasswordPolicy.IsValid("Ab1!", out var failures);

        Assert.False(valid);
        Assert.Contains(failures, f => f.Contains("8 characters"));
    }

    [Fact]
    public void IsValid_RejectsMissingUppercase()
    {
        var valid = PasswordPolicy.IsValid("abcdefg1!", out var failures);

        Assert.False(valid);
        Assert.Contains(failures, f => f.Contains("uppercase"));
    }

    [Fact]
    public void IsValid_RejectsMissingLowercase()
    {
        var valid = PasswordPolicy.IsValid("ABCDEFG1!", out var failures);

        Assert.False(valid);
        Assert.Contains(failures, f => f.Contains("lowercase"));
    }

    [Fact]
    public void IsValid_RejectsMissingNumber()
    {
        var valid = PasswordPolicy.IsValid("Abcdefgh!", out var failures);

        Assert.False(valid);
        Assert.Contains(failures, f => f.Contains("number"));
    }

    [Fact]
    public void IsValid_RejectsMissingSymbol()
    {
        var valid = PasswordPolicy.IsValid("Abcdefg1", out var failures);

        Assert.False(valid);
        Assert.Contains(failures, f => f.Contains("symbol"));
    }

    [Fact]
    public void IsValid_ReportsAllFailuresAtOnce()
    {
        var valid = PasswordPolicy.IsValid("abc", out var failures);

        Assert.False(valid);
        Assert.True(failures.Count > 1);
    }
}
