using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KromaNexus.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarStatsCartas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Borrar tabla vieja si existe
            migrationBuilder.DropTable(name: "Cartas");

            // Crear tabla nueva con todos los campos del juego
            migrationBuilder.CreateTable(
                name: "cartas",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nombre      = table.Column<string>(nullable: false),
                    Era         = table.Column<int>(nullable: false, defaultValue: 1),
                    Tipo        = table.Column<string>(nullable: false, defaultValue: "Normal"),
                    VidaMaxima  = table.Column<int>(nullable: false),
                    EscudoMaximo = table.Column<int>(nullable: false),
                    PuntosAtaque = table.Column<int>(nullable: false),
                    RutaImagen  = table.Column<string>(nullable: false, defaultValue: ""),
                    RutaEscena  = table.Column<string>(nullable: false, defaultValue: ""),
                    Habilidad   = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartas", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cartas");
        }
    }
}
