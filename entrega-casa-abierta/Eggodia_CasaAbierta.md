# Eggodia — Proyecto Final Casa Abierta

**Integrantes del equipo:**
- Jeremy Gomez
- Gonzalo Siguenza
- Carlos Jurado

**Fecha de entrega:** 2026-09-10
**Tipo de proyecto:** Videojuego móvil de cartas por turnos (Beta)

---

## 1. Link de ingreso al proyecto

El juego se distribuye como **APK de Android** (versión Beta).

**📥 Descarga (Google Drive):**
https://drive.google.com/drive/folders/1JCkD1l_6C_888zdqzfZU-WK_OU90tSqb?usp=drive_link

**Archivo:** `Eggodia.apk` (~223 MB)

**Instrucciones de instalación:**
1. Abrir el link desde el celular Android.
2. Descargar `Eggodia.apk`.
3. Al abrir el APK, permitir "instalación de apps de origen desconocido".
4. Instalar y abrir "Eggodia" desde el menú de apps.

> Requiere **Android 8.0 o superior**. Ocupa aproximadamente **223 MB**.

**Repositorio del código fuente (privado):** `github.com/JeremyGomezf/Eggodia`
*(161 commits · 3 ramas · 4 contribuidores)*

---

## 2. Usuario y clave de ingreso

El juego incluye **modo invitado sin registro** — puedes jugar directamente contra la IA (VS BOT) sin crear cuenta.

Si deseas probar el modo **ONLINE** o el ranking, puedes registrar un usuario desde el propio juego (opción disponible en la pantalla de login).

Al ingresar como invitado aparecerás como **"Invitado" — Nv. 1** con **0 monedas** iniciales.

---

## 3. Imagen del ambiente de inicio

### Menú principal del juego

`[imagen: 01_menu_principal.png]`

*Pantalla de inicio de Eggodia. El menú muestra:*
- **Barra superior**: contador de monedas, nombre del jugador ("Invitado" o usuario logueado), nivel actual, botón de ajustes y herramientas.
- **Botón CARTAS** (izquierda): abre la colección y el constructor de mazo.
- **Botón TIENDA** (derecha): compra de cartas y cosméticos con monedas.
- **Botón ONLINE**: modo multijugador contra otros jugadores.
- **Botón VS BOT**: partida rápida contra la IA con dificultad adaptativa.
- **Centro**: mascota del juego (el "Rey Huevo") con el reloj de arena (tiempo) y despertador (partida rápida).

### Estructura del proyecto (Visual Studio Code)

`[imagen: 02_visual_studio.png]`

*El proyecto está organizado en tres módulos: `Backend/` (API REST), `Database/` (esquema SQL) y `Frontend/` (juego Godot con carpetas para cartas, escenas, imágenes, música y scripts).*

### Repositorio en GitHub

`[imagen: 03_github_repo.png]`

*Repositorio privado en GitHub con 161 commits, 3 ramas y 4 contribuidores. Historial completo del desarrollo desde el prototipo inicial hasta la Beta actual.*

---

## 4. Breve explicación del funcionamiento

**Eggodia** es un juego de cartas de batalla por turnos para Android, ambientado en cuatro eras (Primordial, Medieval, Moderna y Mística). Inspirado en juegos como Clash Royale y Hearthstone, con mecánicas propias enfocadas en estrategia rápida.

### Cómo se juega

1. **Ingreso** — Entra como invitado o con tu cuenta desde el menú principal.
2. **Construir mazo** — En la sección **CARTAS**, arma un mazo de 8 cartas eligiendo de una colección de 13 tropas (dragones, golems, dinosaurios, soldados, piezas de ajedrez, etc.).
3. **Elegir modo** — Pulsa **VS BOT** para partida rápida contra la IA, o **ONLINE** para multijugador.
4. **Apertura** — Al iniciar la partida, cada jugador coloca 3 tropas en sus carriles de invocación.
5. **Turnos** — Se alternan turnos con energía limitada (3 a 5 puntos, escalable) para invocar nuevas tropas, atacar, defender o usar habilidades especiales.
6. **Combate** — El daño depende del ataque de la tropa, con multiplicadores por **ventaja de era** (+20%) y **ventaja de tipo elemental** (+25%), además de golpes críticos aleatorios (×1.5).
7. **Hechizos** — 5 hechizos por partida: Encebollado (buff), Curación, Robar Carta, Veneno y Bloqueo.
8. **Victoria** — Gana quien reduzca a 0 los HP de la base rival, o quien tenga más HP al agotarse el tiempo (5 minutos).
9. **Recompensas** — Al terminar la partida, ganas monedas (más si ganas o si mantienes racha) para usar en la **TIENDA**.

### Sistemas implementados

- **Sistema de tipos elemental** — 5 tipos (Fuego, Agua, Naturaleza, Metal, Sombra) con relaciones fuerte/débil tipo piedra-papel-tijera de 5 vías.
- **IA adaptativa** que ajusta su dificultad (Fácil / Normal / Difícil) según el desempeño del jugador.
- **Sistema de logros** — Primera victoria, racha de victorias, uso de habilidad especial.
- **Economía persistente** — Monedas que sobreviven al cierre del juego, base para la Tienda.
- **Sistema de niveles** — Progreso persistente del jugador (Nv. 1 al iniciar).
- **Historial de batalla** en tiempo real — panel desplegable que registra cada acción.
- **Bestiario** — Pantalla con todas las tropas, sus stats, tipos y habilidades.
- **Tutorial automático** en la primera partida.

### Tecnología usada

| Componente | Tecnología |
|---|---|
| Motor de juego | Godot 4.6.1 (Mono / C#) |
| Backend (login / ranking) | ASP.NET Core 8 |
| Base de datos | SQLite |
| Exportación | Android APK (arm64 + arm7) |
| Lenguaje | C# 12 (.NET 8) |
| Control de versiones | Git + GitHub (privado) |

### Estado del proyecto

Esta es una **versión Beta incompleta**. Funcionalidades ya implementadas:
- ✅ Sistema de batalla completo con IA adaptativa
- ✅ 13 tropas con habilidades únicas y tipos elementales
- ✅ Sistema de eras y ventajas de tipo
- ✅ Constructor de mazos con colección completa
- ✅ Menú principal, tutorial, bestiario, opciones
- ✅ Login / registro opcional y modo invitado
- ✅ Sistema de monedas persistente

Funcionalidades pendientes para la versión final:
- ⏳ Tienda de cartas totalmente funcional
- ⏳ Modo ONLINE (multijugador PvP)
- ⏳ Más eras y facciones desbloqueables
- ⏳ Cosméticos y skins

---

## Créditos

Desarrollado por **Jeremy Gomez**, **Gonzalo Siguenza** y **Carlos Jurado** como proyecto final de Casa Abierta.

Motor: Godot Engine 4.6.1 (open-source, MIT).

