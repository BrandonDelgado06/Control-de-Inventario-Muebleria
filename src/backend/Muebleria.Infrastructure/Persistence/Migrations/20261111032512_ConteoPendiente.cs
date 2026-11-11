using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muebleria.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// RF-28: la cantidad contada queda en NULL mientras el artículo no se ha contado.
    /// SQL Server no deja cambiar una columna que usa una columna calculada, así que
    /// "diferencia" se quita y se vuelve a crear con la misma fórmula.
    /// </summary>
    public partial class ConteoPendiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "diferencia", table: "CONTEO_DETALLE");

            migrationBuilder.AlterColumn<decimal>(
                name: "cantidad_contada",
                table: "CONTEO_DETALLE",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "diferencia",
                table: "CONTEO_DETALLE",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                computedColumnSql: "[cantidad_contada] - [stock_sistema]",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "diferencia", table: "CONTEO_DETALLE");

            migrationBuilder.Sql("UPDATE CONTEO_DETALLE SET cantidad_contada = 0 WHERE cantidad_contada IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "cantidad_contada",
                table: "CONTEO_DETALLE",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "diferencia",
                table: "CONTEO_DETALLE",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                computedColumnSql: "[cantidad_contada] - [stock_sistema]",
                stored: true);
        }
    }
}
