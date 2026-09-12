using Microsoft.EntityFrameworkCore;
using ProposalEval.Api;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class RfqAppService(AppDbContext db, IFileStore files, EvaluationService evaluation)
{
    public async Task<PagedResult<RfqListItemDto>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = db.Rfqs.AsNoTracking().OrderByDescending(r => r.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RfqListItemDto(
                r.Id,
                r.Number,
                r.Title,
                r.Status.ToString(),
                r.Vendors.Count,
                r.Documents.Any(d => d.Kind == RfqDocumentKind.Tor),
                r.Documents.Any(d => d.Kind == RfqDocumentKind.ScoringStrategy)))
            .ToListAsync(cancellationToken);

        return PagedResult<RfqListItemDto>.Create(items, page, size, total);
    }

    public async Task<OpResult<RfqDetailDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs
            .AsNoTracking()
            .Include(r => r.Documents)
            .Include(r => r.Vendors)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return rfq is null
            ? OpResult<RfqDetailDto>.Fail(404, "RFQ not found.")
            : OpResult<RfqDetailDto>.Success(Map(rfq));
    }

    public async Task<OpResult<RfqDetailDto>> CreateAsync(CreateRfqRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Number) || string.IsNullOrWhiteSpace(request.Title))
            return OpResult<RfqDetailDto>.Fail(400, "Number and title are required.");

        var now = DateTime.UtcNow;
        var rfq = new Rfq
        {
            Number = request.Number.Trim(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = RfqStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Rfqs.Add(rfq);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<RfqDetailDto>.Success(Map(rfq), 201);
    }

    public async Task<OpResult<DocumentDto>> UploadDocumentAsync(int rfqId, RfqDocumentKind kind, IFormFile file, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs.Include(r => r.Documents).Include(r => r.Vendors)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq is null)
            return OpResult<DocumentDto>.Fail(404, "RFQ not found.");
        if (file.Length == 0)
            return OpResult<DocumentDto>.Fail(400, "Choose a file to upload.");

        var existing = rfq.Documents.FirstOrDefault(d => d.Kind == kind);
        if (existing is not null)
        {
            await files.DeleteAsync(BlobContainers.ProposalEvals, existing.StorageKey, cancellationToken);
            db.RfqDocuments.Remove(existing);
        }

        var key = await files.SaveAsync(BlobContainers.ProposalEvals, BlobFolders.Rfq(rfq.Id), file.FileName, file.ContentType, file.OpenReadStream(), cancellationToken);
        var doc = new RfqDocument
        {
            RfqId = rfq.Id,
            Kind = kind,
            FileName = BlobNaming.OriginalName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            UploadedAt = DateTime.UtcNow
        };
        db.RfqDocuments.Add(doc);
        RfqIntake.RefreshRfqStatus(rfq);
        await db.SaveChangesAsync(cancellationToken);
        if (kind == RfqDocumentKind.ScoringStrategy)
            await DefaultRfqCriteria.EnsureAsync(db, rfq.Id, cancellationToken);
        return OpResult<DocumentDto>.Success(new DocumentDto(doc.Id, "Rfq", doc.Kind.ToString(), doc.FileName, doc.SizeBytes, doc.UploadedAt), 201);
    }

    public async Task<OpResult<IReadOnlyList<DocumentDto>>> ListDocumentsAsync(int rfqId, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq is null)
            return OpResult<IReadOnlyList<DocumentDto>>.Fail(404, "RFQ not found.");

        var items = rfq.Documents
            .OrderBy(d => d.Kind)
            .Select(d => new DocumentDto(d.Id, "Rfq", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
            .ToList();
        return OpResult<IReadOnlyList<DocumentDto>>.Success(items);
    }

    public async Task<OpResult<IReadOnlyList<CriterionDto>>> ListCriteriaAsync(int rfqId, CancellationToken cancellationToken)
    {
        if (!await db.Rfqs.AnyAsync(r => r.Id == rfqId, cancellationToken))
            return OpResult<IReadOnlyList<CriterionDto>>.Fail(404, "RFQ not found.");

        await DefaultRfqCriteria.EnsureAsync(db, rfqId, cancellationToken);
        var items = await db.RfqCriteria.AsNoTracking()
            .Where(c => c.RfqId == rfqId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CriterionDto(c.Id, c.Code, c.Name, c.Weight, c.GroupCode, c.GroupWeight, c.SortOrder))
            .ToListAsync(cancellationToken);
        return OpResult<IReadOnlyList<CriterionDto>>.Success(items);
    }

    public async Task<OpResult<IReadOnlyList<CriterionDto>>> UpsertCriteriaAsync(int rfqId, UpsertCriteriaRequest request, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs.Include(r => r.Criteria).FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq is null)
            return OpResult<IReadOnlyList<CriterionDto>>.Fail(404, "RFQ not found.");
        if (request.Items is null || request.Items.Count == 0)
            return OpResult<IReadOnlyList<CriterionDto>>.Fail(400, "At least one criterion is required.");

        db.RfqCriteria.RemoveRange(rfq.Criteria);
        var order = 0;
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
                return OpResult<IReadOnlyList<CriterionDto>>.Fail(400, "Each criterion needs a code and name.");

            db.RfqCriteria.Add(new RfqCriterion
            {
                RfqId = rfqId,
                Code = item.Code.Trim(),
                Name = item.Name.Trim(),
                Weight = item.Weight,
                GroupCode = string.IsNullOrWhiteSpace(item.GroupCode) ? null : item.GroupCode.Trim(),
                GroupWeight = item.GroupWeight,
                SortOrder = item.SortOrder == 0 ? order : item.SortOrder
            });
            order++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ListCriteriaAsync(rfqId, cancellationToken);
    }

    public async Task<OpResult<EvaluateResultDto>> EvaluateAsync(int rfqId, CancellationToken cancellationToken)
    {
        var result = await evaluation.StartAsync(rfqId, cancellationToken);
        if (!result.Ok)
        {
            var status = result.Message == "RFQ not found." ? 404 : 400;
            return OpResult<EvaluateResultDto>.Fail(status, result.Message);
        }

        var run = await db.EvaluationRuns
            .AsNoTracking()
            .Include(r => r.Jobs)
            .Where(r => r.RfqId == rfqId)
            .OrderByDescending(r => r.Id)
            .FirstAsync(cancellationToken);

        var dto = new EvaluateResultDto(
            run.Id,
            run.Status.ToString(),
            run.StatusMessage,
            run.Jobs.OrderBy(j => j.Id).Select(MapJob).ToList());
        return OpResult<EvaluateResultDto>.Success(dto);
    }

    public async Task<OpResult<EvaluateResultDto>> RetryFailedAsync(int rfqId, int? jobId, CancellationToken cancellationToken)
    {
        var result = await evaluation.RetryFailedAsync(rfqId, jobId, cancellationToken);
        if (!result.Ok)
        {
            var status = result.Message == "RFQ not found." || result.Message == "Job not found on this RFQ."
                ? 404
                : 409;
            return OpResult<EvaluateResultDto>.Fail(status, result.Message);
        }

        var run = await db.EvaluationRuns
            .AsNoTracking()
            .Include(r => r.Jobs)
            .Where(r => r.RfqId == rfqId)
            .OrderByDescending(r => r.Id)
            .FirstAsync(cancellationToken);

        var dto = new EvaluateResultDto(
            run.Id,
            run.Status.ToString(),
            run.StatusMessage,
            run.Jobs.OrderBy(j => j.Id).Select(MapJob).ToList());
        return OpResult<EvaluateResultDto>.Success(dto);
    }

    public async Task<PagedResult<JobDto>?> ListJobsAsync(int rfqId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (!await db.Rfqs.AnyAsync(r => r.Id == rfqId, cancellationToken))
            return null;

        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = db.EvaluationJobs.AsNoTracking().Where(j => j.RfqId == rfqId).OrderByDescending(j => j.Id);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(j => new JobDto(j.Id, j.RunId, j.RfqId, j.VendorId, j.JobType.ToString(), j.Status.ToString(), j.StatusMessage, j.CreatedAt))
            .ToListAsync(cancellationToken);
        return PagedResult<JobDto>.Create(items, page, size, total);
    }

    public async Task<PagedResult<VendorListItemDto>?> ListVendorsAsync(int rfqId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (!await db.Rfqs.AnyAsync(r => r.Id == rfqId, cancellationToken))
            return null;

        var (page, size) = Paging.Normalize(pageNumber, pageSize);
        var query = db.Vendors.AsNoTracking().Where(v => v.RfqId == rfqId).OrderBy(v => v.Name);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size)
            .Select(v => new VendorListItemDto(v.Id, v.Name, v.ExternalProjectId, v.Status.ToString(), v.Documents.Count))
            .ToListAsync(cancellationToken);
        return PagedResult<VendorListItemDto>.Create(items, page, size, total);
    }

    public async Task<OpResult<RankingDto>> GetRankingAsync(int rfqId, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs.AsNoTracking()
            .Include(r => r.Vendors)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq is null)
            return OpResult<RankingDto>.Fail(404, "RFQ not found.");

        var evaluations = await db.VendorEvaluations
            .Include(e => e.Vendor)
            .Include(e => e.Scores)
            .ThenInclude(s => s.Criterion)
            .Where(e => e.Vendor.RfqId == rfqId)
            .ToListAsync(cancellationToken);

        var latestByVendor = evaluations
            .GroupBy(e => e.VendorId)
            .Select(g => g.MaxBy(e => e.Id)!)
            .ToList();

        var scored = latestByVendor
            .Where(e => e.Status != ScoreStatus.Rejected)
            .OrderByDescending(e => e.Tws)
            .ThenBy(e => e.Vendor.Name)
            .ToList();

        var eligible = scored.Where(e => e.MandatoryPass && e.TechnicalPass).ToList();
        var rankById = eligible
            .Select((e, i) => (e.VendorId, Rank: i + 1))
            .ToDictionary(x => x.VendorId, x => x.Rank);

        var scoredDtos = scored.Select(e => new RankedVendorDto(
            rankById.TryGetValue(e.VendorId, out var rank) ? rank : null,
            e.VendorId,
            e.Vendor.Name,
            e.Vendor.ExternalProjectId,
            e.JobId,
            e.Tws,
            e.TechnicalPass,
            e.MandatoryPass,
            e.MandatoryPass && e.TechnicalPass,
            e.Recommendation,
            e.Status.ToString())).ToList();

        var scoredIds = scored.Select(e => e.VendorId).ToHashSet();
        var summaries = await db.ProposalSummaries.AsNoTracking()
            .Where(s => s.Vendor.RfqId == rfqId)
            .ToListAsync(cancellationToken);
        var latestSummary = summaries
            .GroupBy(s => s.VendorId)
            .ToDictionary(g => g.Key, g => g.MaxBy(s => s.Id)!);

        var scoreJobs = await db.EvaluationJobs.AsNoTracking()
            .Where(j => j.RfqId == rfqId && j.JobType == JobType.Score)
            .ToListAsync(cancellationToken);
        var latestScoreJob = scoreJobs
            .Where(j => j.VendorId is not null)
            .GroupBy(j => j.VendorId!.Value)
            .ToDictionary(g => g.Key, g => g.MaxBy(j => j.Id)!);

        var leftover = rfq.Vendors
            .Where(v => !scoredIds.Contains(v.Id))
            .OrderBy(v => v.Name)
            .Select(v =>
            {
                latestSummary.TryGetValue(v.Id, out var summary);
                latestScoreJob.TryGetValue(v.Id, out var scoreJob);
                var scoresRejected = v.Status == VendorStatus.ScoreRejected
                    || latestByVendor.FirstOrDefault(e => e.VendorId == v.Id)?.Status == ScoreStatus.Rejected;
                var rejected = scoresRejected
                    || v.Status == VendorStatus.SummaryRejected
                    || summary is { Status: SummaryStatus.Rejected };
                return (
                    Rejected: rejected,
                    Dto: new UnscoredVendorDto(
                        v.Id,
                        v.Name,
                        v.ExternalProjectId,
                        v.Status.ToString(),
                        scoresRejected ? scoreJob?.StatusMessage : summary?.RejectionReason ?? scoreJob?.StatusMessage,
                        summary?.JobId,
                        scoreJob?.Id));
            })
            .ToList();

        var inProgress = leftover.Where(x => !x.Rejected).Select(x => x.Dto).ToList();
        var notAccepted = leftover.Where(x => x.Rejected).Select(x => x.Dto).ToList();

        return OpResult<RankingDto>.Success(new RankingDto(rfq.Id, rfq.Number, rfq.Title, scoredDtos, inProgress, notAccepted));
    }

    private static RfqDetailDto Map(Rfq rfq) =>
        new(
            rfq.Id,
            rfq.Number,
            rfq.Title,
            rfq.Description,
            rfq.Status.ToString(),
            rfq.CreatedAt,
            rfq.UpdatedAt,
            rfq.Documents.OrderBy(d => d.Kind)
                .Select(d => new DocumentDto(d.Id, "Rfq", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
                .ToList(),
            rfq.Vendors.Count);

    private static JobDto MapJob(EvaluationJob job) =>
        new(job.Id, job.RunId, job.RfqId, job.VendorId, job.JobType.ToString(), job.Status.ToString(), job.StatusMessage, job.CreatedAt);
}
