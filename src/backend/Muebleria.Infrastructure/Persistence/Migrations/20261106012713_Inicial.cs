using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muebleria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CATEGORIA",
                columns: table => new
                {
                    id_categoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    tipo_inventario = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIA", x => x.id_categoria);
                });

            migrationBuilder.CreateTable(
                name: "CONFIGURACION",
                columns: table => new
                {
                    clave = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    valor = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONFIGURACION", x => x.clave);
                });

            migrationBuilder.CreateTable(
                name: "PERMISO",
                columns: table => new
                {
                    id_permiso = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERMISO", x => x.id_permiso);
                });

            migrationBuilder.CreateTable(
                name: "ROL",
                columns: table => new
                {
                    id_rol = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROL", x => x.id_rol);
                });

            migrationBuilder.CreateTable(
                name: "UBICACION",
                columns: table => new
                {
                    id_ubicacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UBICACION", x => x.id_ubicacion);
                });

            migrationBuilder.CreateTable(
                name: "CAMPO_PERSONALIZADO",
                columns: table => new
                {
                    id_campo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_categoria = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    tipo_dato = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    obligatorio = table.Column<bool>(type: "bit", nullable: false),
                    opciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAMPO_PERSONALIZADO", x => x.id_campo);
                    table.ForeignKey(
                        name: "FK_CAMPO_PERSONALIZADO_CATEGORIA_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIA",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ROL_PERMISO",
                columns: table => new
                {
                    id_rol = table.Column<int>(type: "int", nullable: false),
                    id_permiso = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROL_PERMISO", x => new { x.id_rol, x.id_permiso });
                    table.ForeignKey(
                        name: "FK_ROL_PERMISO_PERMISO_id_permiso",
                        column: x => x.id_permiso,
                        principalTable: "PERMISO",
                        principalColumn: "id_permiso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ROL_PERMISO_ROL_id_rol",
                        column: x => x.id_rol,
                        principalTable: "ROL",
                        principalColumn: "id_rol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "USUARIO",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_rol = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    usuario = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    contrasena = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIO", x => x.id_usuario);
                    table.ForeignKey(
                        name: "FK_USUARIO_ROL_id_rol",
                        column: x => x.id_rol,
                        principalTable: "ROL",
                        principalColumn: "id_rol",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ARTICULO",
                columns: table => new
                {
                    id_articulo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    id_categoria = table.Column<int>(type: "int", nullable: false),
                    id_ubicacion = table.Column<int>(type: "int", nullable: false),
                    unidad_medida = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    stock_actual = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    stock_minimo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'Buena'"),
                    lote = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    presentacion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    foto_ruta = table.Column<string>(type: "varchar(260)", unicode: false, maxLength: 260, nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false),
                    version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARTICULO", x => x.id_articulo);
                    table.CheckConstraint("CK_ARTICULO_stock_actual", "[stock_actual] >= 0");
                    table.CheckConstraint("CK_ARTICULO_stock_minimo", "[stock_minimo] >= 0");
                    table.ForeignKey(
                        name: "FK_ARTICULO_CATEGORIA_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIA",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ARTICULO_UBICACION_id_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "UBICACION",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BITACORA",
                columns: table => new
                {
                    id_bitacora = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    modulo = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    accion = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BITACORA", x => x.id_bitacora);
                    table.ForeignKey(
                        name: "FK_BITACORA_USUARIO_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTEO_FISICO",
                columns: table => new
                {
                    id_conteo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    id_ubicacion = table.Column<int>(type: "int", nullable: true),
                    id_categoria = table.Column<int>(type: "int", nullable: true),
                    id_responsable = table.Column<int>(type: "int", nullable: false),
                    estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTEO_FISICO", x => x.id_conteo);
                    table.ForeignKey(
                        name: "FK_CONTEO_FISICO_CATEGORIA_id_categoria",
                        column: x => x.id_categoria,
                        principalTable: "CATEGORIA",
                        principalColumn: "id_categoria",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTEO_FISICO_UBICACION_id_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "UBICACION",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTEO_FISICO_USUARIO_id_responsable",
                        column: x => x.id_responsable,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RESPALDO",
                columns: table => new
                {
                    id_respaldo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    archivo = table.Column<string>(type: "varchar(260)", unicode: false, maxLength: 260, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESPALDO", x => x.id_respaldo);
                    table.ForeignKey(
                        name: "FK_RESPALDO_USUARIO_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ARTICULO_ALIAS",
                columns: table => new
                {
                    id_articulo_alias = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    alias = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARTICULO_ALIAS", x => x.id_articulo_alias);
                    table.ForeignKey(
                        name: "FK_ARTICULO_ALIAS_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ARTICULO_VALOR_CAMPO",
                columns: table => new
                {
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    id_campo = table.Column<int>(type: "int", nullable: false),
                    valor = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARTICULO_VALOR_CAMPO", x => new { x.id_articulo, x.id_campo });
                    table.ForeignKey(
                        name: "FK_ARTICULO_VALOR_CAMPO_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ARTICULO_VALOR_CAMPO_CAMPO_PERSONALIZADO_id_campo",
                        column: x => x.id_campo,
                        principalTable: "CAMPO_PERSONALIZADO",
                        principalColumn: "id_campo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HISTORIAL_ESTADO",
                columns: table => new
                {
                    id_historial_estado = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    estado_anterior = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    estado_nuevo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HISTORIAL_ESTADO", x => x.id_historial_estado);
                    table.ForeignKey(
                        name: "FK_HISTORIAL_ESTADO_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HISTORIAL_ESTADO_USUARIO_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MOVIMIENTO",
                columns: table => new
                {
                    id_movimiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    cantidad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    stock_anterior = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    stock_nuevo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MOVIMIENTO", x => x.id_movimiento);
                    table.CheckConstraint("CK_MOVIMIENTO_cantidad", "[cantidad] > 0");
                    table.ForeignKey(
                        name: "FK_MOVIMIENTO_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MOVIMIENTO_USUARIO_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SALIDA_HERRAMIENTA",
                columns: table => new
                {
                    id_salida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    id_responsable = table.Column<int>(type: "int", nullable: false),
                    destino = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    fecha_salida = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_devolucion = table.Column<DateOnly>(type: "date", nullable: true),
                    estado_devolucion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SALIDA_HERRAMIENTA", x => x.id_salida);
                    table.CheckConstraint("CK_SALIDA_HERRAMIENTA_fechas", "[fecha_prevista] >= [fecha_salida]");
                    table.ForeignKey(
                        name: "FK_SALIDA_HERRAMIENTA_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SALIDA_HERRAMIENTA_USUARIO_id_responsable",
                        column: x => x.id_responsable,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SOLICITUD",
                columns: table => new
                {
                    id_solicitud = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    id_solicitante = table.Column<int>(type: "int", nullable: false),
                    id_aprobador = table.Column<int>(type: "int", nullable: true),
                    cantidad = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValueSql: "'Pendiente'"),
                    fecha_solicitud = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()"),
                    fecha_resolucion = table.Column<DateTime>(type: "datetime", nullable: true),
                    motivo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    motivo_rechazo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    fecha_entrega = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SOLICITUD", x => x.id_solicitud);
                    table.CheckConstraint("CK_SOLICITUD_cantidad", "[cantidad] > 0");
                    table.ForeignKey(
                        name: "FK_SOLICITUD_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SOLICITUD_USUARIO_id_aprobador",
                        column: x => x.id_aprobador,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SOLICITUD_USUARIO_id_solicitante",
                        column: x => x.id_solicitante,
                        principalTable: "USUARIO",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTEO_DETALLE",
                columns: table => new
                {
                    id_conteo_detalle = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_conteo = table.Column<int>(type: "int", nullable: false),
                    id_articulo = table.Column<int>(type: "int", nullable: false),
                    stock_sistema = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    cantidad_contada = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    diferencia = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false, computedColumnSql: "[cantidad_contada] - [stock_sistema]", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTEO_DETALLE", x => x.id_conteo_detalle);
                    table.ForeignKey(
                        name: "FK_CONTEO_DETALLE_ARTICULO_id_articulo",
                        column: x => x.id_articulo,
                        principalTable: "ARTICULO",
                        principalColumn: "id_articulo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CONTEO_DETALLE_CONTEO_FISICO_id_conteo",
                        column: x => x.id_conteo,
                        principalTable: "CONTEO_FISICO",
                        principalColumn: "id_conteo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_codigo",
                table: "ARTICULO",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_id_categoria",
                table: "ARTICULO",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_id_ubicacion",
                table: "ARTICULO",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_nombre",
                table: "ARTICULO",
                column: "nombre");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_ALIAS_alias",
                table: "ARTICULO_ALIAS",
                column: "alias");

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_ALIAS_id_articulo_alias",
                table: "ARTICULO_ALIAS",
                columns: new[] { "id_articulo", "alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ARTICULO_VALOR_CAMPO_id_campo",
                table: "ARTICULO_VALOR_CAMPO",
                column: "id_campo");

            migrationBuilder.CreateIndex(
                name: "IX_BITACORA_fecha_hora",
                table: "BITACORA",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "IX_BITACORA_id_usuario",
                table: "BITACORA",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_CAMPO_PERSONALIZADO_id_categoria_nombre",
                table: "CAMPO_PERSONALIZADO",
                columns: new[] { "id_categoria", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_nombre",
                table: "CATEGORIA",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTEO_DETALLE_id_articulo",
                table: "CONTEO_DETALLE",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_CONTEO_DETALLE_id_conteo_id_articulo",
                table: "CONTEO_DETALLE",
                columns: new[] { "id_conteo", "id_articulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTEO_FISICO_id_categoria",
                table: "CONTEO_FISICO",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_CONTEO_FISICO_id_responsable",
                table: "CONTEO_FISICO",
                column: "id_responsable");

            migrationBuilder.CreateIndex(
                name: "IX_CONTEO_FISICO_id_ubicacion",
                table: "CONTEO_FISICO",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "IX_HISTORIAL_ESTADO_id_articulo",
                table: "HISTORIAL_ESTADO",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_HISTORIAL_ESTADO_id_usuario",
                table: "HISTORIAL_ESTADO",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMIENTO_fecha_hora",
                table: "MOVIMIENTO",
                column: "fecha_hora");

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMIENTO_id_articulo_fecha_hora",
                table: "MOVIMIENTO",
                columns: new[] { "id_articulo", "fecha_hora" });

            migrationBuilder.CreateIndex(
                name: "IX_MOVIMIENTO_id_usuario",
                table: "MOVIMIENTO",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_PERMISO_codigo",
                table: "PERMISO",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RESPALDO_id_usuario",
                table: "RESPALDO",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_ROL_nombre",
                table: "ROL",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROL_PERMISO_id_permiso",
                table: "ROL_PERMISO",
                column: "id_permiso");

            migrationBuilder.CreateIndex(
                name: "IX_SALIDA_HERRAMIENTA_fecha_devolucion",
                table: "SALIDA_HERRAMIENTA",
                column: "fecha_devolucion");

            migrationBuilder.CreateIndex(
                name: "IX_SALIDA_HERRAMIENTA_id_articulo",
                table: "SALIDA_HERRAMIENTA",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_SALIDA_HERRAMIENTA_id_responsable",
                table: "SALIDA_HERRAMIENTA",
                column: "id_responsable");

            migrationBuilder.CreateIndex(
                name: "IX_SOLICITUD_estado",
                table: "SOLICITUD",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_SOLICITUD_id_aprobador",
                table: "SOLICITUD",
                column: "id_aprobador");

            migrationBuilder.CreateIndex(
                name: "IX_SOLICITUD_id_articulo",
                table: "SOLICITUD",
                column: "id_articulo");

            migrationBuilder.CreateIndex(
                name: "IX_SOLICITUD_id_solicitante",
                table: "SOLICITUD",
                column: "id_solicitante");

            migrationBuilder.CreateIndex(
                name: "IX_UBICACION_nombre",
                table: "UBICACION",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_id_rol",
                table: "USUARIO",
                column: "id_rol");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_usuario",
                table: "USUARIO",
                column: "usuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ARTICULO_ALIAS");

            migrationBuilder.DropTable(
                name: "ARTICULO_VALOR_CAMPO");

            migrationBuilder.DropTable(
                name: "BITACORA");

            migrationBuilder.DropTable(
                name: "CONFIGURACION");

            migrationBuilder.DropTable(
                name: "CONTEO_DETALLE");

            migrationBuilder.DropTable(
                name: "HISTORIAL_ESTADO");

            migrationBuilder.DropTable(
                name: "MOVIMIENTO");

            migrationBuilder.DropTable(
                name: "RESPALDO");

            migrationBuilder.DropTable(
                name: "ROL_PERMISO");

            migrationBuilder.DropTable(
                name: "SALIDA_HERRAMIENTA");

            migrationBuilder.DropTable(
                name: "SOLICITUD");

            migrationBuilder.DropTable(
                name: "CAMPO_PERSONALIZADO");

            migrationBuilder.DropTable(
                name: "CONTEO_FISICO");

            migrationBuilder.DropTable(
                name: "PERMISO");

            migrationBuilder.DropTable(
                name: "ARTICULO");

            migrationBuilder.DropTable(
                name: "USUARIO");

            migrationBuilder.DropTable(
                name: "CATEGORIA");

            migrationBuilder.DropTable(
                name: "UBICACION");

            migrationBuilder.DropTable(
                name: "ROL");
        }
    }
}
