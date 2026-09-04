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

            // ── SEED CARTAS (17 tropas del juego, con Rol y Serie) ───────────
            modelBuilder.Entity<Carta>().HasData(
                // AJEDREZ
                new Carta { Id=1,  Nombre="Peón",            Rol="Tactico", Serie="Ajedrez",  Era=2, Tipo="Ajedrez",  VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Peon_prime.tscn",              Habilidad="Sacrificio" },
                new Carta { Id=2,  Nombre="Torre",           Rol="Coloso",  Serie="Ajedrez",  Era=2, Tipo="Ajedrez",  VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Torre_prime.tscn",             Habilidad="Enroque táctico / forma gigante" },
                new Carta { Id=3,  Nombre="Arfil",           Rol="Tactico", Serie="Ajedrez",  Era=2, Tipo="Ajedrez",  VidaMaxima=280, EscudoMaximo=270, PuntosAtaque=240, RutaImagen="res://imagenes/CartasPng/ArfilCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Arfil_prime.tscn",             Habilidad="Salto diagonal" },
                new Carta { Id=4,  Nombre="Caballo",         Rol="Tactico", Serie="Ajedrez",  Era=2, Tipo="Ajedrez",  VidaMaxima=230, EscudoMaximo=250, PuntosAtaque=200, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/AJEDREZ/Caballo_prime.tscn",           Habilidad="Salto en L: x2 daño a 2 carriles" },
                new Carta { Id=5,  Nombre="Dama",            Rol="Asesino", Serie="Ajedrez",  Era=2, Tipo="Ajedrez",  VidaMaxima=350, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Dama_prime.tscn",              Habilidad="Vuelo con inspiración aliada" },
                // TOON
                new Carta { Id=6,  Nombre="Soldado Cartoon", Rol="Asesino", Serie="Toon",     Era=3, Tipo="Toon",     VidaMaxima=220, EscudoMaximo=350, PuntosAtaque=50,  RutaImagen="res://imagenes/CartasPng/SoldCartoonCart.png",RutaEscena="res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",     Habilidad="Soldado explosivo" },
                new Carta { Id=7,  Nombre="Tanque",          Rol="Coloso",  Serie="Toon",     Era=3, Tipo="Toon",     VidaMaxima=820, EscudoMaximo=0,   PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TanqueCart.png",    RutaEscena="res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",      Habilidad="Doble disparo de misil" },
                new Carta { Id=8,  Nombre="Granadero",       Rol="Asesino", Serie="Toon",     Era=3, Tipo="Toon",     VidaMaxima=50,  EscudoMaximo=600, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/GranaderoCart.png", RutaEscena="res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",   Habilidad="Escudo masivo" },
                new Carta { Id=9,  Nombre="Ka-Bar",          Rol="Asesino", Serie="Toon",     Era=3, Tipo="Toon",     VidaMaxima=250, EscudoMaximo=360, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/FantasmaCart.png",  RutaEscena="res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",      Habilidad="Resucita como fantasma" },
                new Carta { Id=10, Nombre="Campero",         Rol="Tactico", Serie="Toon",     Era=3, Tipo="Toon",     VidaMaxima=200, EscudoMaximo=300, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CamperoCart.png",   RutaEscena="res://cartas prime/TOONS/Campero_cartoon_prime.tscn",     Habilidad="Postura defensiva" },
                // MEDIEVAL
                new Carta { Id=11, Nombre="Soldado Real",    Rol="Tactico", Serie="Medieval", Era=2, Tipo="Medieval", VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",      Habilidad="Guardia real" },
                new Carta { Id=12, Nombre="Maguín",          Rol="Tactico", Serie="Medieval", Era=2, Tipo="Medieval", VidaMaxima=200, EscudoMaximo=220, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Maguin_prime.tscn",           Habilidad="Transmutación" },
                new Carta { Id=13, Nombre="Dragón",          Rol="Asesino", Serie="Medieval", Era=2, Tipo="Medieval", VidaMaxima=350, EscudoMaximo=250, PuntosAtaque=280, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Dragon_prime.tscn",           Habilidad="Fuego eterno" },
                new Carta { Id=14, Nombre="Golem",           Rol="Coloso",  Serie="Medieval", Era=2, Tipo="Medieval", VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/MEDIEVAL/Golem_prime.tscn",            Habilidad="Rocas escudo: +200 ESC a aliados" },
                // PACIFICO
                new Carta { Id=15, Nombre="Tiburón",         Rol="Asesino", Serie="Pacifico", Era=1, Tipo="Pacifico", VidaMaxima=300, EscudoMaximo=200, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/PACIFICO/Tiburon_prime.tscn",          Habilidad="Ataque en profundidad" },
                new Carta { Id=16, Nombre="Calamar Gigante", Rol="Coloso",  Serie="Pacifico", Era=1, Tipo="Pacifico", VidaMaxima=400, EscudoMaximo=380, PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/PACIFICO/CalamarG_prime.tscn",         Habilidad="Tinta bloqueadora" },
                // PAPELEO
                new Carta { Id=17, Nombre="Paper-Rex",       Rol="Coloso",  Serie="Papeleo",  Era=1, Tipo="Papeleo",  VidaMaxima=850, EscudoMaximo=0,   PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/PaperReXCart.png",  RutaEscena="res://cartas prime/PAPEL/TRex_prime.tscn",                Habilidad="Rugido primordial: -30% ataque enemigo" }
            );
        }
    }
}
