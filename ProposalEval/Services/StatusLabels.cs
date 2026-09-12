using ProposalEval.Data;

namespace ProposalEval.Services;

public static class StatusLabels
{
    public static string Rfq(RfqStatus status) => status switch
    {
        RfqStatus.Draft => "Draft",
        RfqStatus.Ready => "Ready",
        RfqStatus.Evaluating => "Evaluating",
        RfqStatus.Completed => "Completed",
        _ => status.ToString()
    };

    public static string Vendor(VendorStatus status) => status switch
    {
        VendorStatus.Draft => "Draft",
        VendorStatus.Ready => "Ready",
        VendorStatus.Summarizing => "Summarizing",
        VendorStatus.AwaitingSummaryReview => "Awaiting summary review",
        VendorStatus.SummaryAccepted => "Summary accepted",
        VendorStatus.SummaryRejected => "Summary rejected",
        VendorStatus.Scoring => "Scoring",
        VendorStatus.AwaitingScoreReview => "Awaiting score review",
        VendorStatus.Scored => "Scored",
        VendorStatus.Approved => "Approved",
        VendorStatus.ScoreRejected => "Scores rejected",
        _ => status.ToString()
    };

    public static string Job(JobStatus status) => status switch
    {
        JobStatus.Queued => "Queued",
        JobStatus.Running => "Running",
        JobStatus.WaitingReview => "Waiting review",
        JobStatus.Completed => "Completed",
        JobStatus.Failed => "Failed",
        JobStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    public static string Run(RunStatus status) => status switch
    {
        RunStatus.Requested => "Requested",
        RunStatus.Running => "Running",
        RunStatus.WaitingSummaryReview => "Waiting summary review",
        RunStatus.Scoring => "Scoring",
        RunStatus.WaitingScoreReview => "Waiting score review",
        RunStatus.Completed => "Completed",
        RunStatus.Failed => "Failed",
        _ => status.ToString()
    };

    public static string VendorDocument(VendorDocumentKind kind) => kind switch
    {
        VendorDocumentKind.Technical => "Technical proposal",
        VendorDocumentKind.Financial => "Financial proposal",
        VendorDocumentKind.Cvs => "CVs",
        VendorDocumentKind.Portfolio => "Portfolio",
        VendorDocumentKind.Permit => "Government permit",
        VendorDocumentKind.Other => "Other",
        _ => kind.ToString()
    };

    public static string Score(ScoreStatus status) => status switch
    {
        ScoreStatus.Draft => "Draft",
        ScoreStatus.Approved => "Approved",
        ScoreStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static string Summary(SummaryStatus status) => status switch
    {
        SummaryStatus.Draft => "Draft",
        SummaryStatus.Accepted => "Accepted",
        SummaryStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    public static string RfqDocument(RfqDocumentKind kind) => kind switch
    {
        RfqDocumentKind.Tor => "TOR",
        RfqDocumentKind.ScoringStrategy => "Scoring strategy",
        _ => kind.ToString()
    };

    public static string Badge(string status) => status switch
    {
        "Draft" => "badge-draft",
        "Ready" => "badge-ready",
        "Evaluating" or "Summarizing" or "Scoring" or "Queued" or "Running" or "Requested" => "badge-live",
        "Awaiting summary review" or "Awaiting score review" or "Waiting review"
            or "Waiting summary review" or "Waiting score review" => "badge-wait",
        "Summary accepted" or "Accepted" or "Scored" or "Approved" or "Completed" => "badge-ok",
        "Summary rejected" or "Scores rejected" or "Rejected" or "Failed" or "Cancelled" => "badge-bad",
        _ => "badge-draft"
    };
}
