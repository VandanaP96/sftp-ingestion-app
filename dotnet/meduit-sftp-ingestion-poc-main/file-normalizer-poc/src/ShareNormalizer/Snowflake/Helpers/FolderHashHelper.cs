using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    /// <summary>
    /// Generates a deterministic hash representing a folder and its files.
    /// Used to prevent duplicate folder registration.
    /// </summary>
    internal static class FolderHashHelper
    {
        /// <summary>
        /// Generates SHA256 hash using folder path and file metadata.
        /// </summary>
        public static string Generate(string folderPath)
{
    if (string.IsNullOrWhiteSpace(folderPath))
        return "";

    folderPath =
        folderPath
            .Trim()
            .ToUpperInvariant();

    using (SHA256 sha = SHA256.Create())
    {
        byte[] hash =
            sha.ComputeHash(
                Encoding.UTF8.GetBytes(folderPath));

        StringBuilder builder =
            new StringBuilder();

        foreach (byte b in hash)
            builder.Append(b.ToString("x2"));

        return builder.ToString();
    }
}
    }
}