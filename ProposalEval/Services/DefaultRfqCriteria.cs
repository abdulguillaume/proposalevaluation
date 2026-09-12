using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;

namespace ProposalEval.Services;

public static class DefaultRfqCriteria
{
    public static async Task EnsureAsync(AppDbContext db, int rfqId, CancellationToken cancellationToken)
    {
        if (await db.RfqCriteria.AnyAsync(c => c.RfqId == rfqId, cancellationToken))
            return;

        db.RfqCriteria.AddRange(
            new RfqCriterion { RfqId = rfqId, Code = "C1", Name = "Proposal plan and fit", Weight = 30m, SortOrder = 1 },
            new RfqCriterion { RfqId = rfqId, Code = "C2a", Name = "Valid government permit", GroupCode = "C2", GroupWeight = 20m, SortOrder = 2 },
            new RfqCriterion { RfqId = rfqId, Code = "C2b", Name = "Specific experience and expertise", GroupCode = "C2", GroupWeight = 20m, SortOrder = 3 },
            new RfqCriterion { RfqId = rfqId, Code = "C2c", Name = "Organization and staffing", GroupCode = "C2", GroupWeight = 20m, SortOrder = 4 },
            new RfqCriterion { RfqId = rfqId, Code = "C3", Name = "Experience of the firm", Weight = 20m, SortOrder = 5 });
        await db.SaveChangesAsync(cancellationToken);
    }
}
