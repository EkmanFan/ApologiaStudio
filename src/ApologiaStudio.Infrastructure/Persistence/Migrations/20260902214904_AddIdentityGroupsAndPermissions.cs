using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApologiaStudio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityGroupsAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TargetUserId",
                table: "identity_administration_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "identity_administration_events",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetGroupId",
                table: "identity_administration_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetRoleId",
                table: "identity_administration_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "identity_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_groups_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_group_memberships",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_group_memberships", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_identity_group_memberships_AspNetUsers_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identity_group_memberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_group_memberships_identity_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "identity_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_group_roles",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_group_roles", x => new { x.GroupId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_identity_group_roles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identity_group_roles_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_identity_group_roles_identity_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "identity_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_identity_administration_events_TargetGroupId",
                table: "identity_administration_events",
                column: "TargetGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_administration_events_TargetRoleId",
                table: "identity_administration_events",
                column: "TargetRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_group_memberships_AddedByUserId",
                table: "identity_group_memberships",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_group_memberships_UserId",
                table: "identity_group_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_group_roles_AssignedByUserId",
                table: "identity_group_roles",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_group_roles_RoleId",
                table: "identity_group_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_groups_CreatedByUserId",
                table: "identity_groups",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_groups_NormalizedName",
                table: "identity_groups",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_identity_administration_events_AspNetRoles_TargetRoleId",
                table: "identity_administration_events",
                column: "TargetRoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_identity_administration_events_identity_groups_TargetGroupId",
                table: "identity_administration_events",
                column: "TargetGroupId",
                principalTable: "identity_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_identity_administration_events_AspNetRoles_TargetRoleId",
                table: "identity_administration_events");

            migrationBuilder.DropForeignKey(
                name: "FK_identity_administration_events_identity_groups_TargetGroupId",
                table: "identity_administration_events");

            migrationBuilder.DropTable(
                name: "identity_group_memberships");

            migrationBuilder.DropTable(
                name: "identity_group_roles");

            migrationBuilder.DropTable(
                name: "identity_groups");

            migrationBuilder.DropIndex(
                name: "IX_identity_administration_events_TargetGroupId",
                table: "identity_administration_events");

            migrationBuilder.DropIndex(
                name: "IX_identity_administration_events_TargetRoleId",
                table: "identity_administration_events");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "identity_administration_events");

            migrationBuilder.DropColumn(
                name: "TargetGroupId",
                table: "identity_administration_events");

            migrationBuilder.DropColumn(
                name: "TargetRoleId",
                table: "identity_administration_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetUserId",
                table: "identity_administration_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
