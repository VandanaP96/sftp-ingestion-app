using System.Collections.Generic;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Mappers
{
    internal static class StageRecordMapper
    {
        public static List<StageRecord> Map(
            List<string[]> rows)
        {
            List<StageRecord> list =
                new List<StageRecord>();

            if (rows == null || rows.Count <= 1)
                return list;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];

                StageRecord record =
                    new StageRecord();

                long id;

                long.TryParse(
                    row[0],
                    out id);

                record.DetailId =
                    id;

                record.CurrentFileName =
                    row[1];

                record.CurrentPath =
                    row[2];

                record.StagePath =
                    row[3];

                record.ArchivePath =
                    row[4];

                list.Add(record);
            }

            return list;
        }
    }
}