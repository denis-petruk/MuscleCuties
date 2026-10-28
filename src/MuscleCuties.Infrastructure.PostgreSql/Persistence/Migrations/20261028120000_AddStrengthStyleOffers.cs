using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations;

[DbContext(typeof(PostgresDbContext))]
[Migration("20261028120000_AddStrengthStyleOffers")]
public sealed class AddStrengthStyleOffers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE app.strength_style_offers (
            id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES app.users(id) ON DELETE CASCADE,
            plan_id uuid NOT NULL, revision_id uuid NOT NULL, current_style_id integer NOT NULL,
            alternative_style_ids integer[] NOT NULL, dismissed_at_utc timestamp with time zone NULL,
            CONSTRAINT uq_strength_style_offers_user_id_id UNIQUE (user_id, id)
        );
        CREATE INDEX ix_strength_style_offers_user_id ON app.strength_style_offers(user_id);
        CREATE UNIQUE INDEX ix_strength_style_offers_user_id_revision_id ON app.strength_style_offers(user_id, revision_id);
        ALTER TABLE app.strength_style_offers ENABLE ROW LEVEL SECURITY;
        ALTER TABLE app.strength_style_offers FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant_isolation ON app.strength_style_offers
            USING (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
            WITH CHECK (user_id = NULLIF(current_setting('app.user_id', true), '')::uuid);
        GRANT SELECT, INSERT, UPDATE, DELETE ON app.strength_style_offers TO musclecuties_app;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("strength_style_offers", "app");
}
