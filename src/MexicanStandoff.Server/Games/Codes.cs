using System.Security.Cryptography;

namespace MexicanStandoff.Server.Games;

/// <summary>
/// The short, public, human-readable codes: a game's code, and the pairing code a
/// screen shows while it waits for the host to make it the board. Neither is a
/// secret (tokens are — see <see cref="Tokens"/>); they exist to be read aloud
/// across a room and typed on a phone, so no lookalike characters (I/O/0/1).
/// </summary>
public static class Codes
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Crypto RNG: a code is short and public, but knowing one must not let anyone
    /// predict the codes handed out around it.
    /// </summary>
    public static string New(int length = 4) =>
        string.Concat(Enumerable.Range(0, length)
            .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]));
}
