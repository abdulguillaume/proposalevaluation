namespace ProposalEval.AgentHost;

public sealed class McpProcessOptions
{
    public string Command { get; }
    public IReadOnlyList<string> Arguments { get; }

    public McpProcessOptions()
    {
        var configured = Environment.GetEnvironmentVariable("PROPOSAL_EVAL_MCP_COMMAND");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Command = configured.Trim();
            Arguments = SplitArgs(Environment.GetEnvironmentVariable("PROPOSAL_EVAL_MCP_ARGS"));
            return;
        }

        var exe = FindMcpExecutable();
        if (exe is null)
        {
            Command = "";
            Arguments = [];
            return;
        }

        Command = exe;
        Arguments = [];
    }

    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(Command) || !File.Exists(Command))
            throw new InvalidOperationException("Set PROPOSAL_EVAL_MCP_COMMAND to the ProposalEval.Mcp executable.");
    }

    private static string? FindMcpExecutable()
    {
        var fileName = OperatingSystem.IsWindows() ? "ProposalEval.Mcp.exe" : "ProposalEval.Mcp";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProposalEval.Mcp", "bin", "Debug", "net9.0", fileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProposalEval.Mcp", "bin", "Release", "net9.0", fileName))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IReadOnlyList<string> SplitArgs(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
