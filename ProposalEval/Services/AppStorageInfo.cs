namespace ProposalEval.Services;

public sealed class AppStorageInfo
{
    public bool UsingSqlServer { get; init; }
    public string FileStorageLabel { get; init; } = "Azure Blob";
    public string DatabaseLabel => UsingSqlServer ? "SQL Server" : "In-memory (set PROPOSAL_EVAL_SQL to use SQL Server)";
}
