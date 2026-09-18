# Eggodia — Códigos de canje de huevos

100 códigos en total: **50 de Huevo Dorado** y **50 de Huevo Ecotec**.
Cada código es de **un solo uso** (una vez canjeado, no sirve para nadie más).

Se canjean desde la pantalla de códigos del juego, con la sesión iniciada.

> Los códigos **101 al 110** de cada huevo son los que ya existían desde antes: si ya
> repartiste alguno, sigue siendo válido. Los nuevos son del **111 al 150**.

---

## 🥚 Huevo Dorado (50)

| # | Código | # | Código |
|---|---|---|---|
| 1 | `dorado101huevo` | 26 | `dorado126huevo` |
| 2 | `dorado102huevo` | 27 | `dorado127huevo` |
| 3 | `dorado103huevo` | 28 | `dorado128huevo` |
| 4 | `dorado104huevo` | 29 | `dorado129huevo` |
| 5 | `dorado105huevo` | 30 | `dorado130huevo` |
| 6 | `dorado106huevo` | 31 | `dorado131huevo` |
| 7 | `dorado107huevo` | 32 | `dorado132huevo` |
| 8 | `dorado108huevo` | 33 | `dorado133huevo` |
| 9 | `dorado109huevo` | 34 | `dorado134huevo` |
| 10 | `dorado110huevo` | 35 | `dorado135huevo` |
| 11 | `dorado111huevo` | 36 | `dorado136huevo` |
| 12 | `dorado112huevo` | 37 | `dorado137huevo` |
| 13 | `dorado113huevo` | 38 | `dorado138huevo` |
| 14 | `dorado114huevo` | 39 | `dorado139huevo` |
| 15 | `dorado115huevo` | 40 | `dorado140huevo` |
| 16 | `dorado116huevo` | 41 | `dorado141huevo` |
| 17 | `dorado117huevo` | 42 | `dorado142huevo` |
| 18 | `dorado118huevo` | 43 | `dorado143huevo` |
| 19 | `dorado119huevo` | 44 | `dorado144huevo` |
| 20 | `dorado120huevo` | 45 | `dorado145huevo` |
| 21 | `dorado121huevo` | 46 | `dorado146huevo` |
| 22 | `dorado122huevo` | 47 | `dorado147huevo` |
| 23 | `dorado123huevo` | 48 | `dorado148huevo` |
| 24 | `dorado124huevo` | 49 | `dorado149huevo` |
| 25 | `dorado125huevo` | 50 | `dorado150huevo` |

---

## 🌱 Huevo Ecotec (50)

| # | Código | # | Código |
|---|---|---|---|
| 1 | `ecotec101huevo` | 26 | `ecotec126huevo` |
| 2 | `ecotec102huevo` | 27 | `ecotec127huevo` |
| 3 | `ecotec103huevo` | 28 | `ecotec128huevo` |
| 4 | `ecotec104huevo` | 29 | `ecotec129huevo` |
| 5 | `ecotec105huevo` | 30 | `ecotec130huevo` |
| 6 | `ecotec106huevo` | 31 | `ecotec131huevo` |
| 7 | `ecotec107huevo` | 32 | `ecotec132huevo` |
| 8 | `ecotec108huevo` | 33 | `ecotec133huevo` |
| 9 | `ecotec109huevo` | 34 | `ecotec134huevo` |
| 10 | `ecotec110huevo` | 35 | `ecotec135huevo` |
| 11 | `ecotec111huevo` | 36 | `ecotec136huevo` |
| 12 | `ecotec112huevo` | 37 | `ecotec137huevo` |
| 13 | `ecotec113huevo` | 38 | `ecotec138huevo` |
| 14 | `ecotec114huevo` | 39 | `ecotec139huevo` |
| 15 | `ecotec115huevo` | 40 | `ecotec140huevo` |
| 16 | `ecotec116huevo` | 41 | `ecotec141huevo` |
| 17 | `ecotec117huevo` | 42 | `ecotec142huevo` |
| 18 | `ecotec118huevo` | 43 | `ecotec143huevo` |
| 19 | `ecotec119huevo` | 44 | `ecotec144huevo` |
| 20 | `ecotec120huevo` | 45 | `ecotec145huevo` |
| 21 | `ecotec121huevo` | 46 | `ecotec146huevo` |
| 22 | `ecotec122huevo` | 47 | `ecotec147huevo` |
| 23 | `ecotec123huevo` | 48 | `ecotec148huevo` |
| 24 | `ecotec124huevo` | 49 | `ecotec149huevo` |
| 25 | `ecotec125huevo` | 50 | `ecotec150huevo` |

---

## Importante

Los códigos nuevos (111–150) **solo funcionan después de subir el backend** con este cambio.
Están definidos en `Backend/Eggodia/AppDbContext.cs` → `GenerarCodigosPromo()`, y se siembran en
la base de datos de forma idempotente al arrancar el servidor (ver `Program.cs`), así que los
códigos ya existentes no se duplican ni se pierden.
