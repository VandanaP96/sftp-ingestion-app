using System.Collections.Generic;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Mappers
{
    internal static class StageUploadJobMapper
    {
        public static List<StageUploadJob> Map(
            List<string[]> rows)
        {
            List<StageUploadJob> jobs =
                new List<StageUploadJob>();

            if (rows == null)
                return jobs;

            if (rows.Count <= 1)
                return jobs;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row =
                    rows[i];

                StageUploadJob job =
                    new StageUploadJob();

                long value;

                long.TryParse(
                    row[0],
                    out value);

                job.DetailId =
                    value;

                long.TryParse(
                    row[1],
                    out value);

                job.FolderId =
                    value;

                job.CurrentFileName =
                    row[2];

                job.CurrentPath =
                    row[3];

                job.StagePath =
                    row[4];

                job.ArchivePath =
                    row[5];

                jobs.Add(job);
            }

            return jobs;
        }
    }
}