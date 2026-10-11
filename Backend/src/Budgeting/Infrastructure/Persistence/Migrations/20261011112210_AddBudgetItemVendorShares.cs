using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetItemVendorShares : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// <b>Schema (additive):</b> one <c>vendor_shares jsonb NOT NULL DEFAULT '[]'</c> column on
        /// budgeting.budget_allocations and on its projection table budgeting.rm_budget_allocations
        /// — an expense budget item's vendors, each with a dollar share. Hand-edited from the
        /// generated nullable column: EF cannot mark an owned collection mapped with
        /// <c>OwnsMany(...).ToJson("vendor_shares")</c> as required, so the NOT NULL and the default
        /// live here (see BudgetItemVendorShareMapping; the Fleet AddWorkOrderDefectLines precedent).
        /// The default makes every existing item "No vendor" before the backfill runs. No RLS
        /// change: both tables already carry ENABLE + FORCE and a tenant policy, and RLS is
        /// row-level, so the new column is covered.
        /// </para>
        /// <para>
        /// <b>Data (backfill):</b> turns the free-text vendor on <b>expense</b> items into register
        /// vendors plus one full-amount share, then clears the text on those items. Revenue items
        /// keep their text untouched — on them it is the payer. Read the comment above the Sql call.
        /// </para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "vendor_shares",
                schema: "budgeting",
                table: "rm_budget_allocations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "vendor_shares",
                schema: "budgeting",
                table: "budget_allocations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            // ---- Hand-appended: backfill vendors and vendor shares from the legacy free text ----
            //
            // Which items: every item whose trimmed `vendor` text is non-blank and whose budget code
            // (by id — an item's code is always a code of its own period) is NOT a Revenue code. An
            // item whose code row cannot be found counts as expense, exactly as PeriodPlannedTotals
            // and the rollups read it. `category` is stored as the enum NAME (HasConversion<string>),
            // so the string literal is the right comparison.
            //
            // Per (tenant, normalized name) — normalized exactly as Vendor.NormalizeName does it,
            // upper(trim(name)), and compared against vendors.normalized_name, the unique key:
            //   * no vendor holds that normalized name yet → one is created, active, with the FIRST
            //     spelling seen (earliest created item, then lowest id — deterministic), no contact
            //     fields, no actor (created_by NULL: the system did this, not a person), version 1;
            //   * a vendor already holds it (active OR retired — the unique index covers both) →
            //     nothing is created and that vendor is used (ON CONFLICT DO NOTHING).
            //
            // Per item with amount > 0: vendor_shares = [{ "VendorId": <that vendor>, "AmountCad":
            // <the item's amount> }] — one share for the whole amount, which satisfies the
            // sum-to-amount rule by construction — and the `vendor` text is cleared. The JSON keys
            // are the CLR property names EF's ToJson mapping reads and writes.
            //
            // Per item with amount = 0: a share must be positive, so a $0 item cannot carry one.
            // Its vendor is still created (so the name is in the register to pick later), but the
            // item keeps its text and gets no share. BudgetAllocation.Update accepts that unchanged
            // text ("only on change"), so the item stays editable.
            //
            // Raw SQL bypasses the audit pipeline: no event_journal rows, so the projection never
            // sees these writes. rm_vendors and rm_budget_allocations are therefore written directly
            // too (the AddCostCentres precedent). A vendor's journal history starts at its first
            // edit through the API.
            //
            // RLS is the whole difficulty, and getting it wrong is silent: a migration runs with no
            // app.tenant_id, so a tenant policy matches nothing and an INSERT/UPDATE "succeeds"
            // having done nothing. Everything is ONE Sql batch because SET LOCAL is scoped to the
            // migration's transaction:
            //
            //   budget_allocations, budget_codes, vendors (reads)
            //                            their *_system_read policies admit SELECT under
            //                            app.is_system.
            //   vendors, budget_allocations (writes)
            //                            their only system policy is FOR SELECT, so FORCE is dropped
            //                            for the write (the app role owns the tables; an unforced
            //                            policy does not bind the owner) and restored in the same
            //                            transaction — ALTER TABLE is transactional, so a failure
            //                            rolls everything back and no table is ever left unforced.
            //                            No policy is created, altered or dropped.
            //   rm_vendors, rm_budget_allocations (writes)
            //                            their policies carry an `OR app.is_system` arm for every
            //                            command, so app.is_system admits the INSERT/UPDATE.
            //
            // Idempotent on a re-run: the vendor INSERTs are ON CONFLICT DO NOTHING, and an item
            // whose text was cleared is no longer a candidate.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                CREATE TEMP TABLE vendor_share_backfill ON COMMIT DROP AS
                SELECT a.id,
                       a.tenant_id,
                       a.amount_cad,
                       a.created_at_utc,
                       btrim(a.vendor)        AS vendor_name,
                       upper(btrim(a.vendor)) AS normalized_name
                FROM budgeting.budget_allocations a
                LEFT JOIN budgeting.budget_codes c
                       ON c.id = a.budget_code_id AND c.tenant_id = a.tenant_id
                WHERE a.vendor IS NOT NULL
                  AND btrim(a.vendor) <> ''
                  AND (c.category IS NULL OR c.category <> 'Revenue');

                ALTER TABLE budgeting.vendors NO FORCE ROW LEVEL SECURITY;
                INSERT INTO budgeting.vendors
                    (id, tenant_id, name, normalized_name, contact_name, email, phone, address, notes,
                     gst_registration_number, qbo_display_name, default_budget_code, is_active,
                     created_by, modified_by, created_at_utc, updated_at_utc, version)
                SELECT gen_random_uuid(), first_spelling.tenant_id, first_spelling.vendor_name,
                       first_spelling.normalized_name, NULL, NULL, NULL, NULL, NULL,
                       NULL, NULL, NULL, true,
                       NULL, NULL, now(), now(), 1
                FROM (SELECT DISTINCT ON (tenant_id, normalized_name)
                             tenant_id, normalized_name, vendor_name
                      FROM vendor_share_backfill
                      ORDER BY tenant_id, normalized_name, created_at_utc, id) AS first_spelling
                ON CONFLICT (tenant_id, normalized_name) DO NOTHING;
                ALTER TABLE budgeting.vendors FORCE ROW LEVEL SECURITY;

                INSERT INTO budgeting.rm_vendors
                    (id, tenant_id, name, normalized_name, contact_name, email, phone, address, notes,
                     gst_registration_number, qbo_display_name, default_budget_code, is_active,
                     created_by, modified_by, created_at_utc, updated_at_utc, version)
                SELECT id, tenant_id, name, normalized_name, contact_name, email, phone, address, notes,
                       gst_registration_number, qbo_display_name, default_budget_code, is_active,
                       created_by, modified_by, created_at_utc, updated_at_utc, version
                FROM budgeting.vendors
                ON CONFLICT (id) DO NOTHING;

                ALTER TABLE budgeting.budget_allocations NO FORCE ROW LEVEL SECURITY;
                UPDATE budgeting.budget_allocations a
                SET vendor_shares = jsonb_build_array(
                        jsonb_build_object('VendorId', v.id, 'AmountCad', b.amount_cad)),
                    vendor = NULL
                FROM vendor_share_backfill b
                JOIN budgeting.vendors v
                  ON v.tenant_id = b.tenant_id AND v.normalized_name = b.normalized_name
                WHERE a.id = b.id
                  AND b.amount_cad > 0;
                ALTER TABLE budgeting.budget_allocations FORCE ROW LEVEL SECURITY;

                UPDATE budgeting.rm_budget_allocations rm
                SET vendor_shares = a.vendor_shares,
                    vendor = a.vendor
                FROM budgeting.budget_allocations a
                WHERE rm.id = a.id
                  AND rm.tenant_id = a.tenant_id
                  AND a.id IN (SELECT id FROM vendor_share_backfill WHERE amount_cad > 0);
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Best-effort reverse of the backfill before the columns go: an item with shares and no
        /// text gets its FIRST share's vendor name back as text (exact for every backfilled item,
        /// which has one share; a deliberate multi-vendor split loses its other vendors, because
        /// free text cannot hold a split). The vendors the backfill created stay in the register —
        /// they are ordinary entries now. Same RLS handling as Up.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                ALTER TABLE budgeting.budget_allocations NO FORCE ROW LEVEL SECURITY;
                UPDATE budgeting.budget_allocations a
                SET vendor = v.name
                FROM budgeting.vendors v
                WHERE a.vendor IS NULL
                  AND jsonb_array_length(a.vendor_shares) > 0
                  AND v.tenant_id = a.tenant_id
                  AND v.id = (a.vendor_shares -> 0 ->> 'VendorId')::uuid;
                ALTER TABLE budgeting.budget_allocations FORCE ROW LEVEL SECURITY;

                UPDATE budgeting.rm_budget_allocations rm
                SET vendor = a.vendor
                FROM budgeting.budget_allocations a
                WHERE rm.id = a.id
                  AND rm.tenant_id = a.tenant_id
                  AND rm.vendor IS DISTINCT FROM a.vendor;
                """);

            migrationBuilder.DropColumn(
                name: "vendor_shares",
                schema: "budgeting",
                table: "rm_budget_allocations");

            migrationBuilder.DropColumn(
                name: "vendor_shares",
                schema: "budgeting",
                table: "budget_allocations");
        }
    }
}
