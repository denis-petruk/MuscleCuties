using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MuscleCuties.Infrastructure.PostgreSql.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "email_challenges",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    redeemed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_challenges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    video_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    technique_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    primary_muscle = table.Column<string>(type: "text", nullable: false),
                    secondary_muscles = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    joint_areas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_injury_friendly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "food_items",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    calories = table.Column<decimal>(type: "numeric", nullable: false),
                    protein = table.Column<decimal>(type: "numeric", nullable: false),
                    carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    fats = table.Column<decimal>(type: "numeric", nullable: false),
                    fiber = table.Column<decimal>(type: "numeric", nullable: false),
                    iron = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_b12 = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_c = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_d = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_a = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_b6 = table.Column<decimal>(type: "numeric", nullable: false),
                    folate = table.Column<decimal>(type: "numeric", nullable: false),
                    calcium = table.Column<decimal>(type: "numeric", nullable: false),
                    magnesium = table.Column<decimal>(type: "numeric", nullable: false),
                    zinc = table.Column<decimal>(type: "numeric", nullable: false),
                    potassium = table.Column<decimal>(type: "numeric", nullable: false),
                    fdc_id = table.Column<int>(type: "integer", nullable: true),
                    data_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    brand_owner = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    gtin_upc = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ingredients = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    serving_size = table.Column<decimal>(type: "numeric", nullable: true),
                    serving_size_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    serving_options_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "food_sync_logs",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    items_upserted = table.Column<int>(type: "integer", nullable: false),
                    items_failed = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_sync_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meal_templates",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    meal_type = table.Column<string>(type: "text", nullable: false),
                    dietary_tags = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    phase_tags = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    question = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    question_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_archetypes",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    display_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_lower_dominant = table.Column<bool>(type: "boolean", nullable: false),
                    contains_heavy_hinge = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_archetypes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    normalized_email = table.Column<string>(type: "text", nullable: false, computedColumnSql: "lower(btrim(email))", stored: true),
                    apple_user_id = table.Column<string>(type: "text", nullable: true),
                    credential_type = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_onboarding_complete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "week_templates",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    days_per_week = table.Column<int>(type: "integer", nullable: false),
                    archetype_sequence_json = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_week_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workout_exercise_definitions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    pattern = table.Column<string>(type: "text", nullable: false),
                    required_equipment = table.Column<string>(type: "text", nullable: false),
                    long_length_loaded = table.Column<byte>(type: "smallint", nullable: false),
                    fatigue_cost = table.Column<byte>(type: "smallint", nullable: false),
                    skill_demand = table.Column<byte>(type: "smallint", nullable: false),
                    setup_seconds = table.Column<int>(type: "integer", nullable: false),
                    seconds_per_rep = table.Column<byte>(type: "smallint", nullable: false),
                    eligible_for_hi_block = table.Column<bool>(type: "boolean", nullable: false),
                    unilateral_doubles_time = table.Column<bool>(type: "boolean", nullable: false),
                    is_bodyweight = table.Column<bool>(type: "boolean", nullable: false),
                    contraindications = table.Column<string>(type: "text", nullable: false),
                    preferred_for = table.Column<string>(type: "text", nullable: false),
                    supine_or_prone = table.Column<byte>(type: "smallint", nullable: false),
                    valsalva_demand = table.Column<byte>(type: "smallint", nullable: false),
                    substitute_group_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_exercise_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workout_muscle_groups",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_muscle_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workout_planning_config_entries",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    section = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_planning_config_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "food_item_versions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    food_item_id = table.Column<int>(type: "integer", nullable: false),
                    nutrient_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    versioned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    change_source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_item_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_food_item_versions_food_items_food_item_id",
                        column: x => x.food_item_id,
                        principalSchema: "catalog",
                        principalTable: "food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meal_template_entries",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    meal_template_id = table.Column<int>(type: "integer", nullable: false),
                    food_item_id = table.Column<int>(type: "integer", nullable: false),
                    grams = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_template_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_meal_template_entries_food_items_food_item_id",
                        column: x => x.food_item_id,
                        principalSchema: "catalog",
                        principalTable: "food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meal_template_entries_meal_templates_meal_template_id",
                        column: x => x.meal_template_id,
                        principalSchema: "catalog",
                        principalTable: "meal_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_answers",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    mapped_value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_quiz_answers_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "catalog",
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cycle_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    cycle_length = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_logs", x => x.id);
                    table.UniqueConstraint("uq_cycle_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_cycle_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_nutrition_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    water_intake_ml = table.Column<int>(type: "integer", nullable: false),
                    target_calories = table.Column<decimal>(type: "numeric", nullable: false),
                    target_protein = table.Column<decimal>(type: "numeric", nullable: false),
                    target_carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    target_fat = table.Column<decimal>(type: "numeric", nullable: false),
                    total_calories = table.Column<decimal>(type: "numeric", nullable: false),
                    total_protein = table.Column<decimal>(type: "numeric", nullable: false),
                    total_carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    total_fat = table.Column<decimal>(type: "numeric", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_nutrition_logs", x => x.id);
                    table.UniqueConstraint("uq_daily_nutrition_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_daily_nutrition_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_readiness_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    sleep_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    sleep3d_avg = table.Column<decimal>(type: "numeric", nullable: false),
                    steps_yesterday = table.Column<int>(type: "integer", nullable: false),
                    steps7d_avg = table.Column<int>(type: "integer", nullable: false),
                    energy = table.Column<int>(type: "integer", nullable: false),
                    pain = table.Column<int>(type: "integer", nullable: false),
                    bloating = table.Column<bool>(type: "boolean", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    sleep_quality = table.Column<int>(type: "integer", nullable: true),
                    soreness = table.Column<int>(type: "integer", nullable: true),
                    previous_day_load = table.Column<decimal>(type: "numeric", nullable: true),
                    readiness_score = table.Column<int>(type: "integer", nullable: false),
                    readiness_tier = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_readiness_logs", x => x.id);
                    table.UniqueConstraint("uq_daily_readiness_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_daily_readiness_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "logged_meals",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    meal_type = table.Column<string>(type: "text", nullable: false),
                    accepted_suggestion_name = table.Column<string>(type: "text", nullable: true),
                    accepted_suggestion_phase = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logged_meals", x => x.id);
                    table.UniqueConstraint("uq_logged_meals_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_logged_meals_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_journey_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    image_path = table.Column<string>(type: "text", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_journey_entries", x => x.id);
                    table.UniqueConstraint("uq_photo_journey_entries_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_photo_journey_entries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.id);
                    table.UniqueConstraint("uq_recipes_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_recipes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_meal_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    meal_type = table.Column<string>(type: "text", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_meal_templates", x => x.id);
                    table.UniqueConstraint("uq_saved_meal_templates_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_saved_meal_templates_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_food_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    calories = table.Column<decimal>(type: "numeric", nullable: false),
                    protein = table.Column<decimal>(type: "numeric", nullable: false),
                    carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    fats = table.Column<decimal>(type: "numeric", nullable: false),
                    fiber = table.Column<decimal>(type: "numeric", nullable: false),
                    iron = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_b12 = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_c = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_d = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_a = table.Column<decimal>(type: "numeric", nullable: false),
                    vitamin_b6 = table.Column<decimal>(type: "numeric", nullable: false),
                    folate = table.Column<decimal>(type: "numeric", nullable: false),
                    calcium = table.Column<decimal>(type: "numeric", nullable: false),
                    magnesium = table.Column<decimal>(type: "numeric", nullable: false),
                    zinc = table.Column<decimal>(type: "numeric", nullable: false),
                    potassium = table.Column<decimal>(type: "numeric", nullable: false),
                    fdc_id = table.Column<int>(type: "integer", nullable: true),
                    data_type = table.Column<string>(type: "text", nullable: true),
                    brand_owner = table.Column<string>(type: "text", nullable: true),
                    brand_name = table.Column<string>(type: "text", nullable: true),
                    gtin_upc = table.Column<string>(type: "text", nullable: true),
                    ingredients = table.Column<string>(type: "text", nullable: true),
                    serving_size = table.Column<decimal>(type: "numeric", nullable: true),
                    serving_size_unit = table.Column<string>(type: "text", nullable: true),
                    serving_options_json = table.Column<string>(type: "jsonb", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_food_items", x => x.id);
                    table.UniqueConstraint("uq_user_food_items_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_food_items_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_meal_templates",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    meal_type = table.Column<string>(type: "text", nullable: false),
                    dietary_tags = table.Column<string>(type: "text", nullable: false),
                    phase_tags = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meal_templates", x => x.id);
                    table.UniqueConstraint("uq_user_meal_templates_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_meal_templates_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_movement_notes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<int>(type: "integer", nullable: false),
                    exercise_name = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_movement_notes", x => x.id);
                    table.UniqueConstraint("uq_user_movement_notes_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_movement_notes_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "catalog",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_movement_notes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profile_snapshots",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_reason = table.Column<string>(type: "text", nullable: false),
                    profile_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_snapshots", x => x.id);
                    table.UniqueConstraint("uq_user_profile_snapshots_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_profile_snapshots_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    height = table.Column<decimal>(type: "numeric", nullable: false),
                    weight = table.Column<decimal>(type: "numeric", nullable: false),
                    goal = table.Column<string>(type: "text", nullable: false),
                    weight_goal_pace = table.Column<string>(type: "text", nullable: false),
                    training_experience_level = table.Column<string>(type: "text", nullable: false),
                    cycle_tracking_mode = table.Column<string>(type: "text", nullable: false),
                    current_cycle_phase = table.Column<string>(type: "text", nullable: true),
                    workout_days_per_week = table.Column<int>(type: "integer", nullable: false),
                    cycle_length = table.Column<int>(type: "integer", nullable: false),
                    dietary_tags = table.Column<string>(type: "text", nullable: false),
                    session_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    equipment_level = table.Column<string>(type: "text", nullable: false),
                    phase_baselines_json = table.Column<string>(type: "jsonb", nullable: false),
                    preferred_workout_activity_types = table.Column<string>(type: "text", nullable: false),
                    unit_system = table.Column<string>(type: "text", nullable: false),
                    body_weight_unit = table.Column<string>(type: "text", nullable: false),
                    food_mass_unit = table.Column<string>(type: "text", nullable: false),
                    height_unit = table.Column<string>(type: "text", nullable: false),
                    distance_unit = table.Column<string>(type: "text", nullable: false),
                    energy_unit = table.Column<string>(type: "text", nullable: false),
                    nutrition_goals_json = table.Column<string>(type: "jsonb", nullable: false),
                    reproductive_context_json = table.Column<string>(type: "jsonb", nullable: false),
                    profile_image_path = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.id);
                    table.UniqueConstraint("uq_user_profiles_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_injury_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_flag = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    since = table.Column<DateOnly>(type: "date", nullable: false),
                    pain = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_injury_logs", x => x.id);
                    table.UniqueConstraint("uq_workout_injury_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_injury_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_plans",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    cycle_phase_target = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_plans", x => x.id);
                    table.UniqueConstraint("uq_workout_plans_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_plans_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_session_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_name = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    total_volume_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    rpe_rating = table.Column<int>(type: "integer", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_session_logs", x => x.id);
                    table.UniqueConstraint("uq_workout_session_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_session_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_exercise_preferences",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_exercise_id = table.Column<int>(type: "integer", nullable: false),
                    preferred_exercise_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_exercise_preferences", x => x.id);
                    table.UniqueConstraint("uq_user_exercise_preferences_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_exercise_preferences_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_exercise_preferences_workout_exercise_definitions_orig~",
                        column: x => x.original_exercise_id,
                        principalSchema: "catalog",
                        principalTable: "workout_exercise_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_exercise_preferences_workout_exercise_definitions_pref~",
                        column: x => x.preferred_exercise_id,
                        principalSchema: "catalog",
                        principalTable: "workout_exercise_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exercise_muscle_contributions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    exercise_id = table.Column<int>(type: "integer", nullable: false),
                    muscle_group_id = table.Column<int>(type: "integer", nullable: false),
                    fraction = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_muscle_contributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exercise_muscle_contributions_workout_exercise_definitions_~",
                        column: x => x.exercise_id,
                        principalSchema: "catalog",
                        principalTable: "workout_exercise_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_muscle_contributions_workout_muscle_groups_muscle_~",
                        column: x => x.muscle_group_id,
                        principalSchema: "catalog",
                        principalTable: "workout_muscle_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goal_tier_weights",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    goal = table.Column<string>(type: "text", nullable: false),
                    muscle_group_id = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_tier_weights", x => x.id);
                    table.ForeignKey(
                        name: "FK_goal_tier_weights_workout_muscle_groups_muscle_group_id",
                        column: x => x.muscle_group_id,
                        principalSchema: "catalog",
                        principalTable: "workout_muscle_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "slot_templates",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    archetype_id = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<byte>(type: "smallint", nullable: false),
                    block = table.Column<string>(type: "text", nullable: false),
                    allowed_patterns_json = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    primary_muscle_id = table.Column<int>(type: "integer", nullable: false),
                    sets_min = table.Column<byte>(type: "smallint", nullable: false),
                    sets_max = table.Column<byte>(type: "smallint", nullable: false),
                    reps_min = table.Column<byte>(type: "smallint", nullable: false),
                    reps_max = table.Column<byte>(type: "smallint", nullable: false),
                    target_rir = table.Column<byte>(type: "smallint", nullable: false),
                    droppable = table.Column<bool>(type: "boolean", nullable: false),
                    superset_group = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_slot_templates_session_archetypes_archetype_id",
                        column: x => x.archetype_id,
                        principalSchema: "catalog",
                        principalTable: "session_archetypes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_slot_templates_workout_muscle_groups_primary_muscle_id",
                        column: x => x.primary_muscle_id,
                        principalSchema: "catalog",
                        principalTable: "workout_muscle_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "volume_budget_rows",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    seed_code = table.Column<string>(type: "text", nullable: false),
                    days_per_week = table.Column<int>(type: "integer", nullable: false),
                    muscle_group_id = table.Column<int>(type: "integer", nullable: false),
                    fractional_sets = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volume_budget_rows", x => x.id);
                    table.ForeignKey(
                        name: "FK_volume_budget_rows_workout_muscle_groups_muscle_group_id",
                        column: x => x.muscle_group_id,
                        principalSchema: "catalog",
                        principalTable: "workout_muscle_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cycle_phase_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_log_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phase = table.Column<string>(type: "text", nullable: false),
                    flow_intensity = table.Column<string>(type: "text", nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_phase_logs", x => x.id);
                    table.UniqueConstraint("uq_cycle_phase_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_cycle_phase_logs_cycle_logs_user_id_cycle_log_id",
                        columns: x => new { x.user_id, x.cycle_log_id },
                        principalSchema: "app",
                        principalTable: "cycle_logs",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_phase_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "symptom_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    symptom_type = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symptom_logs", x => x.id);
                    table.UniqueConstraint("uq_symptom_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_symptom_logs_cycle_logs_user_id_cycle_log_id",
                        columns: x => new { x.user_id, x.cycle_log_id },
                        principalSchema: "app",
                        principalTable: "cycle_logs",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_symptom_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "food_item_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    serving_size = table.Column<decimal>(type: "numeric", nullable: false),
                    serving_unit = table.Column<string>(type: "text", nullable: false),
                    servings_count = table.Column<decimal>(type: "numeric", nullable: false),
                    calories = table.Column<decimal>(type: "numeric", nullable: false),
                    protein = table.Column<decimal>(type: "numeric", nullable: false),
                    carbs = table.Column<decimal>(type: "numeric", nullable: false),
                    fat = table.Column<decimal>(type: "numeric", nullable: false),
                    logged_at_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_item_entries", x => x.id);
                    table.UniqueConstraint("uq_food_item_entries_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_food_item_entries_daily_nutrition_logs_user_id_daily_log_id",
                        columns: x => new { x.user_id, x.daily_log_id },
                        principalSchema: "app",
                        principalTable: "daily_nutrition_logs",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_food_item_entries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "logged_meal_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logged_meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_food_item_id = table.Column<int>(type: "integer", nullable: true),
                    user_food_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grams = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logged_meal_entries", x => x.id);
                    table.UniqueConstraint("uq_logged_meal_entries_user_id_id", x => new { x.user_id, x.id });
                    table.CheckConstraint("ck_logged_meal_entries_exactly_one_food_ref", "(catalog_food_item_id IS NOT NULL AND user_food_item_id IS NULL) OR (catalog_food_item_id IS NULL AND user_food_item_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_logged_meal_entries_food_items_catalog_food_item_id",
                        column: x => x.catalog_food_item_id,
                        principalSchema: "catalog",
                        principalTable: "food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_logged_meal_entries_logged_meals_user_id_logged_meal_id",
                        columns: x => new { x.user_id, x.logged_meal_id },
                        principalSchema: "app",
                        principalTable: "logged_meals",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_logged_meal_entries_user_food_items_user_id_user_food_item_~",
                        columns: x => new { x.user_id, x.user_food_item_id },
                        principalSchema: "app",
                        principalTable: "user_food_items",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_logged_meal_entries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_meal_template_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_food_item_id = table.Column<int>(type: "integer", nullable: true),
                    user_food_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grams = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meal_template_entries", x => x.id);
                    table.UniqueConstraint("uq_user_meal_template_entries_user_id_id", x => new { x.user_id, x.id });
                    table.CheckConstraint("ck_user_meal_template_entries_exactly_one_food_ref", "(catalog_food_item_id IS NOT NULL AND user_food_item_id IS NULL) OR (catalog_food_item_id IS NULL AND user_food_item_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_user_meal_template_entries_food_items_catalog_food_item_id",
                        column: x => x.catalog_food_item_id,
                        principalSchema: "catalog",
                        principalTable: "food_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_meal_template_entries_user_food_items_user_id_user_foo~",
                        columns: x => new { x.user_id, x.user_food_item_id },
                        principalSchema: "app",
                        principalTable: "user_food_items",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_meal_template_entries_user_meal_templates_user_id_meal~",
                        columns: x => new { x.user_id, x.meal_template_id },
                        principalSchema: "app",
                        principalTable: "user_meal_templates",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_meal_template_entries_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_quiz_responses",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_question_id = table.Column<int>(type: "integer", nullable: false),
                    quiz_answer_id = table.Column<int>(type: "integer", nullable: false),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_profile_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_quiz_responses", x => x.id);
                    table.UniqueConstraint("uq_user_quiz_responses_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_user_quiz_responses_quiz_answers_quiz_answer_id",
                        column: x => x.quiz_answer_id,
                        principalSchema: "catalog",
                        principalTable: "quiz_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quiz_responses_quiz_questions_quiz_question_id",
                        column: x => x.quiz_question_id,
                        principalSchema: "catalog",
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quiz_responses_user_profile_snapshots_user_id_user_pro~",
                        columns: x => new { x.user_id, x.user_profile_snapshot_id },
                        principalSchema: "app",
                        principalTable: "user_profile_snapshots",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_quiz_responses_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_days",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    workout_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    week_index = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_days", x => x.id);
                    table.UniqueConstraint("uq_workout_days_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_days_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workout_days_workout_plans_user_id_workout_plan_id",
                        columns: x => new { x.user_id, x.workout_plan_id },
                        principalSchema: "app",
                        principalTable: "workout_plans",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercise_set_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_session_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<string>(type: "text", nullable: true),
                    exercise_name = table.Column<string>(type: "text", nullable: false),
                    set_number = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    reps_completed = table.Column<int>(type: "integer", nullable: false),
                    rpe = table.Column<decimal>(type: "numeric", nullable: true),
                    is_warmup = table.Column<bool>(type: "boolean", nullable: false),
                    rest_time_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_set_logs", x => x.id);
                    table.UniqueConstraint("uq_exercise_set_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_exercise_set_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exercise_set_logs_workout_session_logs_user_id_workout_sess~",
                        columns: x => new { x.user_id, x.workout_session_log_id },
                        principalSchema: "app",
                        principalTable: "workout_session_logs",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_day_exercises",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<int>(type: "integer", nullable: false),
                    sets = table.Column<int>(type: "integer", nullable: false),
                    reps = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_day_exercises", x => x.id);
                    table.UniqueConstraint("uq_workout_day_exercises_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_day_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "catalog",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_day_exercises_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workout_day_exercises_workout_days_user_id_workout_day_id",
                        columns: x => new { x.user_id, x.workout_day_id },
                        principalSchema: "app",
                        principalTable: "workout_days",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    completion_percent = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_logs", x => x.id);
                    table.UniqueConstraint("uq_workout_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workout_logs_workout_days_user_id_workout_day_id",
                        columns: x => new { x.user_id, x.workout_day_id },
                        principalSchema: "app",
                        principalTable: "workout_days",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workout_exercise_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workout_day_exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<int>(type: "integer", nullable: false),
                    completed_sets = table.Column<int>(type: "integer", nullable: false),
                    completed_reps = table.Column<int>(type: "integer", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric", nullable: true),
                    completed_duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    distance_km = table.Column<decimal>(type: "numeric", nullable: true),
                    average_heart_rate_bpm = table.Column<int>(type: "integer", nullable: true),
                    pace_seconds_per_km = table.Column<int>(type: "integer", nullable: true),
                    power_watts = table.Column<int>(type: "integer", nullable: true),
                    cadence_rpm = table.Column<int>(type: "integer", nullable: true),
                    effort_rating = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_exercise_logs", x => x.id);
                    table.UniqueConstraint("uq_workout_exercise_logs_user_id_id", x => new { x.user_id, x.id });
                    table.ForeignKey(
                        name: "FK_workout_exercise_logs_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalSchema: "catalog",
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_exercise_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workout_exercise_logs_workout_day_exercises_user_id_workout~",
                        columns: x => new { x.user_id, x.workout_day_exercise_id },
                        principalSchema: "app",
                        principalTable: "workout_day_exercises",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_exercise_logs_workout_logs_user_id_workout_log_id",
                        columns: x => new { x.user_id, x.workout_log_id },
                        principalSchema: "app",
                        principalTable: "workout_logs",
                        principalColumns: new[] { "user_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cycle_logs_user_id",
                schema: "app",
                table: "cycle_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cycle_phase_logs_user_id",
                schema: "app",
                table: "cycle_phase_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_phase_logs_user_id_cycle_log_id",
                schema: "app",
                table: "cycle_phase_logs",
                columns: new[] { "user_id", "cycle_log_id" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_nutrition_logs_owner_local_date",
                schema: "app",
                table: "daily_nutrition_logs",
                columns: new[] { "user_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_daily_nutrition_logs_user_id",
                schema: "app",
                table: "daily_nutrition_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_daily_readiness_logs_user_id",
                schema: "app",
                table: "daily_readiness_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_muscle_contributions_exercise_id_muscle_group_id",
                schema: "catalog",
                table: "exercise_muscle_contributions",
                columns: new[] { "exercise_id", "muscle_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercise_muscle_contributions_muscle_group_id",
                schema: "catalog",
                table: "exercise_muscle_contributions",
                column: "muscle_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_muscle_contributions_seed_code",
                schema: "catalog",
                table: "exercise_muscle_contributions",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exercise_set_logs_user_id",
                schema: "app",
                table: "exercise_set_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_set_logs_user_id_workout_session_log_id",
                schema: "app",
                table: "exercise_set_logs",
                columns: new[] { "user_id", "workout_session_log_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exercises_code",
                schema: "catalog",
                table: "exercises",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_name",
                schema: "catalog",
                table: "exercises",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_seed_code",
                schema: "catalog",
                table: "exercises",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_food_item_entries_user_id",
                schema: "app",
                table: "food_item_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_food_item_entries_user_id_daily_log_id",
                schema: "app",
                table: "food_item_entries",
                columns: new[] { "user_id", "daily_log_id" });

            migrationBuilder.CreateIndex(
                name: "IX_food_item_versions_food_item_id_versioned_at",
                schema: "catalog",
                table: "food_item_versions",
                columns: new[] { "food_item_id", "versioned_at" });

            migrationBuilder.CreateIndex(
                name: "IX_food_items_brand_name",
                schema: "catalog",
                table: "food_items",
                column: "brand_name");

            migrationBuilder.CreateIndex(
                name: "IX_food_items_brand_owner",
                schema: "catalog",
                table: "food_items",
                column: "brand_owner");

            migrationBuilder.CreateIndex(
                name: "IX_food_items_data_type_name",
                schema: "catalog",
                table: "food_items",
                columns: new[] { "data_type", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_food_items_fdc_id",
                schema: "catalog",
                table: "food_items",
                column: "fdc_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_items_gtin_upc",
                schema: "catalog",
                table: "food_items",
                column: "gtin_upc");

            migrationBuilder.CreateIndex(
                name: "IX_food_items_name",
                schema: "catalog",
                table: "food_items",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_food_items_seed_code",
                schema: "catalog",
                table: "food_items",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_food_sync_logs_started_at",
                schema: "ops",
                table: "food_sync_logs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_goal_tier_weights_goal_muscle_group_id",
                schema: "catalog",
                table: "goal_tier_weights",
                columns: new[] { "goal", "muscle_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goal_tier_weights_muscle_group_id",
                schema: "catalog",
                table: "goal_tier_weights",
                column: "muscle_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_tier_weights_seed_code",
                schema: "catalog",
                table: "goal_tier_weights",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_logged_meal_entries_catalog_food_item_id",
                schema: "app",
                table: "logged_meal_entries",
                column: "catalog_food_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_logged_meal_entries_user_id",
                schema: "app",
                table: "logged_meal_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_logged_meal_entries_user_id_logged_meal_id",
                schema: "app",
                table: "logged_meal_entries",
                columns: new[] { "user_id", "logged_meal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_logged_meal_entries_user_id_user_food_item_id",
                schema: "app",
                table: "logged_meal_entries",
                columns: new[] { "user_id", "user_food_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_logged_meals_user_id",
                schema: "app",
                table: "logged_meals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_template_entries_food_item_id",
                schema: "catalog",
                table: "meal_template_entries",
                column: "food_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_template_entries_meal_template_id",
                schema: "catalog",
                table: "meal_template_entries",
                column: "meal_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_template_entries_seed_code",
                schema: "catalog",
                table: "meal_template_entries",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_templates_seed_code",
                schema: "catalog",
                table: "meal_templates",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_journey_entries_user_id",
                schema: "app",
                table: "photo_journey_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_answers_question_id_order_index",
                schema: "catalog",
                table: "quiz_answers",
                columns: new[] { "question_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_answers_seed_code",
                schema: "catalog",
                table: "quiz_answers",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_order_index",
                schema: "catalog",
                table: "quiz_questions",
                column: "order_index",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_seed_code",
                schema: "catalog",
                table: "quiz_questions",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipes_user_id",
                schema: "app",
                table: "recipes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_meal_templates_user_id",
                schema: "app",
                table: "saved_meal_templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_archetypes_code",
                schema: "catalog",
                table: "session_archetypes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_archetypes_seed_code",
                schema: "catalog",
                table: "session_archetypes",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slot_templates_archetype_id_order",
                schema: "catalog",
                table: "slot_templates",
                columns: new[] { "archetype_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slot_templates_primary_muscle_id",
                schema: "catalog",
                table: "slot_templates",
                column: "primary_muscle_id");

            migrationBuilder.CreateIndex(
                name: "IX_slot_templates_seed_code",
                schema: "catalog",
                table: "slot_templates",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_symptom_logs_user_id",
                schema: "app",
                table: "symptom_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_symptom_logs_user_id_cycle_log_id",
                schema: "app",
                table: "symptom_logs",
                columns: new[] { "user_id", "cycle_log_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_exercise_preferences_original_exercise_id",
                schema: "app",
                table: "user_exercise_preferences",
                column: "original_exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_exercise_preferences_preferred_exercise_id",
                schema: "app",
                table: "user_exercise_preferences",
                column: "preferred_exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_exercise_preferences_user_id",
                schema: "app",
                table: "user_exercise_preferences",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_exercise_preferences_owner_original",
                schema: "app",
                table: "user_exercise_preferences",
                columns: new[] { "user_id", "original_exercise_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_food_items_user_id",
                schema: "app",
                table: "user_food_items",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_template_entries_catalog_food_item_id",
                schema: "app",
                table: "user_meal_template_entries",
                column: "catalog_food_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_meal_template_entries_user_id",
                schema: "app",
                table: "user_meal_template_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_template_entries_user_id_meal_template_id",
                schema: "app",
                table: "user_meal_template_entries",
                columns: new[] { "user_id", "meal_template_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_template_entries_user_id_user_food_item_id",
                schema: "app",
                table: "user_meal_template_entries",
                columns: new[] { "user_id", "user_food_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_meal_templates_user_id",
                schema: "app",
                table: "user_meal_templates",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_movement_notes_exercise_id",
                schema: "app",
                table: "user_movement_notes",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_movement_notes_user_id",
                schema: "app",
                table: "user_movement_notes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_snapshots_user_id",
                schema: "app",
                table: "user_profile_snapshots",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_user_profiles_user_id",
                schema: "app",
                table: "user_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_quiz_answer_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_quiz_question_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "quiz_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_quiz_responses_user_id",
                schema: "app",
                table: "user_quiz_responses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_quiz_responses_user_id_user_profile_snapshot_id",
                schema: "app",
                table: "user_quiz_responses",
                columns: new[] { "user_id", "user_profile_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "uq_users_apple_user_id",
                schema: "app",
                table: "users",
                column: "apple_user_id",
                unique: true,
                filter: "apple_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_users_normalized_email",
                schema: "app",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_volume_budget_rows_days_per_week_muscle_group_id",
                schema: "catalog",
                table: "volume_budget_rows",
                columns: new[] { "days_per_week", "muscle_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_volume_budget_rows_muscle_group_id",
                schema: "catalog",
                table: "volume_budget_rows",
                column: "muscle_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_volume_budget_rows_seed_code",
                schema: "catalog",
                table: "volume_budget_rows",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_week_templates_days_per_week",
                schema: "catalog",
                table: "week_templates",
                column: "days_per_week",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_week_templates_seed_code",
                schema: "catalog",
                table: "week_templates",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_day_exercises_exercise_id",
                schema: "app",
                table: "workout_day_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_workout_day_exercises_user_id",
                schema: "app",
                table: "workout_day_exercises",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_day_exercises_user_id_workout_day_id",
                schema: "app",
                table: "workout_day_exercises",
                columns: new[] { "user_id", "workout_day_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workout_days_user_id",
                schema: "app",
                table: "workout_days",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_days_user_id_workout_plan_id",
                schema: "app",
                table: "workout_days",
                columns: new[] { "user_id", "workout_plan_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_definitions_name",
                schema: "catalog",
                table: "workout_exercise_definitions",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_definitions_pattern",
                schema: "catalog",
                table: "workout_exercise_definitions",
                column: "pattern");

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_definitions_seed_code",
                schema: "catalog",
                table: "workout_exercise_definitions",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_logs_exercise_id",
                schema: "app",
                table: "workout_exercise_logs",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "ix_workout_exercise_logs_user_id",
                schema: "app",
                table: "workout_exercise_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_logs_user_id_workout_day_exercise_id",
                schema: "app",
                table: "workout_exercise_logs",
                columns: new[] { "user_id", "workout_day_exercise_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_exercise_logs_user_id_workout_log_id",
                schema: "app",
                table: "workout_exercise_logs",
                columns: new[] { "user_id", "workout_log_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workout_injury_logs_user_id",
                schema: "app",
                table: "workout_injury_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workout_logs_user_id",
                schema: "app",
                table: "workout_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_logs_user_id_workout_day_id",
                schema: "app",
                table: "workout_logs",
                columns: new[] { "user_id", "workout_day_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workout_muscle_groups_name",
                schema: "catalog",
                table: "workout_muscle_groups",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_muscle_groups_seed_code",
                schema: "catalog",
                table: "workout_muscle_groups",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_planning_config_entries_section_key",
                schema: "catalog",
                table: "workout_planning_config_entries",
                columns: new[] { "section", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_planning_config_entries_seed_code",
                schema: "catalog",
                table: "workout_planning_config_entries",
                column: "seed_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workout_plans_user_id",
                schema: "app",
                table: "workout_plans",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workout_session_logs_user_id",
                schema: "app",
                table: "workout_session_logs",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cycle_phase_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_readiness_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "email_challenges",
                schema: "app");

            migrationBuilder.DropTable(
                name: "exercise_muscle_contributions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "exercise_set_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "food_item_entries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "food_item_versions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "food_sync_logs",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "goal_tier_weights",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "logged_meal_entries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "meal_template_entries",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "photo_journey_entries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "saved_meal_templates",
                schema: "app");

            migrationBuilder.DropTable(
                name: "slot_templates",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "symptom_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_exercise_preferences",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_meal_template_entries",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_movement_notes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_quiz_responses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "volume_budget_rows",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "week_templates",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "workout_exercise_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_injury_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_planning_config_entries",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "workout_session_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_nutrition_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "logged_meals",
                schema: "app");

            migrationBuilder.DropTable(
                name: "meal_templates",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "session_archetypes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "cycle_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_exercise_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "food_items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "user_food_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_meal_templates",
                schema: "app");

            migrationBuilder.DropTable(
                name: "quiz_answers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "user_profile_snapshots",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_muscle_groups",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "workout_day_exercises",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "quiz_questions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "exercises",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "workout_days",
                schema: "app");

            migrationBuilder.DropTable(
                name: "workout_plans",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");
        }
    }
}
