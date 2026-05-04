using Microsoft.EntityFrameworkCore;
using KromaNexus.API.model;

namespace KromaNexus.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Carta> Cartas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carta>(entity =>
            {
                entity.ToTable("cartas");
            });

            // ── SEED: Las 12 cartas del juego ────────────────────────────────
            // Era 1 = Primordial | Era 2 = Medieval | Era 3 = Mística
            modelBuilder.Entity<Carta>().HasData(

                // ── ERA 3: MÍSTICA ────────────────────────────────────────────
                new Carta {
                    Id = 1, Nombre = "Dragón", Era = 3, Tipo = "Místico",
                    VidaMaxima = 350, EscudoMaximo = 250, PuntosAtaque = 280,
                    RutaImagen = "res://imagenes/CartasPng/DragonCart.png",
                    RutaEscena = "res://cartas prime/Dragon_prime.tscn",
                    Habilidad  = "Fuego eterno"
                },
                new Carta {
                    Id = 2, Nombre = "Golem", Era = 3, Tipo = "Místico",
                    VidaMaxima = 450, EscudoMaximo = 500, PuntosAtaque = 350,
                    RutaImagen = "res://imagenes/CartasPng/GolemCart.png",
                    RutaEscena = "res://cartas prime/Golem_prime.tscn",
                    Habilidad  = "Rocas escudo: +200 ESC a 2 aliados"
                },
                new Carta {
                    Id = 3, Nombre = "Maguín", Era = 3, Tipo = "Místico",
                    VidaMaxima = 200, EscudoMaximo = 220, PuntosAtaque = 250,
                    RutaImagen = "res://imagenes/CartasPng/MaguinCart.png",
                    RutaEscena = "res://cartas prime/Maguin_prime.tscn",
                    Habilidad  = "Magia arcana"
                },

                // ── ERA 2: MEDIEVAL ───────────────────────────────────────────
                new Carta {
                    Id = 4, Nombre = "Soldado Real", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 250, EscudoMaximo = 300, PuntosAtaque = 150,
                    RutaImagen = "res://imagenes/CartasPng/SoldRealCart.png",
                    RutaEscena = "res://cartas prime/SoldadoReal_prime.tscn",
                    Habilidad  = "Guardia real"
                },
                new Carta {
                    Id = 5, Nombre = "Torre", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 500, EscudoMaximo = 450, PuntosAtaque = 350,
                    RutaImagen = "res://imagenes/CartasPng/TorreCart.png",
                    RutaEscena = "res://cartas prime/Torre_prime.tscn",
                    Habilidad  = "Forma gigante: x2 ATK y ESC por 2 turnos"
                },
                new Carta {
                    Id = 6, Nombre = "Peón", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 150, EscudoMaximo = 150, PuntosAtaque = 100,
                    RutaImagen = "res://imagenes/CartasPng/PeonCart.png",
                    RutaEscena = "res://cartas prime/Peon_prime.tscn",
                    Habilidad  = "Sacrificio"
                },
                new Carta {
                    Id = 7, Nombre = "Encebollado", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 500, EscudoMaximo = 550, PuntosAtaque = 450,
                    RutaImagen = "res://imagenes/CartasPng/EncebolladoCart.png",
                    RutaEscena = "res://cartas prime/Encebollado_prime.tscn",
                    Habilidad  = "La mejor sopa"
                },
                new Carta {
                    Id = 8, Nombre = "Caballo", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 230, EscudoMaximo = 250, PuntosAtaque = 200,
                    RutaImagen = "res://imagenes/CartasPng/CaballoCart.png",
                    RutaEscena = "res://cartas prime/Caballo_prime.tscn",
                    Habilidad  = "Salto en L: x2 daño a 2 carriles"
                },
                new Carta {
                    Id = 9, Nombre = "Dama", Era = 2, Tipo = "Medieval",
                    VidaMaxima = 350, EscudoMaximo = 300, PuntosAtaque = 350,
                    RutaImagen = "res://imagenes/CartasPng/DamaCart.png",
                    RutaEscena = "res://cartas prime/Dama_prime.tscn",
                    Habilidad  = "Dominio total"
                },

                // ── ERA 1: PRIMORDIAL ─────────────────────────────────────────
                new Carta {
                    Id = 10, Nombre = "T-Rex", Era = 1, Tipo = "Primordial",
                    VidaMaxima = 400, EscudoMaximo = 350, PuntosAtaque = 400,
                    RutaImagen = "res://imagenes/CartasPng/TReXCart.png",
                    RutaEscena = "res://cartas prime/TRex_prime.tscn",
                    Habilidad  = "Furia primitiva"
                },
                new Carta {
                    Id = 11, Nombre = "Tiburón", Era = 1, Tipo = "Primordial",
                    VidaMaxima = 300, EscudoMaximo = 200, PuntosAtaque = 230,
                    RutaImagen = "res://imagenes/CartasPng/TiburonCart.png",
                    RutaEscena = "res://cartas prime/Tiburon_prime.tscn",
                    Habilidad  = "Ataque en profundidad"
                },
                new Carta {
                    Id = 12, Nombre = "Calamar Gigante", Era = 1, Tipo = "Primordial",
                    VidaMaxima = 400, EscudoMaximo = 380, PuntosAtaque = 370,
                    RutaImagen = "res://imagenes/CartasPng/CalamarGCart.png",
                    RutaEscena = "res://cartas prime/CalamarG_prime.tscn",
                    Habilidad  = "Tinta bloqueadora: paraliza 2 tropas cercanas"
                }
            );
        }
    }
}