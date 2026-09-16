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
        public DbSet<UserItem>  UserItems  { get; set; }

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
            modelBuilder.Entity<UserItem>(e =>
            {
                e.ToTable("user_items");
                e.HasIndex(ui => new { ui.UserId, ui.Tipo, ui.ItemId }).IsUnique();
            });

            // ── SEED CARTAS (Rebalanceo de 17 tropas) ────
            modelBuilder.Entity<Carta>().HasData(
                // AJEDREZ
                new Carta { Id=1,  Nombre="Peón",            Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Peon_prime.tscn",              Habilidad="Se convierte en las demás piezas + otorga +150 de daño y escudo" },
                new Carta { Id=2,  Nombre="Torre",           Tipo="Coloso",  Serie="Ajedrez",  VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=330, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Torre_prime.tscn",             Habilidad="Intercambia de carril" },
                new Carta { Id=3,  Nombre="Arfil",           Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=320, EscudoMaximo=240, PuntosAtaque=235, RutaImagen="res://imagenes/CartasPng/ArfilCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Arfil_prime.tscn",             Habilidad="Solo ataca en diagonal, +100 sobre su ataque normal" },
                new Carta { Id=4,  Nombre="Caballo",         Tipo="Tactico", Serie="Ajedrez",  VidaMaxima=300, EscudoMaximo=250, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/AJEDREZ/Caballo_prime.tscn",           Habilidad="Ataque en forma de L con daño igual a su ataque normal" },
                new Carta { Id=5,  Nombre="Dama",            Tipo="Asesino", Serie="Ajedrez",  VidaMaxima=380, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Dama_prime.tscn",              Habilidad="Ataca a cualquier posición del tablero (+150 fijo sobre su ataque)" },
                // TOON
                new Carta { Id=6,  Nombre="Soldado Cartoon", Tipo="Asesino", Serie="Toon",     VidaMaxima=230, EscudoMaximo=350, PuntosAtaque=50,  RutaImagen="res://imagenes/CartasPng/SoldCartoonCart.png",RutaEscena="res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",     Habilidad="Se esconde y ataca sin parar (varios golpes seguidos)" },
                new Carta { Id=7,  Nombre="Tanque",          Tipo="Coloso",  Serie="Toon",     VidaMaxima=820, EscudoMaximo=0,   PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TanqueCart.png",    RutaEscena="res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",      Habilidad="Dispara dos veces" },
                new Carta { Id=8,  Nombre="Granadero",       Tipo="Asesino", Serie="Toon",     VidaMaxima=50,  EscudoMaximo=600, PuntosAtaque=220, RutaImagen="res://imagenes/CartasPng/GranaderoCart.png", RutaEscena="res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",   Habilidad="Lanza un misil hacia arriba con el ataque del Tanque" },
                new Carta { Id=9,  Nombre="Ka-Bar",          Tipo="Asesino", Serie="Toon",     VidaMaxima=250, EscudoMaximo=330, PuntosAtaque=115, RutaImagen="res://imagenes/CartasPng/FantasmaCart.png",  RutaEscena="res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",      Habilidad="Al morir, aparece su fantasma en su mismo carril" },
                new Carta { Id=10, Nombre="Campero",         Tipo="Tactico", Serie="Toon",     VidaMaxima=200, EscudoMaximo=300, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CamperoCart.png",   RutaEscena="res://cartas prime/TOONS/Campero_cartoon_prime.tscn",     Habilidad="Golpea a dos rivales con su ataque normal menos 35" },
                // MEDIEVAL
                new Carta { Id=11, Nombre="Soldado Real",    Tipo="Asesino", Serie="Medieval", VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=170, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",      Habilidad="Hace parry (se cubre; si lo atacan, contraataca)" },
                new Carta { Id=12, Nombre="Maguín",          Tipo="Tactico", Serie="Medieval", VidaMaxima=200, EscudoMaximo=280, PuntosAtaque=245, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Maguin_prime.tscn",           Habilidad="Convierte en animal al rival más fuerte" },
                new Carta { Id=13, Nombre="Dragón",          Tipo="Asesino", Serie="Medieval", VidaMaxima=340, EscudoMaximo=380, PuntosAtaque=270, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Dragon_prime.tscn",           Habilidad="Dispara fuego que aturde por unos segundos" },
                new Carta { Id=14, Nombre="Golem",           Tipo="Coloso",  Serie="Medieval", VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=330, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/MEDIEVAL/Golem_prime.tscn",            Habilidad="Invoca rocas para sus aliados" },
                // PACIFICO
                new Carta { Id=15, Nombre="Tiburón",         Tipo="Asesino", Serie="Pacifico", VidaMaxima=310, EscudoMaximo=250, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/PACIFICO/Tiburon_prime.tscn",          Habilidad="Su ataque sube +35 cada turno" },
                new Carta { Id=16, Nombre="Calamar Gigante", Tipo="Coloso",  Serie="Pacifico", VidaMaxima=420, EscudoMaximo=380, PuntosAtaque=300, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/PACIFICO/CalamarG_prime.tscn",         Habilidad="Atrapa enemigos y se cubre" },
                // PAPELEO
                new Carta { Id=17, Nombre="Paper-Rex",       Tipo="Coloso",  Serie="Papeleo",  VidaMaxima=840, EscudoMaximo=0,   PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/PaperReXCart.png",  RutaEscena="res://cartas prime/PAPEL/TRex_prime.tscn",                Habilidad="Ignora cobertura/defensa, destroza la vida igual" }
            );

            // ── SEED PROMO CODES (20 códigos: 10 Huevo Dorado + 10 Huevo Ecotec) ────
            modelBuilder.Entity<PromoCode>().HasData(GenerarCodigosPromo());
        }

        // 20 códigos fijos: 10 para Huevo Dorado, 10 para Huevo Ecotec. Reemplaza el lote anterior
        // de ~150 códigos genéricos — ver también CodigosSeed.TODOS (Program.cs), que aplica esta
        // misma lista de forma idempotente contra una base de datos ya existente, porque HasData()
        // aquí solo siembra en una base creada desde cero (ver EnsureCreated en Program.cs).
        public static List<PromoCode> GenerarCodigosPromo()
        {
            var codigos = new List<PromoCode>();
            int id = 1;

            for (int i = 1; i <= 10; i++)
            {
                codigos.Add(new PromoCode
                {
                    Id = id++,
                    Codigo = $"dorado{100 + i}huevo",
                    TipoRecompensa = "skin",
                    ValorRecompensa = "res://imagenes/RendersTropa/Huevo render/HuevoDorado_Render.png",
                    NombreRecompensa = "Huevo Dorado",
                    MaxUsos = 1
                });
            }

            for (int i = 1; i <= 10; i++)
            {
                codigos.Add(new PromoCode
                {
                    Id = id++,
                    Codigo = $"ecotec{100 + i}huevo",
                    TipoRecompensa = "skin",
                    ValorRecompensa = "res://imagenes/RendersTropa/Huevo render/HuevoEcotec_Render.png",
                    NombreRecompensa = "Huevo Ecotec",
                    MaxUsos = 1
                });
            }

            return codigos;
        }
    }
}
