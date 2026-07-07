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
            if (!Directory.Exists(folderPath))
                return string.Empty;

            StringBuilder builder = new StringBuilder();

            builder.Append(folderPath.ToUpperInvariant());

            FileInfo[] files =
                new DirectoryInfo(folderPath)
                    .GetFiles("*", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f.Name)
                    .ToArray();

            foreach (FileInfo file in files)
            {
                builder.Append(file.Name);

                builder.Append("|");

                builder.Append(file.Length);

                builder.Append("|");

                builder.Append(file.LastWriteTimeUtc.Ticks);

                builder.Append("|");
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes =
                    Encoding.UTF8.GetBytes(builder.ToString());

                byte[] hash =
                    sha.ComputeHash(bytes);

                StringBuilder result = new StringBuilder();

                foreach (byte b in hash)
                    result.Append(b.ToString("x2"));

                return result.ToString();
            }
        }
    }
}