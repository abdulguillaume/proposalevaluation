namespace ProposalEval.Data;

public enum RfqStatus
{
    Draft = 0,
    Ready = 1,
    Evaluating = 2,
    Completed = 3
}

public enum RfqDocumentKind
{
    Tor = 0,
    ScoringStrategy = 1
}

public enum VendorStatus
{
    Draft = 0,
    Ready = 1,
    Summarizing = 2,
    AwaitingSummaryReview = 3,
    SummaryAccepted = 4,
    SummaryRejected = 5,
    Scoring = 6,
    AwaitingScoreReview = 7,
        Scored = 8,
        Approved = 9,
        ScoreRejected = 10
    }

public enum VendorDocumentKind
{
    Technical = 0,
    Financial = 1,
    Cvs = 2,
    Portfolio = 3,
    Permit = 4,
    Other = 5
}

public enum JobType
{
    Summarize = 0,
    Score = 1
}

public enum JobStatus
{
    Queued = 0,
    Running = 1,
    WaitingReview = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum RunStatus
{
    Requested = 0,
    Running = 1,
    WaitingSummaryReview = 2,
    Scoring = 3,
    WaitingScoreReview = 4,
    Completed = 5,
    Failed = 6
}

public enum SummaryStatus
{
    Draft = 0,
    Accepted = 1,
    Rejected = 2
}

public enum ScoreStatus
{
    Draft = 0,
    Approved = 1,
    Rejected = 2
}
