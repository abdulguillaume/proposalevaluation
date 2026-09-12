using Microsoft.EntityFrameworkCore;
using ProposalEval.Api;
using ProposalEval.Data;

namespace ProposalEval.Services;

public sealed class VendorAppService(AppDbContext db, IFileStore files)
{
    public async Task<OpResult<VendorDetailDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.AsNoTracking().Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        return vendor is null
            ? OpResult<VendorDetailDto>.Fail(404, "Vendor not found.")
            : OpResult<VendorDetailDto>.Success(Map(vendor));
    }

    public async Task<OpResult<VendorDetailDto>> CreateAsync(int rfqId, CreateVendorRequest request, CancellationToken cancellationToken)
    {
        var rfq = await db.Rfqs.Include(r => r.Documents).Include(r => r.Vendors)
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq is null)
            return OpResult<VendorDetailDto>.Fail(404, "RFQ not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return OpResult<VendorDetailDto>.Fail(400, "Name is required.");

        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            RfqId = rfq.Id,
            Name = request.Name.Trim(),
            ExternalProjectId = string.IsNullOrWhiteSpace(request.ExternalProjectId) ? null : request.ExternalProjectId.Trim(),
            Status = VendorStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Vendors.Add(vendor);
        rfq.Vendors.Add(vendor);
        RfqIntake.RefreshRfqStatus(rfq);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<VendorDetailDto>.Success(Map(vendor), 201);
    }

    public async Task<OpResult<DocumentDto>> UploadDocumentAsync(int vendorId, VendorDocumentKind kind, IFormFile file, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == vendorId, cancellationToken);
        if (vendor is null)
            return OpResult<DocumentDto>.Fail(404, "Vendor not found.");
        if (file.Length == 0)
            return OpResult<DocumentDto>.Fail(400, "Choose a file to upload.");

        var key = await files.SaveAsync(BlobContainers.ProposalEvals, BlobFolders.Vendor(vendor.Name, vendor.ExternalProjectId), file.FileName, file.ContentType, file.OpenReadStream(), cancellationToken);
        var doc = new VendorDocument
        {
            VendorId = vendor.Id,
            Kind = kind,
            FileName = BlobNaming.OriginalName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            StorageKey = key,
            UploadedAt = DateTime.UtcNow
        };
        db.VendorDocuments.Add(doc);
        vendor.Documents.Add(doc);
        RfqIntake.RefreshVendorStatus(vendor);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<DocumentDto>.Success(
            new DocumentDto(doc.Id, "Vendor", doc.Kind.ToString(), doc.FileName, doc.SizeBytes, doc.UploadedAt), 201);
    }

    public async Task<OpResult<IReadOnlyList<DocumentDto>>> ListDocumentsAsync(int vendorId, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.AsNoTracking().Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == vendorId, cancellationToken);
        if (vendor is null)
            return OpResult<IReadOnlyList<DocumentDto>>.Fail(404, "Vendor not found.");

        var items = vendor.Documents
            .OrderBy(d => d.Kind)
            .Select(d => new DocumentDto(d.Id, "Vendor", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
            .ToList();
        return OpResult<IReadOnlyList<DocumentDto>>.Success(items);
    }

    public async Task<OpResult<bool>> DeleteDocumentAsync(int vendorId, int documentId, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.Include(v => v.Documents)
            .FirstOrDefaultAsync(v => v.Id == vendorId, cancellationToken);
        if (vendor is null)
            return OpResult<bool>.Fail(404, "Vendor not found.");

        var document = vendor.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null)
            return OpResult<bool>.Fail(404, "Document not found.");

        await files.DeleteAsync(BlobContainers.ProposalEvals, document.StorageKey, cancellationToken);
        db.VendorDocuments.Remove(document);
        vendor.Documents.Remove(document);
        RfqIntake.RefreshVendorStatus(vendor);
        await db.SaveChangesAsync(cancellationToken);
        return OpResult<bool>.Success(true);
    }

    private static VendorDetailDto Map(Vendor vendor) =>
        new(
            vendor.Id,
            vendor.RfqId,
            vendor.Name,
            vendor.ExternalProjectId,
            vendor.Status.ToString(),
            vendor.CreatedAt,
            vendor.UpdatedAt,
            vendor.Documents.OrderBy(d => d.Kind)
                .Select(d => new DocumentDto(d.Id, "Vendor", d.Kind.ToString(), d.FileName, d.SizeBytes, d.UploadedAt))
                .ToList());
}
