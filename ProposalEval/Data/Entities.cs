namespace ProposalEval.Data;

public sealed class Rfq
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public RfqStatus Status { get; set; } = RfqStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<RfqDocument> Documents { get; set; } = new List<RfqDocument>();
    public ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
    public ICollection<RfqCriterion> Criteria { get; set; } = new List<RfqCriterion>();
    public ICollection<EvaluationRun> Runs { get; set; } = new List<EvaluationRun>();
}

public sealed class RfqDocument
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public RfqDocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";
    public DateTime UploadedAt { get; set; }

    public Rfq Rfq { get; set; } = null!;
}

public sealed class Vendor
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public string Name { get; set; } = "";
    public string? ExternalProjectId { get; set; }
    public VendorStatus Status { get; set; } = VendorStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Rfq Rfq { get; set; } = null!;
    public ICollection<VendorDocument> Documents { get; set; } = new List<VendorDocument>();
    public ICollection<ProposalSummary> Summaries { get; set; } = new List<ProposalSummary>();
    public ICollection<VendorEvaluation> Evaluations { get; set; } = new List<VendorEvaluation>();
}

public sealed class VendorDocument
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public VendorDocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";
    public DateTime UploadedAt { get; set; }

    public Vendor Vendor { get; set; } = null!;
}

public sealed class EvaluationRun
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Requested;
    public string StatusMessage { get; set; } = "";
    public string? RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Rfq Rfq { get; set; } = null!;
    public ICollection<EvaluationJob> Jobs { get; set; } = new List<EvaluationJob>();
}

public sealed class EvaluationJob
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public int RfqId { get; set; }
    public int? VendorId { get; set; }
    public JobType JobType { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string? StatusMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public EvaluationRun Run { get; set; } = null!;
    public Rfq Rfq { get; set; } = null!;
    public Vendor? Vendor { get; set; }
    public ICollection<EvaluationJobEvent> Events { get; set; } = new List<EvaluationJobEvent>();
}

public sealed class EvaluationJobEvent
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public JobStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime At { get; set; }

    public EvaluationJob Job { get; set; } = null!;
}

public sealed class ProposalSummary
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int VendorId { get; set; }
    public string Body { get; set; } = "";
    public SummaryStatus Status { get; set; } = SummaryStatus.Draft;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public EvaluationJob Job { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
}

public sealed class RfqCriterion
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal? Weight { get; set; }
    public string? GroupCode { get; set; }
    public decimal? GroupWeight { get; set; }
    public int SortOrder { get; set; }

    public Rfq Rfq { get; set; } = null!;
    public ICollection<VendorScore> Scores { get; set; } = new List<VendorScore>();
}

public sealed class VendorEvaluation
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int VendorId { get; set; }
    public bool MandatoryPass { get; set; }
    public string? Recommendation { get; set; }
    public ScoreStatus Status { get; set; } = ScoreStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public EvaluationJob Job { get; set; } = null!;
    public Vendor Vendor { get; set; } = null!;
    public ICollection<VendorScore> Scores { get; set; } = new List<VendorScore>();

    public decimal Tws
    {
        get
        {
            var direct = Scores
                .Where(s => s.Criterion.Weight is not null && string.IsNullOrEmpty(s.Criterion.GroupCode))
                .Sum(s => s.Score * s.Criterion.Weight!.Value);

            var grouped = Scores
                .Where(s => !string.IsNullOrEmpty(s.Criterion.GroupCode))
                .GroupBy(s => s.Criterion.GroupCode)
                .Sum(g => Math.Round(g.Average(s => s.Score), 2) * (g.First().Criterion.GroupWeight ?? 0m));

            return Math.Round(direct + grouped, 2);
        }
    }

    public bool TechnicalPass => Tws >= 49.00m;
}

public sealed class VendorScore
{
    public int Id { get; set; }
    public int VendorEvaluationId { get; set; }
    public int RfqCriterionId { get; set; }
    public decimal Score { get; set; }
    public string? Justification { get; set; }

    public VendorEvaluation Evaluation { get; set; } = null!;
    public RfqCriterion Criterion { get; set; } = null!;
}
