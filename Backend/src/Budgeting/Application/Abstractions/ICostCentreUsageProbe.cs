namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Answers the one question a cost-centre hard delete must ask: does anything carry this cost
/// centre's code? Unlike <see cref="IBudgetCodeUsageProbe"/> it is <b>not</b> period-scoped — the
/// register is tenant-wide, so a budget code in <em>any</em> period (open, closed, retired code
/// or not) that carries the string blocks the delete. When actual transactions arrive they plug
/// into the same implementation without touching this interface or its caller.
/// </summary>
public interface ICostCentreUsageProbe
{
    Task<bool> IsReferencedAsync(string costCentreCode, CancellationToken cancellationToken = default);
}
