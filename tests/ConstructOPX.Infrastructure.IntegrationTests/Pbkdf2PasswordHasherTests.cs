using ConstructOPX.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace ConstructOPX.Infrastructure.IntegrationTests;

public sealed class Pbkdf2PasswordHasherTests
{
    private static Pbkdf2PasswordHasher Hasher(int iterations = 1_000) => new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void A_password_verifies_against_its_own_hash_and_not_against_another()
    {
        var hasher = Hasher();
        var hash = hasher.Hash("Correct-horse-7-battery");

        Assert.True(hasher.Verify(hash, "Correct-horse-7-battery"));
        Assert.False(hasher.Verify(hash, "Correct-horse-7-batterY"));
        Assert.False(hasher.Verify(hash, ""));
    }

    [Fact]
    public void The_same_password_gets_a_different_hash_every_time_because_the_salt_is_random()
    {
        var hasher = Hasher();
        Assert.NotEqual(hasher.Hash("Correct-horse-7-battery"), hasher.Hash("Correct-horse-7-battery"));
    }

    [Fact]
    public void The_hash_never_contains_the_password_and_names_its_algorithm_and_cost()
    {
        var hash = Hasher(5_000).Hash("Correct-horse-7-battery");

        Assert.DoesNotContain("Correct", hash);
        Assert.StartsWith("PBKDF2-SHA256$5000$", hash);
    }

    [Fact]
    public void A_hash_made_with_a_different_cost_still_verifies_so_the_cost_can_be_raised_later()
    {
        var old = Hasher(2_000).Hash("Correct-horse-7-battery");
        Assert.True(Hasher(600_000).Verify(old, "Correct-horse-7-battery"));
    }

    [Fact]
    public void An_unknown_account_is_checked_against_a_dummy_and_never_verifies()
    {
        Assert.False(Hasher().Verify(null, "anything at all 1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text password")]
    [InlineData("PBKDF2-SHA256$abc$salt$hash")]
    [InlineData("PBKDF2-SHA256$5$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$5000$not-base64!$aGFzaA==")]
    [InlineData("MD5$5000$c2FsdA==$aGFzaA==")]
    public void A_malformed_or_unsafe_stored_value_never_verifies(string stored) =>
        Assert.False(Hasher().Verify(stored, "Correct-horse-7-battery"));

    [Fact]
    public void An_absurdly_low_cost_is_raised_to_the_minimum() =>
        Assert.StartsWith("PBKDF2-SHA256$1000$", Hasher(1).Hash("Correct-horse-7-battery"));
}
