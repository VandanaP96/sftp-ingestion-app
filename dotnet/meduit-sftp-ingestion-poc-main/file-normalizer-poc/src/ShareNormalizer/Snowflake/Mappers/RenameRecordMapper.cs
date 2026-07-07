using System.Collections.Generic;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Mappers
{
    internal static class RenameRecordMapper
    {
        public static List<RenameRecord> Map(
            List<string[]> rows)
        {
            List<RenameRecord> list =
                new List<RenameRecord>();

            if (rows == null || rows.Count <= 1)
                return list;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];

                RenameRecord record =
                    new RenameRecord();

                long id;

                long.TryParse(
                    row[0],
                    out id);

                record.DetailId = id;

                record.OriginalFileName =
                    row[1];

                record.CurrentFileName =
                    row[2];

                record.OriginalPath =
                    row[3];

                record.CurrentPath =
                    row[4];

                record.QuarantinePath =
                    row[5];

                record.ApprovedBy =
                    row[6];

                list.Add(record);
            }

            return list;
        }
    }
}