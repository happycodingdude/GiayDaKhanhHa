using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CR001GoodsReceiptScheduleProductionLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CR-001 là breaking change về cả domain lẫn schema, và môi trường chưa có dữ liệu sản
            // xuất thật nên chiến lược đã chốt là reset thay vì backfill (CR-001 §5.8).
            //
            // Vì sao phải xoá trước khi ALTER: production_plans và production_days nhận thêm cột
            // production_line_id NOT NULL có khoá ngoại; mọi dòng cũ sẽ mang giá trị mặc định
            // Guid.Empty và lập tức vi phạm khoá ngoại đó. Đơn hàng cũ cũng không thoả
            // ck_orders_schedule_dates vì chúng có ngày nhưng chưa có dây chuyền nào.
            //
            // users và system_settings được giữ nguyên: tài khoản đăng nhập và cấu hình vận hành
            // không liên quan gì tới thay đổi này.
            migrationBuilder.Sql("DELETE FROM plan_adjustment_items");
            migrationBuilder.Sql("DELETE FROM plan_adjustments");
            migrationBuilder.Sql("DELETE FROM production_entry_logs");
            migrationBuilder.Sql("DELETE FROM production_entries");
            migrationBuilder.Sql("DELETE FROM production_days");
            migrationBuilder.Sql("DELETE FROM production_plans");
            migrationBuilder.Sql("DELETE FROM orders");

            migrationBuilder.DropIndex(
                name: "uq_production_plans_order_date",
                table: "production_plans");

            migrationBuilder.DropIndex(
                name: "uq_production_days_order_date",
                table: "production_days");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_date_range",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.RenameColumn(
                name: "order_code",
                table: "orders",
                newName: "shoe_code");

            migrationBuilder.RenameIndex(
                name: "uq_orders_order_code",
                table: "orders",
                newName: "uq_orders_shoe_code");

            migrationBuilder.AddColumn<Guid>(
                name: "production_line_id",
                table: "production_plans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "production_line_id",
                table: "production_days",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "orders",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_date",
                table: "orders",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<string>(
                name: "image_content_type",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_file_name",
                table: "orders",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_path",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "image_size_bytes",
                table: "orders",
                type: "integer",
                nullable: true);

            // EF sinh ADD COLUMN ... NOT NULL DEFAULT '000...' để lấp dòng cũ, nhưng không tự gỡ
            // DEFAULT sau đó. Bảng đang rỗng nên default không lấp gì cả — để lại thì một dòng
            // thiếu production_line_id sẽ lặng lẽ nhận Guid rỗng thay vì bị database từ chối.
            migrationBuilder.Sql("ALTER TABLE production_plans ALTER COLUMN production_line_id DROP DEFAULT");
            migrationBuilder.Sql("ALTER TABLE production_days ALTER COLUMN production_line_id DROP DEFAULT");

            migrationBuilder.CreateTable(
                name: "production_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_lines", x => x.id);
                    table.CheckConstraint("ck_production_lines_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_production_lines_status", "status IN ('Active', 'Inactive')");
                });

            migrationBuilder.CreateTable(
                name: "order_production_lines",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocated_quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_production_lines", x => new { x.order_id, x.production_line_id });
                    table.CheckConstraint("ck_order_production_lines_allocated", "allocated_quantity > 0");
                    table.ForeignKey(
                        name: "fk_order_production_lines_line",
                        column: x => x.production_line_id,
                        principalTable: "production_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_production_lines_order",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_production_plans_line",
                table: "production_plans",
                column: "production_line_id");

            migrationBuilder.CreateIndex(
                name: "uq_production_plans_order_date_line",
                table: "production_plans",
                columns: new[] { "order_id", "production_date", "production_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_production_days_line",
                table: "production_days",
                column: "production_line_id");

            migrationBuilder.CreateIndex(
                name: "uq_production_days_order_date_line",
                table: "production_days",
                columns: new[] { "order_id", "production_date", "production_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_image",
                table: "orders",
                sql: "(image_path IS NULL AND image_file_name IS NULL AND image_content_type IS NULL AND image_size_bytes IS NULL) OR (image_path IS NOT NULL AND image_file_name IS NOT NULL AND image_content_type IS NOT NULL AND image_size_bytes IS NOT NULL AND image_size_bytes > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_schedule_dates",
                table: "orders",
                sql: "(status = 'Pending' AND start_date IS NULL AND due_date IS NULL) OR (status <> 'Pending' AND start_date IS NOT NULL AND due_date IS NOT NULL AND start_date <= due_date)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN ('Pending', 'Incomplete', 'Completed')");

            migrationBuilder.CreateIndex(
                name: "ix_order_production_lines_line",
                table: "order_production_lines",
                column: "production_line_id");

            migrationBuilder.CreateIndex(
                name: "uq_production_lines_code",
                table: "production_lines",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_production_days_line",
                table: "production_days",
                column: "production_line_id",
                principalTable: "production_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_production_plans_line",
                table: "production_plans",
                column: "production_line_id",
                principalTable: "production_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_production_days_line",
                table: "production_days");

            migrationBuilder.DropForeignKey(
                name: "fk_production_plans_line",
                table: "production_plans");

            migrationBuilder.DropTable(
                name: "order_production_lines");

            migrationBuilder.DropTable(
                name: "production_lines");

            migrationBuilder.DropIndex(
                name: "ix_production_plans_line",
                table: "production_plans");

            migrationBuilder.DropIndex(
                name: "uq_production_plans_order_date_line",
                table: "production_plans");

            migrationBuilder.DropIndex(
                name: "ix_production_days_line",
                table: "production_days");

            migrationBuilder.DropIndex(
                name: "uq_production_days_order_date_line",
                table: "production_days");

            migrationBuilder.DropIndex(
                name: "ix_orders_status",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_image",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_schedule_dates",
                table: "orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_status",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "production_line_id",
                table: "production_plans");

            migrationBuilder.DropColumn(
                name: "production_line_id",
                table: "production_days");

            migrationBuilder.DropColumn(
                name: "image_content_type",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "image_file_name",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "image_path",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "image_size_bytes",
                table: "orders");

            migrationBuilder.RenameColumn(
                name: "shoe_code",
                table: "orders",
                newName: "order_code");

            migrationBuilder.RenameIndex(
                name: "uq_orders_shoe_code",
                table: "orders",
                newName: "uq_orders_order_code");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "orders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_date",
                table: "orders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_production_plans_order_date",
                table: "production_plans",
                columns: new[] { "order_id", "production_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_production_days_order_date",
                table: "production_days",
                columns: new[] { "order_id", "production_date" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_date_range",
                table: "orders",
                sql: "start_date <= due_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_status",
                table: "orders",
                sql: "status IN ('Incomplete', 'Completed')");
        }
    }
}
