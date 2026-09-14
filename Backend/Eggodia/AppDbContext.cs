using Microsoft.EntityFrameworkCore;
using Eggodia.API.model;

namespace Eggodia.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Carta>     Cartas     { get; set; }
        public DbSet<Usuario>   Usuarios   { get; set; }
        public DbSet<UserCard>  UserCards  { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<UserSkin>  UserSkins  { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carta>(e => e.ToTable("cartas"));
            modelBuilder.Entity<Usuario>(e =>
            {
                e.ToTable("usuarios");
                e.HasIndex(u => u.Email).IsUnique();
            });
            modelBuilder.Entity<UserCard>(e =>
            {
                e.ToTable("user_cards");
                e.HasIndex(uc => new { uc.UserId, uc.CardId });
            });
            modelBuilder.Entity<PromoCode>(e =>
            {
                e.ToTable("promo_codes");
                e.HasIndex(p => p.Codigo).IsUnique();
            });
            modelBuilder.Entity<UserSkin>(e =>
            {
                e.ToTable("user_skins");
                e.HasIndex(us => new { us.UserId, us.SkinRuta });
            });

            // ── SEED CARTAS (Rebalanceo de 17 tropas) ────
            modelBuilder.Entity<Carta>().HasData(
                // AJEDREZ
                new Carta { Id=1,  Nombre="Peón",            Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Peon_prime.tscn",              Habilidad="Se convierte en las demás piezas + otorga +150 de daño y escudo" },
                new Carta { Id=2,  Nombre="Torre",           Tipo="Coloso",  Serie="Ajedrez",  VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Torre_prime.tscn",             Habilidad="Intercambia de carril" },
                new Carta { Id=3,  Nombre="Arfil",           Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=280, EscudoMaximo=270, PuntosAtaque=240, RutaImagen="res://imagenes/CartasPng/ArfilCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Arfil_prime.tscn",             Habilidad="Solo ataca en diagonal, +50 sobre su ataque normal" },
                new Carta { Id=4,  Nombre="Caballo",         Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=230, EscudoMaximo=250, PuntosAtaque=200, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/AJEDREZ/Caballo_prime.tscn",           Habilidad="Ataque en forma de L con daño igual a su ataque normal" },
                new Carta { Id=5,  Nombre="Dama",            Tipo="Asesino", Serie="Ajedrez",  VidaMaxima=350, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Dama_prime.tscn",              Habilidad="Ataca a cualquier posición del tablero" },
                // TOON
                new Carta { Id=6,  Nombre="Soldado Cartoon", Tipo="Asesino", Serie="Toon",     VidaMaxima=220, EscudoMaximo=350, PuntosAtaque=50,  RutaImagen="res://imagenes/CartasPng/SoldCartoonCart.png",RutaEscena="res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",     Habilidad="Se esconde y ataca sin parar (varios golpes seguidos)" },
                new Carta { Id=7,  Nombre="Tanque",          Tipo="Coloso",  Serie="Toon",     VidaMaxima=820, EscudoMaximo=0,   PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TanqueCart.png",    RutaEscena="res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",      Habilidad="Dispara dos veces" },
                new Carta { Id=8,  Nombre="Granadero",       Tipo="Asesino", Serie="Toon",     VidaMaxima=50,  EscudoMaximo=600, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/GranaderoCart.png", RutaEscena="res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",   Habilidad="Lanza un misil hacia arriba con el ataque del Tanque" },
                new Carta { Id=9,  Nombre="Ka-Bar",          Tipo="Asesino", Serie="Toon",     VidaMaxima=250, EscudoMaximo=360, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/FantasmaCart.png",  RutaEscena="res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",      Habilidad="Al morir, aparece su fantasma en su mismo carril" },
                new Carta { Id=10, Nombre="Campero",         Tipo="Tactico", Serie="Toon",     VidaMaxima=200, EscudoMaximo=300, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CamperoCart.png",   RutaEscena="res://cartas prime/TOONS/Campero_cartoon_prime.tscn",     Habilidad="Golpea a dos rivales con su ataque normal menos 35" },
                // MEDIEVAL
                new Carta { Id=11, Nombre="Soldado Real",    Tipo="Tactico", Serie="Medieval", VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",      Habilidad="Hace parry (se cubre; si lo atacan, contraataca)" },
                new Carta { Id=12, Nombre="Maguín",          Tipo="Tactico", Serie="Medieval", VidaMaxima=200, EscudoMaximo=220, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Maguin_prime.tscn",           Habilidad="Convierte en animal al rival más fuerte" },
                new Carta { Id=13, Nombre="Dragón",          Tipo="Asesino", Serie="Medieval", VidaMaxima=350, EscudoMaximo=250, PuntosAtaque=280, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Dragon_prime.tscn",           Habilidad="Dispara fuego que aturde por unos segundos" },
                new Carta { Id=14, Nombre="Golem",           Tipo="Coloso",  Serie="Medieval", VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/MEDIEVAL/Golem_prime.tscn",            Habilidad="Invoca rocas para sus aliados" },
                // PACIFICO
                new Carta { Id=15, Nombre="Tiburón",         Tipo="Asesino", Serie="Pacifico", VidaMaxima=300, EscudoMaximo=200, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/PACIFICO/Tiburon_prime.tscn",          Habilidad="Su ataque sube +35 cada turno" },
                new Carta { Id=16, Nombre="Calamar Gigante", Tipo="Coloso",  Serie="Pacifico", VidaMaxima=400, EscudoMaximo=380, PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/PACIFICO/CalamarG_prime.tscn",         Habilidad="Atrapa enemigos y se cubre" },
                // PAPELEO
                new Carta { Id=17, Nombre="Paper-Rex",       Tipo="Coloso",  Serie="Papeleo",  VidaMaxima=850, EscudoMaximo=0,   PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/PaperReXCart.png",  RutaEscena="res://cartas prime/PAPEL/TRex_prime.tscn",                Habilidad="Ignora cobertura/defensa, destroza la vida igual" }
            );

            // ── SEED PROMO CODES (~150 códigos tipo 133huevo375) ────
            modelBuilder.Entity<PromoCode>().HasData(GenerarCodigosPromo());
        }

        private static List<PromoCode> GenerarCodigosPromo()
        {
            var codigos = new List<PromoCode>();

            // Código exclusivo para Huevo Ecotec (y variantes VIP)
            codigos.Add(new PromoCode
            {
                Id = 1,
                Codigo = "133huevo375",
                TipoRecompensa = "skin",
                ValorRecompensa = "res://imagenes/PersonajesPng/HuevoEcotec.png",
                NombreRecompensa = "Huevo Ecotec",
                MaxUsos = 1
            });
            codigos.Add(new PromoCode
            {
                Id = 2,
                Codigo = "ecotechuevo2026",
                TipoRecompensa = "skin",
                ValorRecompensa = "res://imagenes/PersonajesPng/HuevoEcotec.png",
                NombreRecompensa = "Huevo Ecotec",
                MaxUsos = 1
            });
            codigos.Add(new PromoCode
            {
                Id = 3,
                Codigo = "eggodiaecotec",
                TipoRecompensa = "skin",
                ValorRecompensa = "res://imagenes/PersonajesPng/HuevoEcotec.png",
                NombreRecompensa = "Huevo Ecotec",
                MaxUsos = 1
            });

            var skinsPool = new[]
            {
                ("res://imagenes/PersonajesPng/CapitanHuevo.png", "Capitán Huevo"),
                ("res://imagenes/PersonajesPng/DinoHuevo.png", "Dino Huevo"),
                ("res://imagenes/PersonajesPng/MajestadHuevo.png", "Majestad Huevo"),
                ("res://imagenes/PersonajesPng/PaperDinoHuevoIcon.tres", "Paper Dino Huevo"),
                ("res://imagenes/PersonajesPng/CoronelHuevo.png", "Coronel Huevo"),
                ("res://imagenes/PersonajesPng/HuevoRosa.png", "Huevo Rosa"),
                ("res://imagenes/PersonajesPng/MajestadHuevo2.png", "Majestad Huevo II")
            };

            for (int i = 4; i <= 150; i++)
            {
                string codigo = $"{100 + i}huevo{300 + (i * 7)}";

                if (i % 2 == 0)
                {
                    var skin = skinsPool[i % skinsPool.Length];
                    codigos.Add(new PromoCode
                    {
                        Id = i,
                        Codigo = codigo,
                        TipoRecompensa = "skin",
                        ValorRecompensa = skin.Item1,
                        NombreRecompensa = skin.Item2,
                        MaxUsos = 1
                    });
                }
                else
                {
                    int monedas = 200 + ((i % 5) * 100);
                    codigos.Add(new PromoCode
                    {
                        Id = i,
                        Codigo = codigo,
                        TipoRecompensa = "monedas",
                        ValorRecompensa = monedas.ToString(),
                        NombreRecompensa = $"{monedas} Monedas de Oro",
                        MaxUsos = 1
                    });
                }
            }

            return codigos;
        }
    }
}
