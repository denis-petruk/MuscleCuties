using MuscleCuties.Core.Domain.Auth;

namespace MuscleCuties.Core.Tests;

public sealed class AuthInputValidatorContactEmailTests
{
    [Theory]
    [InlineData("member@example.com")]
    [InlineData(" member@example.com ")]
    [InlineData("first.last@mail.example.co.uk")]
    public void Contact_Email_Accepts_A_Complete_Address(string value)
    {
        Assert.True(AuthInputValidator.IsValidContactEmail(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("member")]
    [InlineData("member@localhost")]
    [InlineData("member@@example.com")]
    [InlineData("Member <member@example.com>")]
    public void Contact_Email_Rejects_Incomplete_Or_Display_Form_Addresses(string? value)
    {
        Assert.False(AuthInputValidator.IsValidContactEmail(value));
    }
}
