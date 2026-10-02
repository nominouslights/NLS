using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Budget codes belong to a period. Before this migration a tenant had one chart of codes
    /// shared by every period; after it, every period owns its own copy of that chart, and every
    /// budget item points at its own period's copy.
    /// <para>
    /// <b>A DATA migration, and BREAKING for the code on main</b> (which neither writes nor reads
    /// <c>period_id</c>, and relies on the (tenant, code) unique index this drops). Apply it only
    /// after the PR merges, together with the API redeploy — never from a feature branch.
    /// </para>
    /// </summary>
    public partial class ScopeBudgetCodesToPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // What this does, in order:
            //   1. GUARD — refuse before touching anything if a tenant has codes but no period.
            //   2. Drop the (tenant, code) unique indexes — the clones share tenant AND code with
            //      their original until step 4 deletes it, so the old index must go first.
            //   3. Add period_id as NULLABLE to budget_codes and rm_budget_codes.
            //   4. Clone every code into every period of its tenant (new ids), remap parents
            //      within the period, repoint every item to its own period's clone, delete the
            //      originals, and rebuild rm_budget_codes from the write table.
            //   5. period_id NOT NULL; unique (tenant, period, code) on both tables.
            //
            // RLS — the trap every Budgeting data migration has to handle (see
            // ClearCostCentreOnRevenueBudgetCodes and BackfillBudgetingUserLookup). A migration
            // runs with no app.tenant_id, so every tenant policy evaluates to false and a blind
            // statement silently matches ZERO rows while the migration reports success:
            //   * SET LOCAL app.is_system = 'true' admits the SELECT on the write tables (their
            //     *_system_read policies are FOR SELECT) and every command on the rm_* tables
            //     (their policies carry an `OR app.is_system` arm).
            //   * INSERT/UPDATE/DELETE on budget_codes and budget_allocations are NOT admitted by
            //     app.is_system, so FORCE ROW LEVEL SECURITY is dropped for the duration (the app
            //     role owns the tables, and an unforced policy does not bind the owner) and put
            //     back in the same batch. ALTER TABLE is transactional and EF runs the whole
            //     migration in one transaction, so a failure anywhere rolls everything back and
            //     no table is ever left unforced. No policy is created, altered or dropped — the
            //     policies are keyed on tenant_id only, so adding period_id leaves them intact.
            // SET LOCAL is repeated in each batch that needs it rather than relied on across
            // batches, so each batch reads correctly on its own.

            // --- 1. Guard. Runs before any schema or data change. -------------------------------
            // A tenant with codes but no period has nowhere to put its chart: step 4 would clone
            // the codes into zero periods and then delete the originals, silently destroying the
            // chart. Refuse instead. MANUAL FIX: create a budget period for that tenant (any
            // granularity/state — POST /api/budgeting/periods on the pre-migration API), then
            // re-run the migration. Nothing has changed at this point, so a re-run is clean.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                DO $guard$
                DECLARE
                    orphaned text;
                BEGIN
                    SELECT string_agg(DISTINCT c.tenant_id::text, ', ')
                      INTO orphaned
                      FROM budgeting.budget_codes c
                     WHERE NOT EXISTS (
                           SELECT 1 FROM budgeting.budget_periods p WHERE p.tenant_id = c.tenant_id);

                    IF orphaned IS NOT NULL THEN
                        RAISE EXCEPTION
                            'ScopeBudgetCodesToPeriods refused: tenant(s) % have budget codes but no budget period, so their chart would be deleted. Create at least one budget period for each listed tenant, then re-run the migration. Nothing was changed.',
                            orphaned;
                    END IF;
                END
                $guard$;
                """);

            // --- 2. Old uniqueness goes first (see the step list above). -----------------------
            migrationBuilder.DropIndex(
                name: "IX_rm_budget_codes_tenant_id_code",
                schema: "budgeting",
                table: "rm_budget_codes");

            migrationBuilder.DropIndex(
                name: "IX_budget_codes_tenant_id_code",
                schema: "budgeting",
                table: "budget_codes");

            // --- 3. Nullable for now: existing rows have no period until step 4. ---------------
            // (Generated as NOT NULL DEFAULT '00000000-…'; hand-edited, because an all-zeroes
            // period id is a wrong value, not a placeholder, and NOT NULL is applied in step 5
            // once every surviving row has a real one.)
            migrationBuilder.AddColumn<Guid>(
                name: "period_id",
                schema: "budgeting",
                table: "rm_budget_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "period_id",
                schema: "budgeting",
                table: "budget_codes",
                type: "uuid",
                nullable: true);

            // --- 4. The data transform. ---------------------------------------------------------
            // The map: one row per (original code × period of the same tenant), with a fresh id.
            // gen_random_uuid() is volatile, so it is evaluated per row. ON COMMIT DROP keeps the
            // temp table from outliving the migration's transaction.
            //
            // Clones copy EVERY column of the original (the 20 columns of budget_codes: id,
            // tenant_id, code, name, description, category, service_line, cost_centre,
            // parent_code_id, gl_account_code, tax_treatment, budget_owner_user_id,
            // review_frequency, is_active, created_by, modified_by, created_at_utc,
            // updated_at_utc, version — plus the new period_id), except:
            //   * id            → the map's new id;
            //   * period_id     → the map's period;
            //   * parent_code_id→ the clone of the original parent IN THE SAME PERIOD. A parent id
            //                     that names no existing code (a dangling id written before the
            //                     delete guard existed) becomes NULL — top-level — rather than a
            //                     dangling id into nothing.
            // created_by/modified_by/timestamps are copied, not restamped: the clone IS the code as
            // it stood, re-homed. version is copied so the write row and its rm_ mirror agree
            // (the projection maps rm.version = aggregate.Version), and the concurrency token
            // carries on from where the original was.
            //
            // Items: each item is repointed to the clone for (its own period, its old code id).
            // Done on both budget_allocations and rm_budget_allocations, each keyed on ITS OWN
            // (period_id, budget_code_id), so a read row a poll behind its write row still lands on
            // a clone of the same period; the projection's next pass re-reads the (already
            // repointed) write row anyway. Items are not version-bumped — nothing a person decided
            // changed.
            //
            // rm_budget_codes is REBUILT from budget_codes rather than transformed in parallel: its
            // columns are the write table's one for one (enums are stored as names on both sides),
            // so an INSERT … SELECT is exactly what BudgetingProjection.RebuildAllAsync would
            // produce, and it also covers a code whose read row the projection had not written
            // yet. Without this, such a code's clones would never get a read row: the journal only
            // names the OLD id, whose source row is gone.
            //
            // The audit trail (event_journal, aggregate_snapshots) is left as it is: it records the
            // history of the original ids, which remains true. The clones' history starts at their
            // first write after this migration.
            migrationBuilder.Sql(
                """
                SET LOCAL app.is_system = 'true';

                ALTER TABLE budgeting.budget_codes NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.budget_allocations NO FORCE ROW LEVEL SECURITY;

                CREATE TEMP TABLE budget_code_period_map ON COMMIT DROP AS
                SELECT c.id              AS old_id,
                       p.id              AS period_id,
                       gen_random_uuid() AS new_id
                  FROM budgeting.budget_codes c
                  JOIN budgeting.budget_periods p ON p.tenant_id = c.tenant_id
                 WHERE c.period_id IS NULL;

                CREATE UNIQUE INDEX ON budget_code_period_map (old_id, period_id);

                INSERT INTO budgeting.budget_codes (
                    id, tenant_id, period_id, code, name, description, category, service_line,
                    cost_centre, parent_code_id, gl_account_code, tax_treatment,
                    budget_owner_user_id, review_frequency, is_active, created_by, modified_by,
                    created_at_utc, updated_at_utc, version)
                SELECT m.new_id, c.tenant_id, m.period_id, c.code, c.name, c.description,
                       c.category, c.service_line, c.cost_centre, parent_map.new_id,
                       c.gl_account_code, c.tax_treatment, c.budget_owner_user_id,
                       c.review_frequency, c.is_active, c.created_by, c.modified_by,
                       c.created_at_utc, c.updated_at_utc, c.version
                  FROM budget_code_period_map m
                  JOIN budgeting.budget_codes c ON c.id = m.old_id
                  LEFT JOIN budget_code_period_map parent_map
                         ON parent_map.old_id = c.parent_code_id
                        AND parent_map.period_id = m.period_id;

                UPDATE budgeting.budget_allocations a
                   SET budget_code_id = m.new_id
                  FROM budget_code_period_map m
                 WHERE m.old_id = a.budget_code_id
                   AND m.period_id = a.period_id;

                UPDATE budgeting.rm_budget_allocations a
                   SET budget_code_id = m.new_id
                  FROM budget_code_period_map m
                 WHERE m.old_id = a.budget_code_id
                   AND m.period_id = a.period_id;

                -- Invariant check before the originals go: no item may still point at an original.
                -- By construction none can (every item's period belongs to its tenant, and every
                -- original was cloned into every period of its tenant), so this only fires on data
                -- nobody expected — and then it rolls the whole migration back instead of leaving
                -- items pointing at deleted codes.
                DO $check$
                DECLARE
                    stranded bigint;
                BEGIN
                    SELECT count(*)
                      INTO stranded
                      FROM budgeting.budget_allocations a
                      JOIN budgeting.budget_codes c ON c.id = a.budget_code_id
                     WHERE c.period_id IS NULL;

                    IF stranded > 0 THEN
                        RAISE EXCEPTION
                            'ScopeBudgetCodesToPeriods refused: % budget item(s) still reference a tenant-wide budget code after repointing (an item whose period is not one of its tenant''s periods?). Rolled back; nothing was changed.',
                            stranded;
                    END IF;
                END
                $check$;

                DELETE FROM budgeting.budget_codes WHERE period_id IS NULL;

                DELETE FROM budgeting.rm_budget_codes;

                INSERT INTO budgeting.rm_budget_codes (
                    id, tenant_id, period_id, code, name, description, category, service_line,
                    cost_centre, parent_code_id, gl_account_code, tax_treatment,
                    budget_owner_user_id, review_frequency, is_active, created_by, modified_by,
                    created_at_utc, updated_at_utc, version)
                SELECT id, tenant_id, period_id, code, name, description, category, service_line,
                       cost_centre, parent_code_id, gl_account_code, tax_treatment,
                       budget_owner_user_id, review_frequency, is_active, created_by, modified_by,
                       created_at_utc, updated_at_utc, version
                  FROM budgeting.budget_codes;

                ALTER TABLE budgeting.budget_allocations FORCE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.budget_codes FORCE ROW LEVEL SECURITY;
                """);

            // --- 5. Every surviving row now has a real period. ----------------------------------
            // SET NOT NULL scans the table and fails the migration (rolling everything back) if a
            // NULL survived — the last backstop for step 4.
            migrationBuilder.AlterColumn<Guid>(
                name: "period_id",
                schema: "budgeting",
                table: "rm_budget_codes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "period_id",
                schema: "budgeting",
                table: "budget_codes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // One code string per period. The leading (tenant_id, period_id) columns serve every
            // per-period chart read, so no separate (tenant_id, period_id) index is created.
            migrationBuilder.CreateIndex(
                name: "IX_rm_budget_codes_tenant_id_period_id_code",
                schema: "budgeting",
                table: "rm_budget_codes",
                columns: new[] { "tenant_id", "period_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_codes_tenant_id_period_id_code",
                schema: "budgeting",
                table: "budget_codes",
                columns: new[] { "tenant_id", "period_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design. Up() turned one tenant-wide chart into one chart per period,
            // with new ids, and repointed every budget item to its own period's copy. Going back
            // would mean choosing ONE surviving copy per code string (whose name, category,
            // parent, owner and active flag win when the periods have diverged?), repointing every
            // item of every other period to it, and deleting the rest — a lossy merge of decisions
            // people made per period. Refusing is better than guessing.
            //
            // To undo: restore the database from a backup taken before this migration, or write a
            // deliberate forward migration that states which copy wins.
            throw new NotSupportedException(
                "ScopeBudgetCodesToPeriods cannot be reverted: it split each tenant-wide budget code into one "
                + "copy per period (new ids) and repointed every budget item to its period's copy. Reverting "
                + "would have to merge copies that may have diverged, losing data. Restore from a backup taken "
                + "before the migration, or write a forward migration that decides which copy wins.");
        }
    }
}
