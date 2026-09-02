using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CR01SimplifySystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_system_settings_time_range",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "day_end_time",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "day_start_time",
                table: "system_settings");

            // Dòng cấu hình đã tồn tại được nhận giá trị mặc định của nghiệp vụ (có nhắc trước),
            // rồi DEFAULT bị gỡ để mọi dòng sau này phải nói rõ lựa chọn của mình.
            migrationBuilder.AddColumn<bool>(
                name: "remind_before_due",
                table: "system_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("ALTER TABLE system_settings ALTER COLUMN remind_before_due DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "remind_before_due",
                table: "system_settings");

            // Giờ làm việc mặc định của schema cũ. Không dùng 00:00 cho cả hai cột: ràng buộc
            // ck_system_settings_time_range được thêm lại ngay bên dưới sẽ vi phạm.
            migrationBuilder.AddColumn<TimeOnly>(
                name: "day_end_time",
                table: "system_settings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(17, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "day_start_time",
                table: "system_settings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(8, 0, 0));

            migrationBuilder.AddCheckConstraint(
                name: "ck_system_settings_time_range",
                table: "system_settings",
                sql: "day_end_time > day_start_time");
        }
    }
}
