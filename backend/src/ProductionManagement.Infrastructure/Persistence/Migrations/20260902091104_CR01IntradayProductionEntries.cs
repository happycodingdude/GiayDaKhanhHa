using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CR01IntradayProductionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // production_records ĐỔI TÊN thành production_days chứ không bị drop-and-create: bảng đã
            // có dữ liệu sản xuất thật (CR-01 §5.1). Toàn bộ phần ALTER dưới đây được viết tay vì
            // scaffold của EF chỉ nhìn thấy "bảng cũ biến mất, bảng mới xuất hiện".
            migrationBuilder.Sql("""
                ALTER TABLE production_records RENAME TO production_days;

                ALTER TABLE production_days RENAME CONSTRAINT "PK_production_records" TO "PK_production_days";
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_order           TO fk_production_days_order;
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_created_by      TO fk_production_days_created_by;
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_records_updated_by      TO fk_production_days_updated_by;
                ALTER TABLE production_days RENAME CONSTRAINT ck_production_records_actual_quantity TO ck_production_days_actual_quantity;

                ALTER INDEX uq_production_records_order_date       RENAME TO uq_production_days_order_date;
                ALTER INDEX "IX_production_records_created_by"     RENAME TO "IX_production_days_created_by";
                ALTER INDEX "IX_production_records_updated_by"     RENAME TO "IX_production_days_updated_by";
                """);

            // actual_quantity chỉ có giá trị khi ngày đã Xuất hàng (CR-01 OV-9).
            migrationBuilder.Sql("""
                ALTER TABLE production_days ALTER COLUMN actual_quantity DROP NOT NULL;

                ALTER TABLE production_days
                    ADD COLUMN status    varchar(20) NOT NULL DEFAULT 'Open',
                    ADD COLUMN closed_at timestamptz NULL,
                    ADD COLUMN closed_by uuid        NULL;

                ALTER TABLE production_days ALTER COLUMN status DROP DEFAULT;
                """);

            // Mỗi bản ghi sản lượng cũ là một con số đã chốt cho cả ngày, nên nó chuyển thành một
            // ngày ĐÃ Xuất hàng. Giữ nguyên ngữ nghĩa cũ và cũng giữ hiệu lực cho các khoản bù đã
            // áp dụng — sau CR-01 khoản bù chỉ tạo được từ ngày đã đóng (CR-01 §6.7).
            migrationBuilder.Sql("""
                UPDATE production_days
                SET status    = 'Closed',
                    closed_at = updated_at,
                    closed_by = updated_by;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE production_days
                    ADD CONSTRAINT fk_production_days_closed_by
                        FOREIGN KEY (closed_by) REFERENCES users(id) ON DELETE RESTRICT;

                ALTER TABLE production_days
                    ADD CONSTRAINT ck_production_days_status CHECK (status IN ('Open', 'Closed'));

                ALTER TABLE production_days
                    ADD CONSTRAINT ck_production_days_closed_consistency CHECK (
                        (status = 'Closed' AND closed_at IS NOT NULL AND closed_by IS NOT NULL AND actual_quantity IS NOT NULL)
                        OR
                        (status = 'Open' AND closed_at IS NULL AND closed_by IS NULL AND actual_quantity IS NULL));

                CREATE INDEX ix_production_days_status_date ON production_days (status, production_date);
                CREATE INDEX "IX_production_days_closed_by" ON production_days (closed_by);
                """);

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recording_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    day_start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    day_end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                    table.CheckConstraint("ck_system_settings_interval", "recording_interval_minutes BETWEEN 5 AND 480");
                    table.CheckConstraint("ck_system_settings_time_range", "day_end_time > day_start_time");
                    table.ForeignKey(
                        name: "fk_system_settings_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_day_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_entries", x => x.id);
                    table.CheckConstraint("ck_production_entries_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_production_entries_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entries_day",
                        column: x => x.production_day_id,
                        principalTable: "production_days",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entries_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_entry_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_quantity = table.Column<int>(type: "integer", nullable: true),
                    new_quantity = table.Column<int>(type: "integer", nullable: true),
                    old_note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    new_note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_production_entry_logs", x => x.id);
                    table.CheckConstraint("ck_production_entry_logs_action", "action IN ('Create', 'Update', 'Delete')");
                    table.ForeignKey(
                        name: "fk_production_entry_logs_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entry_logs_entry",
                        column: x => x.production_entry_id,
                        principalTable: "production_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });






            migrationBuilder.CreateIndex(
                name: "IX_production_entries_created_by",
                table: "production_entries",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_production_entries_updated_by",
                table: "production_entries",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_production_entries_day_active",
                table: "production_entries",
                column: "production_day_id",
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_production_entries_day_recorded_at",
                table: "production_entries",
                columns: new[] { "production_day_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_production_entry_logs_changed_by",
                table: "production_entry_logs",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_logs_entry",
                table: "production_entry_logs",
                columns: new[] { "production_entry_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_updated_by",
                table: "system_settings",
                column: "updated_by");

            // Sản lượng cũ được dựng lại thành đúng một lần ghi nhận cho ngày đó, để tổng các lần
            // ghi nhận khớp với ảnh chụp actual_quantity. Ngày có sản lượng 0 chuyển thành ngày đã
            // đóng với 0 lần ghi nhận — đúng cách CR-01 thể hiện "cả ngày không sản xuất được".
            migrationBuilder.Sql("""
                INSERT INTO production_entries
                    (id, production_day_id, quantity, recorded_at, note, deleted_at,
                     created_by, updated_by, created_at, updated_at)
                SELECT gen_random_uuid(), d.id, d.actual_quantity, d.updated_at, NULL, NULL,
                       d.created_by, d.updated_by, d.created_at, d.updated_at
                FROM production_days d
                WHERE d.actual_quantity > 0;

                INSERT INTO production_entry_logs
                    (id, production_entry_id, action, old_quantity, new_quantity, old_note, new_note,
                     changed_by, changed_at)
                SELECT gen_random_uuid(), e.id, 'Create', NULL, e.quantity, NULL, NULL,
                       e.created_by, e.created_at
                FROM production_entries e;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Đảo ngược đúng những gì Up đã làm. Sản lượng quay về một giá trị duy nhất trong ngày:
            // các lần ghi nhận được gộp lại, nên ngày còn mở cũng có một con số để trả về cột
            // NOT NULL của schema cũ.
            migrationBuilder.Sql("""
                UPDATE production_days d
                SET actual_quantity = COALESCE(d.actual_quantity, (
                    SELECT COALESCE(SUM(e.quantity), 0)
                    FROM production_entries e
                    WHERE e.production_day_id = d.id AND e.deleted_at IS NULL));
                """);

            migrationBuilder.DropTable(name: "production_entry_logs");
            migrationBuilder.DropTable(name: "production_entries");
            migrationBuilder.DropTable(name: "system_settings");

            migrationBuilder.Sql("""
                DROP INDEX ix_production_days_status_date;
                DROP INDEX "IX_production_days_closed_by";

                ALTER TABLE production_days DROP CONSTRAINT ck_production_days_closed_consistency;
                ALTER TABLE production_days DROP CONSTRAINT ck_production_days_status;
                ALTER TABLE production_days DROP CONSTRAINT fk_production_days_closed_by;

                ALTER TABLE production_days
                    DROP COLUMN status,
                    DROP COLUMN closed_at,
                    DROP COLUMN closed_by;

                ALTER TABLE production_days ALTER COLUMN actual_quantity SET NOT NULL;

                ALTER INDEX "IX_production_days_updated_by"     RENAME TO "IX_production_records_updated_by";
                ALTER INDEX "IX_production_days_created_by"     RENAME TO "IX_production_records_created_by";
                ALTER INDEX uq_production_days_order_date       RENAME TO uq_production_records_order_date;

                ALTER TABLE production_days RENAME CONSTRAINT ck_production_days_actual_quantity TO ck_production_records_actual_quantity;
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_days_updated_by      TO fk_production_records_updated_by;
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_days_created_by      TO fk_production_records_created_by;
                ALTER TABLE production_days RENAME CONSTRAINT fk_production_days_order           TO fk_production_records_order;
                ALTER TABLE production_days RENAME CONSTRAINT "PK_production_days" TO "PK_production_records";

                ALTER TABLE production_days RENAME TO production_records;
                """);
        }
    }
}
