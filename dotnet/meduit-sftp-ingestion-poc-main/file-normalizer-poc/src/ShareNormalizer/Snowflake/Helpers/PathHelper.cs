using System;
using System.IO;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    /// <summary>
    /// Helper methods for building folder paths used by the
    /// Snowflake ingestion process.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// Returns the relative path starting from the source system.
        ///
        /// Example:
        /// E:\Normalized\organized\MCD1\ClientA\2026-01\File1.txt
        ///
        /// becomes
        ///
        /// MCD1\ClientA\2026-01\File1.txt
        /// </summary>
        public static string GetRelativePath(
            string rootPath,
            string fullPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentNullException("rootPath");

            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentNullException("fullPath");

            if (!fullPath.StartsWith(
                    rootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "File does not belong to configured root.");
            }

            string relative =
                fullPath.Substring(rootPath.Length);

            return relative.TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Returns archive file path while preserving
        /// the folder hierarchy.
        /// </summary>
        public static string BuildArchivePath(
            string archiveRoot,
            string rootPath,
            string fullPath)
        {
            string relative =
                GetRelativePath(rootPath, fullPath);

            return Path.Combine(
                archiveRoot,
                relative);
        }

        /// <summary>
        /// Returns quarantine file path while preserving
        /// the folder hierarchy.
        /// </summary>
        public static string BuildQuarantinePath(
            string quarantineRoot,
            string rootPath,
            string fullPath)
        {
            string relative =
                GetRelativePath(rootPath, fullPath);

            return Path.Combine(
                quarantineRoot,
                relative);
        }

        /// <summary>
        /// Returns Snowflake stage path.
        ///
        /// Example:
        ///
        /// MCD1\ClientA\2026-01\File.txt
        ///
        /// becomes
        ///
        /// MCD1/ClientA/2026-01
        /// </summary>
        public static string BuildStageFolder(
            string rootPath,
            string fullPath)
        {
            string relative =
                GetRelativePath(rootPath, fullPath);

            string directory =
                Path.GetDirectoryName(relative);

            if (string.IsNullOrWhiteSpace(directory))
                return "";

            return directory.Replace("\\", "/");
        }

        /// <summary>
        /// Ensures the parent directory exists.
        /// </summary>
        public static void EnsureParentFolder(
            string filePath)
        {
            string folder =
                Path.GetDirectoryName(filePath);

            if (string.IsNullOrWhiteSpace(folder))
                return;

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        /// <summary>
        /// Returns file extension without dot.
        /// </summary>
        public static string GetExtension(
            string fileName)
        {
            string extension =
                Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(extension))
                return "";

            return extension.TrimStart('.')
                            .ToUpperInvariant();
        }

        /// <summary>
        /// Returns filename only.
        /// </summary>
        public static string GetFileName(
            string path)
        {
            return Path.GetFileName(path);
        }

        /// <summary>
        /// Returns folder name.
        /// </summary>
        public static string GetFolderName(
            string path)
        {
            return new DirectoryInfo(path).Name;
        }

        /// <summary>
        /// Returns parent folder.
        /// </summary>
        public static string GetParentFolder(
            string path)
        {
            DirectoryInfo info =
                Directory.GetParent(path);

            return info == null
                ? ""
                : info.FullName;
        }
    }
}