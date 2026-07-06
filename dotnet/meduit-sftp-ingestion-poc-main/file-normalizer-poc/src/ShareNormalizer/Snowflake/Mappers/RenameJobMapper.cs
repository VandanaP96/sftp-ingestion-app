using System.Collections.Generic;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Mappers
{
    internal static class RenameJobMapper
    {
        public static List<RenameJob> Map(
            List<string[]> rows)
        {
            List<RenameJob> jobs =
                new List<RenameJob>();

            if (rows == null || rows.Count <= 1)
                return jobs;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];

                RenameJob job =
                    new RenameJob();

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

                job.OriginalFileName =
                    row[2];

                job.CurrentFileName =
                    row[3];

                job.OriginalPath =
                    row[4];

                job.CurrentPath =
                    row[5];

                job.QuarantinePath =
                    row[6];

                job.ApprovedBy =
                    row[7];

                jobs.Add(job);
            }

            return jobs;
        }
    }
}