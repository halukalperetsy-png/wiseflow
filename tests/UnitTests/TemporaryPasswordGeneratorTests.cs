using CommerceOps.Api.Modules.Identity.Authentication;

namespace CommerceOps.UnitTests;

public sealed class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void A_generated_password_has_the_documented_length() =>
        Assert.Equal(TemporaryPasswordGenerator.Length, TemporaryPasswordGenerator.Generate().Length);

    [Fact]
    public void A_generated_password_is_long_enough_for_the_password_policy() =>
        Assert.True(TemporaryPasswordGenerator.Length >= PasswordRules.MinimumLength);

    /// <summary>
    /// The password is read out loud or copied by hand, so characters that are
    /// easy to confuse are excluded.
    /// </summary>
    [Theory]
    [InlineData('O')]
    [InlineData('0')]
    [InlineData('I')]
    [InlineData('l')]
    [InlineData('1')]
    public void Confusable_characters_are_not_in_the_alphabet(char confusable) =>
        Assert.DoesNotContain(confusable, TemporaryPasswordGenerator.Alphabet);

    [Fact]
    public void Generated_passwords_use_only_the_declared_alphabet()
    {
        var password = TemporaryPasswordGenerator.Generate();

        Assert.All(password, character =>
            Assert.Contains(character, TemporaryPasswordGenerator.Alphabet));
    }

    [Fact]
    public void Two_generated_passwords_differ() =>
        Assert.NotEqual(TemporaryPasswordGenerator.Generate(), TemporaryPasswordGenerator.Generate());
}
