namespace ProposalEval.Api;

public sealed record RfqListItemDto(int Id, string Number, string Title, string Status, int VendorCount, bool HasTor, bool HasStrategy);

public sealed record RfqDetailDto(
    int Id,
    string Number,
    string Title,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DocumentDto> Documents,
    int VendorCount);

public sealed record CreateRfqRequest(string Number, string Title, string? Description);

public sealed record VendorListItemDto(int Id, string Name, string? ExternalProjectId, string Status, int DocumentCount);

public sealed record VendorDetailDto(
    int Id,
    int RfqId,
    string Name,
    string? ExternalProjectId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<DocumentDto> Documents);

public sealed record CreateVendorRequest(string Name, string? ExternalProjectId);

public sealed record DocumentDto(int Id, string Source, string Kind, string FileName, long SizeBytes, DateTime UploadedAt);

public sealed record DocumentTextDto(int Id, string Source, string Kind, string FileName, string Text);

public sealed record JobDto(
    int Id,
    int RunId,
    int RfqId,
    int? VendorId,
    string JobType,
    string Status,
    string? StatusMessage,
    DateTime CreatedAt);

public sealed record VendorProfileDto(int Id, int RfqId, string Name, string? ExternalProjectId, string Status);

public sealed record CriterionDto(int Id, string Code, string Name, decimal? Weight, string? GroupCode, decimal? GroupWeight, int SortOrder);

public sealed record UpsertCriteriaRequest(IReadOnlyList<CriterionDto> Items);

public sealed record SaveSummaryRequest(string Body);

public sealed record RejectSummaryRequest(string? Reason);

public sealed record SummaryDto(int Id, int JobId, int VendorId, string Body, string Status, string? RejectionReason, DateTime CreatedAt);

public sealed record SaveScoreItemRequest(int RfqCriterionId, decimal Score, string? Justification, string? Code = null);

public sealed record SaveScoresRequest(bool MandatoryPass, string? Recommendation, IReadOnlyList<SaveScoreItemRequest> Items);

public sealed record VendorEvaluationDto(
    int Id,
    int JobId,
    int VendorId,
    bool MandatoryPass,
    string? Recommendation,
    string Status,
    decimal Tws,
    bool TechnicalPass,
    IReadOnlyList<VendorScoreDto> Scores);

public sealed record VendorScoreDto(int Id, int RfqCriterionId, string CriterionCode, string CriterionName, decimal Score, string? Justification);

public sealed record EvaluateResultDto(int RunId, string Status, string StatusMessage, IReadOnlyList<JobDto> Jobs);

public sealed record RankingDto(
    int RfqId,
    string Number,
    string Title,
    IReadOnlyList<RankedVendorDto> Scored,
    IReadOnlyList<UnscoredVendorDto> InProgress,
    IReadOnlyList<UnscoredVendorDto> NotAccepted);

public sealed record RankedVendorDto(
    int? EligibleRank,
    int VendorId,
    string Name,
    string? ExternalProjectId,
    int JobId,
    decimal Tws,
    bool TechnicalPass,
    bool MandatoryPass,
    bool Eligible,
    string? Recommendation,
    string Status);

public sealed record UnscoredVendorDto(
    int VendorId,
    string Name,
    string? ExternalProjectId,
    string Status,
    string? Reason,
    int? SummaryJobId,
    int? ScoreJobId);

public sealed record LookupItemDto(string Value, string Label);

public sealed record PreparedPromptDto(int JobId, string Agent, string JobType, string Prompt);
