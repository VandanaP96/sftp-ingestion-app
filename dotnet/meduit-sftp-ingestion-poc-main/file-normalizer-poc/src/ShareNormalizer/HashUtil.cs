using System;
using System.IO;
using System.Security.Cryptography;

namespace Meduit.ShareNormalizer
{
    /// <summary>Content hashing. SHA-256 is the file identity used for cross-run, cross-folder dedup.</summary>
    internal static class HashUtil
    {
        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }
    }
}
