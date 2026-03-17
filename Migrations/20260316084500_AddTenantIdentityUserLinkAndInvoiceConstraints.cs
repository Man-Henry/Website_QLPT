using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website_QLPT.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdentityUserLinkAndInvoiceConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityUserId",
                table: "Tenants",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE t
SET t.IdentityUserId = u.Id
FROM Tenants t
INNER JOIN AspNetUsers u
    ON UPPER(LTRIM(RTRIM(t.Email))) = UPPER(LTRIM(RTRIM(u.Email)))
WHERE t.IdentityUserId IS NULL
  AND t.Email IS NOT NULL
  AND u.Email IS NOT NULL
  AND 1 = (
      SELECT COUNT(*)
      FROM Tenants tx
      WHERE tx.IdentityUserId IS NULL
        AND tx.Email IS NOT NULL
        AND UPPER(LTRIM(RTRIM(tx.Email))) = UPPER(LTRIM(RTRIM(t.Email)))
  )
  AND 1 = (
      SELECT COUNT(*)
      FROM AspNetUsers ux
      WHERE ux.Email IS NOT NULL
        AND UPPER(LTRIM(RTRIM(ux.Email))) = UPPER(LTRIM(RTRIM(t.Email)))
  )
  AND NOT EXISTS (
      SELECT 1
      FROM Tenants linked
      WHERE linked.Id <> t.Id
        AND linked.IdentityUserId = u.Id
  );");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_IdentityUserId",
                table: "Tenants",
                column: "IdentityUserId",
                unique: true,
                filter: "[IdentityUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_Month_Year",
                table: "Invoices",
                columns: new[] { "ContractId", "Month", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_AspNetUsers_IdentityUserId",
                table: "Tenants",
                column: "IdentityUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_AspNetUsers_IdentityUserId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_IdentityUserId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContractId_Month_Year",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "Tenants");
        }
    }
}
