namespace Nop.Plugin.Misc.WaAkg.Services;

/// <summary>
/// Plain snapshot of the Advanced COD plugin's per-order record, copied out via reflection
/// so WA-AKG never needs a compile-time reference to Nop.Plugin.Payments.CashOnDelivery.
/// All amounts are already resolved - callers do not need to know that plugin's CodType enum.
/// </summary>
public class CodOrderInfoDto
{
    /// <summary>1 = WithoutAdvance, 2 = PartialAdvance, 3 = FullPayment (mirrors the COD plugin's CodType).</summary>
    public int CodTypeId { get; set; }

    /// <summary>Human readable COD type, e.g. "Partial Advance".</summary>
    public string CodTypeName { get; set; }

    /// <summary>Amount already paid online (Razorpay/Cashfree) toward this order.</summary>
    public decimal AdvancePaid { get; set; }

    /// <summary>Amount still to collect from the customer on delivery.</summary>
    public decimal CodDue { get; set; }

    /// <summary>Extra COD handling charge applied, if any.</summary>
    public decimal ShippingCharge { get; set; }

    /// <summary>Advance amount that was offered/selected at checkout.</summary>
    public decimal AdvanceAmount { get; set; }

    /// <summary>Online payment transaction id, if any.</summary>
    public string TransactionId { get; set; }
}
