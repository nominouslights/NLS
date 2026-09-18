using System.Globalization;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Actuals.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// ActualTransaction input rules (amount, date, description, external reference), cent rounding,
/// the code snapshot, and Update semantics. The cross-aggregate guards — a date some period
/// covers, an active code — are the handlers' and live in
/// <see cref="RecordActualTransactionCommandHandlerTests"/> and
/// <see cref="UpdateActualTransactionCommandHandlerTests"/>.
/// </summary>
public class ActualTransactionTests
{
    private static readonly Guid CodeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateOnly Occurred = new(2026, 11, 15);

    private static decimal Money(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

    // --- Create ---

    [Fact]
    public void Create_stores_the_transaction_and_stamps_the_actor_as_creator()
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", 480.50m, Occurred,
            "Diesel, Yellowknife depot.", ActualTransactionSource.Manual, null, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        var transaction = result.Value;
        Assert.Equal(TestBudgeting.TenantId, transaction.TenantId);
        Assert.Equal(CodeId, transaction.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", transaction.Code);
        Assert.Equal(480.50m, transaction.AmountCad);
        Assert.Equal(Occurred, transaction.OccurredOn);
        Assert.Equal("Diesel, Yellowknife depot.", transaction.Description);
        Assert.Equal(ActualTransactionSource.Manual, transaction.Source);
        Assert.Null(transaction.ExternalReference);
        Assert.Equal(TestBudgeting.ActorId, transaction.CreatedBy);
        Assert.Null(transaction.ModifiedBy);
        Assert.Equal(transaction.CreatedAtUtc, transaction.UpdatedAtUtc);
    }

    [Fact]
    public void Create_raises_the_created_event_with_actor_code_amount_date_and_source()
    {
        var transaction = TestBudgeting.CreateActual(
            CodeId, "ZBB-FUEL-01", 480.50m, Occurred, actorId: TestBudgeting.ActorId);

        var created = Assert.Single(transaction.DomainEvents.OfType<ActualTransactionCreatedDomainEvent>());
        Assert.Equal(transaction.Id, created.TransactionId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal(CodeId, created.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", created.Code);
        Assert.Equal(480.50m, created.AmountCad);
        Assert.Equal(Occurred, created.OccurredOn);
        Assert.Equal(ActualTransactionSource.Manual, created.Source);
        Assert.Equal(TestBudgeting.ActorId, created.ActorId);
        Assert.Empty(transaction.DomainEvents.OfType<ActualTransactionUpdatedDomainEvent>());
    }

    [Fact]
    public void Create_without_an_actor_leaves_CreatedBy_null()
    {
        var transaction = TestBudgeting.CreateActual(actorId: null);

        Assert.Null(transaction.CreatedBy);
        Assert.Null(Assert.Single(transaction.DomainEvents.OfType<ActualTransactionCreatedDomainEvent>()).ActorId);
    }

    [Fact]
    public void There_is_no_period_on_the_aggregate_only_the_date_it_happened()
    {
        // The single most important design point in the slice, pinned as a test so a later
        // "convenience" period_id column has to argue with it: the period is derived from
        // OccurredOn, and moving the date is the only thing that moves the transaction.
        var transaction = TestBudgeting.CreateActual(occurredOn: new DateOnly(2026, 10, 1));
        Assert.Null(typeof(ActualTransaction).GetProperty("PeriodId"));

        transaction.Update(CodeId, "ZBB-FUEL-01", 10m, new DateOnly(2027, 1, 1), "Moved.", null);

        Assert.Equal(new DateOnly(2027, 1, 1), transaction.OccurredOn);
    }

    // --- Amount ---

    [Fact]
    public void A_missing_amount_is_rejected()
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", null, Occurred, "Why.",
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.AmountRequired, result.Error);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    [InlineData("-999999999.99")]
    public void A_negative_amount_is_rejected(string amount)
    {
        // Expenses are positive amounts on an Expense code; the minus sign is not a second,
        // silent way of saying "revenue" or "refund".
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", Money(amount), Occurred, "Why.",
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.AmountNegative, result.Error);
    }

    [Theory]
    [InlineData("1000000000")]
    [InlineData("1000000000.00")]
    [InlineData("999999999.995")]
    public void An_amount_above_the_numeric_12_2_ceiling_is_rejected(string amount)
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", Money(amount), Occurred, "Why.",
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.AmountTooLarge, result.Error);
    }

    [Fact]
    public void The_ceiling_itself_is_allowed()
    {
        var transaction = TestBudgeting.CreateActual(amount: ActualTransaction.AmountMax);

        Assert.Equal(999_999_999.99m, transaction.AmountCad);
    }

    [Theory]
    [InlineData("10.005", "10.01")]
    [InlineData("10.004", "10.00")]
    [InlineData("2.675", "2.68")]
    [InlineData("480.5", "480.50")]
    public void Amounts_are_rounded_to_cents_half_away_from_zero(string input, string stored)
    {
        var transaction = TestBudgeting.CreateActual(amount: Money(input));

        Assert.Equal(Money(stored), transaction.AmountCad);
        Assert.Equal(
            Money(stored),
            Assert.Single(transaction.DomainEvents.OfType<ActualTransactionCreatedDomainEvent>()).AmountCad);
    }

    // --- Date ---

    [Fact]
    public void A_missing_date_is_rejected()
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", 100m, null, "Why.",
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.OccurredOnRequired, result.Error);
    }

    [Theory]
    [InlineData(1999, 1, 1)]
    [InlineData(2199, 12, 31)]
    public void The_aggregate_puts_no_calendar_bounds_on_the_date(int year, int month, int day)
    {
        // Deliberately unbounded here: the real gate is that some period covers the date, and
        // that lives in the handler because the aggregate cannot see periods. A year range
        // duplicated here would be a second, weaker copy of the same rule.
        var transaction = TestBudgeting.CreateActual(occurredOn: new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(year, month, day), transaction.OccurredOn);
    }

    // --- Description ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_blank_description_is_rejected(string? description)
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", 100m, Occurred, description,
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.DescriptionRequired, result.Error);
    }

    [Fact]
    public void A_description_over_the_limit_is_rejected()
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", 100m, Occurred,
            new string('x', ActualTransaction.DescriptionMaxLength + 1),
            ActualTransactionSource.Manual, null, null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.DescriptionTooLong, result.Error);
    }

    [Fact]
    public void The_description_is_trimmed_on_store_and_padding_does_not_count_toward_the_limit()
    {
        var body = new string('x', ActualTransaction.DescriptionMaxLength);

        var transaction = TestBudgeting.CreateActual(description: $"  {body} \n");

        Assert.Equal(body, transaction.Description);
    }

    // --- Source and external reference: the re-import guard's two halves ---

    [Fact]
    public void The_source_is_whatever_created_the_row_and_Update_never_touches_it()
    {
        var pulled = TestBudgeting.CreateActual(
            source: ActualTransactionSource.QuickBooks, externalReference: "QBO-8891");

        pulled.Update(CodeId, "ZBB-FUEL-01", 12m, Occurred, "Corrected after review.", null);

        Assert.Equal(ActualTransactionSource.QuickBooks, pulled.Source);
        Assert.Equal("QBO-8891", pulled.ExternalReference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_external_reference_is_stored_as_null(string? reference)
    {
        // Blank is not a reference. Storing "" would make a row look referenced while colliding
        // with nothing, which is the worst of both.
        var transaction = TestBudgeting.CreateActual(externalReference: reference);

        Assert.Null(transaction.ExternalReference);
    }

    [Fact]
    public void An_external_reference_is_trimmed()
    {
        // A trailing newline out of an import would otherwise make two copies of one id look like
        // two different transactions to the unique index — which is exactly the guard failing.
        var transaction = TestBudgeting.CreateActual(externalReference: "  QBO-8891\n");

        Assert.Equal("QBO-8891", transaction.ExternalReference);
    }

    [Fact]
    public void An_external_reference_over_the_limit_is_rejected()
    {
        var result = ActualTransaction.Create(
            TestBudgeting.TenantId, CodeId, "ZBB-FUEL-01", 100m, Occurred, "Why.",
            ActualTransactionSource.QuickBooks,
            new string('x', ActualTransaction.ExternalReferenceMaxLength + 1),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.ExternalReferenceTooLong, result.Error);
    }

    [Fact]
    public void The_domain_does_not_dedupe_two_rows_carrying_the_same_external_reference()
    {
        // Pinned deliberately: idempotent re-import is NOT an aggregate rule and cannot be — an
        // aggregate cannot see its siblings. It is the unique (tenant_id, source,
        // external_reference) index, asserted in ActualTransactionSchemaTests. Two rows here
        // means the second insert is the database's to refuse, not the domain's.
        var first = TestBudgeting.CreateActual(
            source: ActualTransactionSource.QuickBooks, externalReference: "QBO-8891");
        var second = TestBudgeting.CreateActual(
            source: ActualTransactionSource.QuickBooks, externalReference: "QBO-8891");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.ExternalReference, second.ExternalReference);
    }

    // --- Validate ordering ---

    [Fact]
    public void Validate_reports_the_amount_then_the_date_then_the_description()
    {
        // The handlers run Validate before any lookup and the caller sees one error at a time.
        Assert.Equal(
            ActualTransactionErrors.AmountRequired,
            ActualTransaction.Validate(null, null, null).Error);
        Assert.Equal(
            ActualTransactionErrors.AmountNegative,
            ActualTransaction.Validate(-1m, null, "   ").Error);
        Assert.Equal(
            ActualTransactionErrors.AmountTooLarge,
            ActualTransaction.Validate(ActualTransaction.AmountMax + 0.01m, null, null).Error);
        Assert.Equal(
            ActualTransactionErrors.OccurredOnRequired,
            ActualTransaction.Validate(0m, null, "   ").Error);
        Assert.Equal(
            ActualTransactionErrors.DescriptionRequired,
            ActualTransaction.Validate(0m, Occurred, "").Error);
        Assert.Equal(
            ActualTransactionErrors.DescriptionTooLong,
            ActualTransaction.Validate(0m, Occurred, new string('x', 301)).Error);
    }

    [Fact]
    public void Validate_passes_a_well_formed_input()
    {
        Assert.True(ActualTransaction.Validate(0m, Occurred, "Why.").IsSuccess);
        Assert.True(ActualTransaction
            .Validate(
                ActualTransaction.AmountMax,
                Occurred,
                new string('x', ActualTransaction.DescriptionMaxLength))
            .IsSuccess);
    }

    // --- The code snapshot ---

    [Fact]
    public void The_code_string_is_the_snapshot_taken_at_creation_and_has_no_public_setter()
    {
        // The reason the string is held at all: a code deleted and recreated under it gets a new
        // id, and this row must still be findable from the string. Nothing outside the aggregate
        // can rewrite it.
        var transaction = TestBudgeting.CreateActual(budgetCodeId: CodeId, code: "ZBB-FUEL-01");

        Assert.Equal("ZBB-FUEL-01", transaction.Code);
        Assert.False(typeof(ActualTransaction).GetProperty(nameof(ActualTransaction.Code))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void The_code_string_only_ever_moves_together_with_the_code_id()
    {
        // The one API that changes either takes both, so the string can never drift from the id
        // it describes. Re-pointing the transaction at another code is the only way it changes —
        // the miscoded-expense correction, not a free-text edit.
        var transaction = TestBudgeting.CreateActual(budgetCodeId: CodeId, code: "ZBB-FUEL-01");
        var maintenanceId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        var parameters = typeof(ActualTransaction)
            .GetMethod(nameof(ActualTransaction.Update))!
            .GetParameters()
            .Select(p => p.Name)
            .ToList();
        Assert.Contains("budgetCodeId", parameters);
        Assert.Contains("code", parameters);

        transaction.Update(maintenanceId, "ZBB-MAINT-01", 480.50m, Occurred, "Miscoded.", null);

        Assert.Equal(maintenanceId, transaction.BudgetCodeId);
        Assert.Equal("ZBB-MAINT-01", transaction.Code);
    }

    // --- Update ---

    [Fact]
    public void Update_rewrites_the_code_pair_amount_date_description_and_modifier_and_leaves_the_creator_alone()
    {
        var transaction = TestBudgeting.CreateActual(
            CodeId, "ZBB-FUEL-01", 480.50m, Occurred, "Diesel.", actorId: TestBudgeting.ActorId);
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var otherCodeId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var id = transaction.Id;

        var result = transaction.Update(
            otherCodeId, "ZBB-MAINT-01", 512.345m, new DateOnly(2026, 12, 2),
            "  Miscoded — this was maintenance.  ", editor);

        Assert.True(result.IsSuccess);
        Assert.Equal(otherCodeId, transaction.BudgetCodeId);
        Assert.Equal("ZBB-MAINT-01", transaction.Code);
        Assert.Equal(512.35m, transaction.AmountCad);
        Assert.Equal(new DateOnly(2026, 12, 2), transaction.OccurredOn);
        Assert.Equal("Miscoded — this was maintenance.", transaction.Description);
        Assert.Equal(editor, transaction.ModifiedBy);
        Assert.Equal(TestBudgeting.ActorId, transaction.CreatedBy);
        Assert.Equal(id, transaction.Id);
    }

    [Fact]
    public void Update_raises_the_updated_event_carrying_the_code_pair_amount_and_date()
    {
        var transaction = TestBudgeting.CreateActual(CodeId, "ZBB-FUEL-01");
        transaction.ClearDomainEvents();

        transaction.Update(CodeId, "ZBB-FUEL-01", 10.005m, Occurred, "Rounded up.", TestBudgeting.ActorId);

        var updated = Assert.Single(transaction.DomainEvents.OfType<ActualTransactionUpdatedDomainEvent>());
        Assert.Equal(transaction.Id, updated.TransactionId);
        Assert.Equal(CodeId, updated.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", updated.Code);
        Assert.Equal(10.01m, updated.AmountCad);
        Assert.Equal(Occurred, updated.OccurredOn);
        Assert.Equal(TestBudgeting.ActorId, updated.ActorId);
        Assert.Empty(transaction.DomainEvents.OfType<ActualTransactionCreatedDomainEvent>());
    }

    [Fact]
    public void Update_with_unchanged_values_still_raises_the_event()
    {
        // A re-submitted correction is still a decision the journal carries, and an eventless
        // Modified save is refused by the audit pipeline — so "nothing changed" must not mean
        // "no event".
        var transaction = TestBudgeting.CreateActual(CodeId, "ZBB-FUEL-01", 480.50m, Occurred, "Same.");
        transaction.ClearDomainEvents();

        var result = transaction.Update(CodeId, "ZBB-FUEL-01", 480.50m, Occurred, "Same.", null);

        Assert.True(result.IsSuccess);
        Assert.Single(transaction.DomainEvents.OfType<ActualTransactionUpdatedDomainEvent>());
    }

    [Fact]
    public void A_refused_update_changes_nothing_and_raises_nothing()
    {
        var transaction = TestBudgeting.CreateActual(
            CodeId, "ZBB-FUEL-01", 480.50m, Occurred, "Original.", actorId: null);
        transaction.ClearDomainEvents();

        var result = transaction.Update(Guid.NewGuid(), "ZBB-MAINT-01", -5m, Occurred, "Fine.", TestBudgeting.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(CodeId, transaction.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", transaction.Code);
        Assert.Equal(480.50m, transaction.AmountCad);
        Assert.Equal("Original.", transaction.Description);
        Assert.Null(transaction.ModifiedBy);
        Assert.Empty(transaction.DomainEvents);
    }

    [Fact]
    public void Update_reports_the_same_errors_as_Create()
    {
        var transaction = TestBudgeting.CreateActual(CodeId, "ZBB-FUEL-01");

        Assert.Equal(
            ActualTransactionErrors.AmountRequired,
            transaction.Update(CodeId, "ZBB-FUEL-01", null, Occurred, "Why.", null).Error);
        Assert.Equal(
            ActualTransactionErrors.AmountNegative,
            transaction.Update(CodeId, "ZBB-FUEL-01", -1m, Occurred, "Why.", null).Error);
        Assert.Equal(
            ActualTransactionErrors.OccurredOnRequired,
            transaction.Update(CodeId, "ZBB-FUEL-01", 1m, null, "Why.", null).Error);
        Assert.Equal(
            ActualTransactionErrors.DescriptionRequired,
            transaction.Update(CodeId, "ZBB-FUEL-01", 1m, Occurred, null, null).Error);
    }
}
