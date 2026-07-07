using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meduit.ShareNormalizer.Snowflake.Helpers
{
    /// <summary>
    /// Discovers folders from the normalized directory.
    ///
    /// Structure:
    ///
    /// NormalizedRoot
    ///     ├── MCD1
    ///     │      ├── ClientA
    ///     │      │      ├──2026-01
    ///     │      │      └──2026-02
    ///     │
    ///     ├── MCD2
    ///     └── MCD3
    /// </summary>
    internal sealed class FileDiscoveryHelper
    {
        private readonly Config _config;

        public FileDiscoveryHelper(Config config)
        {
            _config = config;
        }

        /// <summary>
        /// Discovers every Year-Month folder.
        /// </summary>
        public List<DiscoveredFolder> Discover()
        {
            List<DiscoveredFolder> folders =
                new List<DiscoveredFolder>();

            if (!Directory.Exists(_config.NormalizedRoot))
                return folders;

            foreach (string systemFolder in
                Directory.GetDirectories(_config.NormalizedRoot))
            {
                string system =
                    Path.GetFileName(systemFolder);

                foreach (string clientFolder in
                    Directory.GetDirectories(systemFolder))
                {
                    string client =
                        Path.GetFileName(clientFolder);

                    foreach (string yearMonthFolder in
                        Directory.GetDirectories(clientFolder))
                    {
                        DirectoryInfo info =
                            new DirectoryInfo(yearMonthFolder);

                        FileInfo[] files =
                            info.GetFiles("*",
                                SearchOption.TopDirectoryOnly);

                        folders.Add(
                            new DiscoveredFolder
                            {
                                SourceSystem = system,

                                ClientName = client,

                                FolderName = info.Name,

                                FolderPath = info.FullName,

                                Files = files.ToList()
                            });
                    }
                }
            }

            return folders;
        }
    }

    internal sealed class DiscoveredFolder
    {
        public string SourceSystem;

        public string ClientName;

        public string FolderName;

        public string FolderPath;

        public List<FileInfo> Files;
    }
}