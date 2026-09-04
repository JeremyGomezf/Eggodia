using Microsoft.EntityFrameworkCore;
using Eggodia.API.model;

namespace Eggodia.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Carta>   Cartas   { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carta>(e => e.ToTable("cartas"));
            modelBuilder.Entity<Usuario>(e =>
            {
                e.ToTable("usuarios");
                e.HasIndex(u => u.Email).IsUnique();
            });

            // ── SEED CARTAS (17 tropas del juego). Tipo = rol de combate. ────
            modelBuilder.Entity<Carta>().HasData(
                // AJEDREZ
                new Carta { Id=1,  Nombre="Peón",            Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Peon_prime.tscn",              Habilidad="Sacrificio" },
                new Carta { Id=2,  Nombre="Torre",           Tipo="Coloso",  Serie="Ajedrez",  VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Torre_prime.tscn",             Habilidad="Enroque táctico / forma gigante" },
                new Carta { Id=3,  Nombre="Arfil",           Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=280, EscudoMaximo=270, PuntosAtaque=240, RutaImagen="res://imagenes/CartasPng/ArfilCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Arfil_prime.tscn",             Habilidad="Salto diagonal" },
                new Carta { Id=4,  Nombre="Caballo",         Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=230, EscudoMaximo=250, PuntosAtaque=200, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/AJEDREZ/Caballo_prime.tscn",           Habilidad="Salto en L: x2 daño a 2 carriles" },
                new Carta { Id=5,  Nombre="Dama",            Tipo="Asesino", Serie="Ajedrez",  VidaMaxima=350, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Dama_prime.tscn",              Habilidad="Vuelo con inspiración aliada" },
                // TOON
                new Carta { Id=6,  Nombre="Soldado Cartoon", Tipo="Asesino", Serie="Toon",     VidaMaxima=220, EscudoMaximo=350, PuntosAtaque=50,  RutaImagen="res://imagenes/CartasPng/SoldCartoonCart.png",RutaEscena="res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",     Habilidad="Soldado explosivo" },
                new Carta { Id=7,  Nombre="Tanque",          Tipo="Coloso",  Serie="Toon",     VidaMaxima=820, EscudoMaximo=0,   PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TanqueCart.png",    RutaEscena="res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",      Habilidad="Doble disparo de misil" },
                new Carta { Id=8,  Nombre="Granadero",       Tipo="Asesino", Serie="Toon",     VidaMaxima=50,  EscudoMaximo=600, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/GranaderoCart.png", RutaEscena="res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",   Habilidad="Escudo masivo" },
                new Carta { Id=9,  Nombre="Ka-Bar",          Tipo="Asesino", Serie="Toon",     VidaMaxima=250, EscudoMaximo=360, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/FantasmaCart.png",  RutaEscena="res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",      Habilidad="Resucita como fantasma" },
                new Carta { Id=10, Nombre="Campero",         Tipo="Tactico", Serie="Toon",     VidaMaxima=200, EscudoMaximo=300, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CamperoCart.png",   RutaEscena="res://cartas prime/TOONS/Campero_cartoon_prime.tscn",     Habilidad="Postura defensiva" },
                // MEDIEVAL
                new Carta { Id=11, Nombre="Soldado Real",    Tipo="Tactico", Serie="Medieval", VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",      Habilidad="Guardia real" },
                new Carta { Id=12, Nombre="Maguín",          Tipo="Tactico", Serie="Medieval", VidaMaxima=200, EscudoMaximo=220, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Maguin_prime.tscn",           Habilidad="Transmutación" },
                new Carta { Id=13, Nombre="Dragón",          Tipo="Asesino", Serie="Medieval", VidaMaxima=350, EscudoMaximo=250, PuntosAtaque=280, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Dragon_prime.tscn",           Habilidad="Fuego eterno" },
                new Carta { Id=14, Nombre="Golem",           Tipo="Coloso",  Serie="Medieval", VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/MEDIEVAL/Golem_prime.tscn",            Habilidad="Rocas escudo: +200 ESC a aliados" },
                // PACIFICO
                new Carta { Id=15, Nombre="Tiburón",         Tipo="Asesino", Serie="Pacifico", VidaMaxima=300, EscudoMaximo=200, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/PACIFICO/Tiburon_prime.tscn",          Habilidad="Ataque en profundidad" },
                new Carta { Id=16, Nombre="Calamar Gigante", Tipo="Coloso",  Serie="Pacifico", VidaMaxima=400, EscudoMaximo=380, PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/PACIFICO/CalamarG_prime.tscn",         Habilidad="Tinta bloqueadora" },
                // PAPELEO
                new Carta { Id=17, Nombre="Paper-Rex",       Tipo="Coloso",  Serie="Papeleo",  VidaMaxima=850, EscudoMaximo=0,   PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/PaperReXCart.png",  RutaEscena="res://cartas prime/PAPEL/TRex_prime.tscn",                Habilidad="Rugido primordial: -30% ataque enemigo" }
            );
        }
    }
}
