using System;
using System.IO;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    /// <summary>
    /// Handles all physical file movement for the
    /// Snowflake ingestion workflow.
    ///
    /// InventoryService
    /// RenameService
    /// StageUploadService
    ///
    /// should use this helper instead of directly
    /// calling File.Move().
    /// </summary>
    internal static class FileMovementHelper
    {
        /// <summary>
        /// Ensures the destination directory exists.
        /// </summary>
        public static void EnsureFolder(
            string fullFilePath)
        {
            string folder =
                Path.GetDirectoryName(
                    fullFilePath);

            if (string.IsNullOrWhiteSpace(folder))
                return;

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        /// <summary>
        /// Moves a file.
        /// If destination already exists,
        /// generates a unique filename.
        /// </summary>
        public static string MoveFile(
            string source,
            string destination)
        {
            EnsureFolder(destination);

            destination =
                GetUniqueFileName(
                    destination);

            File.Move(
                source,
                destination);

            return destination;
        }

        /// <summary>
        /// Renames a file inside the same folder.
        /// </summary>
        public static string RenameFile(
            string currentFile,
            string newFileName)
        {
            string folder =
                Path.GetDirectoryName(
                    currentFile);

            string destination =
                Path.Combine(
                    folder,
                    newFileName);

            destination =
                GetUniqueFileName(
                    destination);

            File.Move(
                currentFile,
                destination);

            return destination;
        }

        /// <summary>
        /// Returns unique filename.
        /// Prevents overwriting.
        /// </summary>
        private static string GetUniqueFileName(
            string file)
        {
            if (!File.Exists(file))
                return file;

            string folder =
                Path.GetDirectoryName(file);

            string name =
                Path.GetFileNameWithoutExtension(file);

            string extension =
                Path.GetExtension(file);

            int counter = 1;

            while (true)
            {
                string candidate =
                    Path.Combine(
                        folder,
                        string.Format(
                            "{0}_{1}{2}",
                            name,
                            counter,
                            extension));

                if (!File.Exists(candidate))
                    return candidate;

                counter++;
            }
        }

        /// <summary>
        /// Deletes file if exists.
        /// </summary>
        public static void Delete(
            string file)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Returns whether file exists.
        /// </summary>
        public static bool Exists(
            string file)
        {
            return File.Exists(file);
        }
    }
}