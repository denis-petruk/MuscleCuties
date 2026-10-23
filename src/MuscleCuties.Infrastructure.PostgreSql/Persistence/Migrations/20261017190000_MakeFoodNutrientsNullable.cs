using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    [DbContext(typeof(PostgresDbContext))]
    [Migration("20261017190000_MakeFoodNutrientsNullable")]
    /// <inheritdoc />
    public partial class MakeFoodNutrientsNullable : Migration
    {
        private static readonly string[] NutrientColumns =
        [
            "calories", "protein", "carbs", "fats", "fiber", "iron", "vitamin_b12", "vitamin_c",
            "vitamin_d", "vitamin_a", "vitamin_b6", "folate", "calcium", "magnesium", "zinc", "potassium"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in NutrientColumns)
            {
                migrationBuilder.AlterColumn<decimal>(column, "food_items", type: "numeric", schema: "catalog", nullable: true,
                    oldClrType: typeof(decimal), oldType: "numeric");
                migrationBuilder.AlterColumn<decimal>(column, "user_food_items", type: "numeric", schema: "app", nullable: true,
                    oldClrType: typeof(decimal), oldType: "numeric");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var missingValuePredicates = NutrientColumns
                .Select(column => $"\"{column}\" IS NULL")
                .ToArray();
            var refusalSql = $"""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM catalog.food_items WHERE {string.Join(" OR ", missingValuePredicates)})
                       OR EXISTS (SELECT 1 FROM app.user_food_items WHERE {string.Join(" OR ", missingValuePredicates)}) THEN
                        RAISE EXCEPTION 'Cannot roll back nullable food nutrients: null values represent unknown nutrition. Restore source-verified nutrient values for every null in catalog.food_items and app.user_food_items, or restore a pre-migration backup.';
                    END IF;
                END $$;
                """;
            migrationBuilder.Sql(refusalSql);

            foreach (var column in NutrientColumns)
            {
                migrationBuilder.AlterColumn<decimal>(column, "food_items", type: "numeric", schema: "catalog", nullable: false,
                    oldClrType: typeof(decimal), oldType: "numeric", oldNullable: true);
                migrationBuilder.AlterColumn<decimal>(column, "user_food_items", type: "numeric", schema: "app", nullable: false,
                    oldClrType: typeof(decimal), oldType: "numeric", oldNullable: true);
            }
        }
    }
}
