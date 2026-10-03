using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductionManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmProductionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "schedule_confirmed_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Tiến độ lập trước khi có bước chốt đã được sản xuất theo luồng cũ, nên coi như đã chốt.
            migrationBuilder.Sql(
                "UPDATE orders SET schedule_confirmed_at = updated_at WHERE status <> 'Pending';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_orders_schedule_confirmed",
                table: "orders",
                sql: "status <> 'Pending' OR schedule_confirmed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_orders_schedule_confirmed",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "schedule_confirmed_at",
                table: "orders");
        }
    }
}
