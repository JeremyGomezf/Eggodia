using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Eggodia.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cartas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Era = table.Column<int>(type: "int", nullable: false),
                    VidaMaxima = table.Column<int>(type: "int", nullable: false),
                    EscudoMaximo = table.Column<int>(type: "int", nullable: false),
                    PuntosAtaque = table.Column<int>(type: "int", nullable: false),
                    RutaImagen = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RutaEscena = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Habilidad = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartas", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Password = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Victorias = table.Column<int>(type: "int", nullable: false),
                    Derrotas = table.Column<int>(type: "int", nullable: false),
                    Empates = table.Column<int>(type: "int", nullable: false),
                    DañoTotal = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "cartas",
                columns: new[] { "Id", "Era", "EscudoMaximo", "Habilidad", "Nombre", "PuntosAtaque", "RutaEscena", "RutaImagen", "Tipo", "VidaMaxima" },
                values: new object[,]
                {
                    { 1, 3, 250, "Fuego eterno", "Dragón", 280, "res://cartas prime/Dragon_prime.tscn", "res://imagenes/CartasPng/DragonCart.png", "Místico", 350 },
                    { 2, 3, 500, "Rocas escudo: +200 ESC a 2 aliados", "Golem", 350, "res://cartas prime/Golem_prime.tscn", "res://imagenes/CartasPng/GolemCart.png", "Místico", 450 },
                    { 3, 3, 220, "Magia arcana", "Maguín", 250, "res://cartas prime/Maguin_prime.tscn", "res://imagenes/CartasPng/MaguinCart.png", "Místico", 200 },
                    { 4, 2, 300, "Guardia real", "Soldado Real", 150, "res://cartas prime/SoldadoReal_prime.tscn", "res://imagenes/CartasPng/SoldRealCart.png", "Medieval", 250 },
                    { 5, 2, 450, "Forma gigante: x2 ATK y ESC por 2 turnos", "Torre", 350, "res://cartas prime/Torre_prime.tscn", "res://imagenes/CartasPng/TorreCart.png", "Medieval", 500 },
                    { 6, 2, 150, "Sacrificio", "Peón", 100, "res://cartas prime/Peon_prime.tscn", "res://imagenes/CartasPng/PeonCart.png", "Medieval", 150 },
                    { 7, 2, 550, "La mejor sopa", "Encebollado", 450, "res://cartas prime/Encebollado_prime.tscn", "res://imagenes/CartasPng/EncebolladoCart.png", "Medieval", 500 },
                    { 8, 2, 250, "Salto en L: x2 daño a 2 carriles", "Caballo", 200, "res://cartas prime/Caballo_prime.tscn", "res://imagenes/CartasPng/CaballoCart.png", "Medieval", 230 },
                    { 9, 2, 300, "Dominio total", "Dama", 350, "res://cartas prime/Dama_prime.tscn", "res://imagenes/CartasPng/DamaCart.png", "Medieval", 350 },
                    { 10, 1, 350, "Furia primitiva", "T-Rex", 400, "res://cartas prime/TRex_prime.tscn", "res://imagenes/CartasPng/TReXCart.png", "Primordial", 400 },
                    { 11, 1, 200, "Ataque en profundidad", "Tiburón", 230, "res://cartas prime/Tiburon_prime.tscn", "res://imagenes/CartasPng/TiburonCart.png", "Primordial", 300 },
                    { 12, 1, 380, "Tinta bloqueadora: paraliza 2 tropas cercanas", "Calamar Gigante", 370, "res://cartas prime/CalamarG_prime.tscn", "res://imagenes/CartasPng/CalamarGCart.png", "Primordial", 400 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cartas");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
