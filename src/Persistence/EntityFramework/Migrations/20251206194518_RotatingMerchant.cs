using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RotatingMerchant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMerchantPurchase",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    MerchantId = table.Column<short>(type: "smallint", nullable: false),
                    ItemKey = table.Column<string>(type: "text", nullable: false),
                    RotationId = table.Column<string>(type: "text", nullable: false),
                    PurchaseDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMerchantPurchase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountMerchantPurchase_Account_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "data",
                        principalTable: "Account",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountMerchantPurchase_Account_AccountId1",
                        column: x => x.AccountId1,
                        principalSchema: "data",
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMerchantPurchase_AccountId",
                schema: "data",
                table: "AccountMerchantPurchase",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMerchantPurchase_AccountId1",
                schema: "data",
                table: "AccountMerchantPurchase",
                column: "AccountId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMerchantPurchase",
                schema: "data");
        }
    }
}
