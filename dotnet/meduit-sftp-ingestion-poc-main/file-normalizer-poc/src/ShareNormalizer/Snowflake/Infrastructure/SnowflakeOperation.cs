namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal enum SnowflakeOperation
    {
        Scalar,

        Query,

        NonQuery,

        Batch,

        Transaction,

        PutFile,

        ListStage,

        RemoveStage,

        StageExists
    }
}