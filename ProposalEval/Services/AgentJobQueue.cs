using System.Threading.Channels;

namespace ProposalEval.Services;

public sealed class AgentJobQueue
{
    private readonly Channel<int> _jobs = Channel.CreateUnbounded<int>();

    public void Enqueue(int jobId) => _jobs.Writer.TryWrite(jobId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
        _jobs.Reader.ReadAllAsync(cancellationToken);
}
