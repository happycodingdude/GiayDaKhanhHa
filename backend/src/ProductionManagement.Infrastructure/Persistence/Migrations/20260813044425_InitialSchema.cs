using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProductionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.CheckConstraint("ck_orders_date_range", "start_date <= due_date");
                    table.CheckConstraint("ck_orders_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_orders_status", "status IN ('Incomplete', 'Completed')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_status", "status IN ('Active', 'Inactive')");
                });

            migrationBuilder.CreateTable(
                name: "production_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    production_date = table.Column<DateOnly>(type: "date", nullable: false),
                    initial_planned_quantity = table.Column<int>(type: "integer", nullable: false),
                    planned_quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_plans", x => x.id);
                    table.CheckConstraint("ck_production_plans_initial_quantity", "initial_planned_quantity >= 0");
                    table.CheckConstraint("ck_production_plans_quantity", "planned_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_production_plans_order",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_id = table.Column<long>(type: "bigint", nullable: false),
                    production_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_quantity = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    updated_by = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_records", x => x.id);
                    table.CheckConstraint("ck_production_records_actual_quantity", "actual_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_production_records_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_records_order",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_records_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_adjustments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    source_production_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    shortage_quantity = table.Column<int>(type: "integer", nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: false),
                    applied_by = table.Column<long>(type: "bigint", nullable: true),
                    reversed_by = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_adjustments", x => x.id);
                    table.CheckConstraint("ck_plan_adjustments_shortage", "shortage_quantity > 0");
                    table.CheckConstraint("ck_plan_adjustments_status", "status IN ('Applied', 'Reversed')");
                    table.CheckConstraint("ck_plan_adjustments_type", "adjustment_type IN ('Manual', 'Automatic')");
                    table.ForeignKey(
                        name: "fk_plan_adjustments_applied_by",
                        column: x => x.applied_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_adjustments_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_adjustments_reversed_by",
                        column: x => x.reversed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_adjustments_source_plan",
                        column: x => x.source_production_plan_id,
                        principalTable: "production_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_adjustment_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    plan_adjustment_id = table.Column<long>(type: "bigint", nullable: false),
                    production_plan_id = table.Column<long>(type: "bigint", nullable: false),
                    add_on_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_adjustment_items", x => x.id);
                    table.CheckConstraint("ck_plan_adjustment_items_add_on", "add_on_quantity > 0");
                    table.ForeignKey(
                        name: "fk_plan_adjustment_items_adjustment",
                        column: x => x.plan_adjustment_id,
                        principalTable: "plan_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_adjustment_items_target_plan",
                        column: x => x.production_plan_id,
                        principalTable: "production_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_orders_order_code",
                table: "orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plan_adjustment_items_adjustment",
                table: "plan_adjustment_items",
                column: "plan_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_adjustment_items_target_plan",
                table: "plan_adjustment_items",
                column: "production_plan_id");

            migrationBuilder.CreateIndex(
                name: "uq_plan_adjustment_items_adjustment_plan",
                table: "plan_adjustment_items",
                columns: new[] { "plan_adjustment_id", "production_plan_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_adjustments_applied_by",
                table: "plan_adjustments",
                column: "applied_by");

            migrationBuilder.CreateIndex(
                name: "IX_plan_adjustments_created_by",
                table: "plan_adjustments",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_plan_adjustments_reversed_by",
                table: "plan_adjustments",
                column: "reversed_by");

            migrationBuilder.CreateIndex(
                name: "ix_plan_adjustments_source_plan",
                table: "plan_adjustments",
                column: "source_production_plan_id");

            migrationBuilder.CreateIndex(
                name: "uq_production_plans_order_date",
                table: "production_plans",
                columns: new[] { "order_id", "production_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_records_created_by",
                table: "production_records",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_production_records_updated_by",
                table: "production_records",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_production_records_order_date",
                table: "production_records",
                columns: new[] { "order_id", "production_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_adjustment_items");

            migrationBuilder.DropTable(
                name: "production_records");

            migrationBuilder.DropTable(
                name: "plan_adjustments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "production_plans");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
