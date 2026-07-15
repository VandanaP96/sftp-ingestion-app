using System;
using System.Collections.Generic;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal interface ISnowflakeExecutor : IDisposable
    {
        bool ExecuteSql(string sql);

        string ExecuteScalar(string sql);

        List<string[]> ExecuteQueryRows(string sql);

        HashSet<string> ExecuteHashSet(string sql);

        bool ExecuteBatch(params string[] sqlStatements);

        bool ExecuteTransaction(params string[] sqlStatements);

        bool PutFile(
            string localFile,
            string stageFolder);

        string ListStage(
            string folder);

        bool RemoveStageFile(
            string stageFile);

        bool StageFileExists(
            string stageFolder,
            string fileName);

        bool TestConnection();

        void BeginTransaction();

void CommitTransaction();

void RollbackTransaction();
    }
}   