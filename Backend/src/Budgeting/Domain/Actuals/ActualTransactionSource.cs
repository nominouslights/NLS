namespace NorthernLink.Budgeting.Domain.Actuals;

/// <summary>
/// Where an actual transaction came from. Both members exist from the first migration even though
/// only <see cref="Manual"/> can be written today: the QuickBooks pull is a later slice, and a
/// discriminator added afterwards would need its own migration plus a backfill of every row
/// already stored. One column, two values, no schema change when the importer lands.
/// <para>
/// It is also half of the re-import guard: the unique (tenant, source, external reference) index
/// is scoped per source, so a reference typed by hand can never collide with a QuickBooks
/// transaction id that happens to read the same.
/// </para>
/// </summary>
public enum ActualTransactionSource
{
    /// <summary>Entered in the Budgeting console by an Owner or Accountant.</summary>
    Manual,

    /// <summary>Pulled from QuickBooks Online, carrying that transaction's id as the external reference.</summary>
    QuickBooks,
}
