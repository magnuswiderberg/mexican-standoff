using System.Security.Cryptography;
using System.Text;

namespace MexicanStandoff.Server.Games;

/// <summary>
/// The secrets that gate a game: a player's seat token and the monitor token.
/// Everyone on the party wifi knows the game code (it is read aloud), so these
/// tokens are the only thing separating a player from someone else's seat or
/// from the game's control buttons — they come from the CSPRNG and are compared
/// without an early exit.
/// </summary>
public static class Tokens
{
    /// <summary>A fresh 128-bit secret, hex-encoded.</summary>
    public static string New() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    /// <summary>Constant-time equality; false for a null or differently sized candidate.</summary>
    public static bool Equal(string? secret, string? candidate)
    {
        if (secret is null || candidate is null)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(candidate));
    }
}
