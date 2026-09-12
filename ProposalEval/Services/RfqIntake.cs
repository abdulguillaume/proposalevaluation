using ProposalEval.Data;

namespace ProposalEval.Services;

public static class RfqIntake
{
    public static void RefreshRfqStatus(Rfq rfq)
    {
        if (rfq.Status is RfqStatus.Evaluating or RfqStatus.Completed)
            return;

        var hasTor = rfq.Documents.Any(d => d.Kind == RfqDocumentKind.Tor);
        var hasStrategy = rfq.Documents.Any(d => d.Kind == RfqDocumentKind.ScoringStrategy);
        rfq.Status = hasTor && hasStrategy && rfq.Vendors.Count > 0
            ? RfqStatus.Ready
            : RfqStatus.Draft;
        rfq.UpdatedAt = DateTime.UtcNow;
    }

    public static void RefreshVendorStatus(Vendor vendor)
    {
        if (vendor.Status is not VendorStatus.Draft and not VendorStatus.Ready)
            return;

        vendor.Status = vendor.Documents.Count > 0 ? VendorStatus.Ready : VendorStatus.Draft;
        vendor.UpdatedAt = DateTime.UtcNow;
    }
}
