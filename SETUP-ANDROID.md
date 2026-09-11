# Eggodia — Guía para exportar la APK (Android)

Esta guía explica cómo generar el `.apk` del juego para probarlo en un celular.
El juego es **Godot 4.7.2 (Mono / C#)**, así que la exportación a Android es un poco
más pesada que un proyecto solo con GDScript.

> **IMPORTANTE — versión de Godot:** todo el equipo debe usar **Godot 4.7.2 Mono**.
> El `Eggodia.csproj` y `project.godot` guardan la versión (`Godot.NET.Sdk/4.7.2`,
> `features "4.7"`); si abres el proyecto con otra versión de Godot, esos archivos se
> reescriben y el **export de la APK falla** (se genera incompleta, sin el C#). Si ves
> un conflicto de git en esos dos archivos, es señal de que alguien usó otra versión.

> **¿Solo quieres jugar/probar?** No necesitas nada de esto.
> - Clona el repo y ábrelo en **Godot 4.7.2 mono** → juega en la PC, **o**
> - Pide el archivo `Eggodia.apk` a quien lo exporta y instálalo en tu celular
>   (Android pedirá permiso de **"Instalar apps desconocidas"** → concédelo).
>
> Solo sigue lo de abajo si **tú** vas a exportar la APK desde tu propia PC.

---

## Lo que hay que instalar (una vez por PC)

| Herramienta | Versión usada | Notas |
|---|---|---|
| **Godot 4.7.2 – Mono/.NET** | 4.7.2-stable_mono | El editor con soporte C# |
| **JDK 17** (Temurin) | 17.x | Godot usa JDK 17 para el build de Gradle (el 21 rompe) |
| **Android SDK** | platform-tools, build-tools **36.1.0**, platform **android-36** | Godot 4.7.2 usa compile/target SDK 36 |
| **Android NDK** | **29.0.14206865** | La que pide Godot 4.7.2. Solo es *necesaria* si compilas código nativo; una APK normal exporta sin ella (pero conviene tenerla para evitar avisos) |
| **Plantillas de exportación de Godot** | 4.7.2.stable.mono | Editor → *Administrar plantillas de exportación* → Descargar |
| **Workload .NET de Android** | `dotnet workload install android` | Necesario para C# en Android |
| **Keystore de debug** | — | Para firmar la APK de prueba |

> **Espacio en disco:** el toolchain completo ocupa ~4–5 GB. Ten al menos ~6 GB libres.

### Comandos de referencia (Windows / PowerShell)

```powershell
# JDK 17
winget install -e --id EclipseAdoptium.Temurin.17.JDK

# Android SDK (command-line tools -> sdkmanager) instalado en C:\Android\Sdk
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-17...-hotspot"
& "C:\Android\Sdk\cmdline-tools\latest\bin\sdkmanager.bat" --sdk_root="C:\Android\Sdk" `
  "platform-tools" "build-tools;36.1.0" "platforms;android-36" "ndk;29.0.14206865"

# Workload de .NET para Android
dotnet workload install android

# Keystore de debug (con el keytool del JDK 17)
keytool -genkeypair -v -keystore C:\Android\debug.keystore -alias androiddebugkey `
  -keyalg RSA -keysize 2048 -validity 10000 -storepass android -keypass android `
  -dname "CN=Android Debug,O=Android,C=US"
```

---

## Configurar Godot (una vez)

1. **Editor → Ajustes del editor → Exportar → Android**, pon las rutas:
   - **Android SDK path** → `C:\Android\Sdk`
   - **Java SDK path (JDK)** → carpeta del JDK 17
   - **Debug keystore** → `C:\Android\debug.keystore` (user `androiddebugkey`, pass `android`)
2. **Proyecto → Instalar plantilla de compilación de Android.**
   Esto crea la carpeta `Frontend/android/build/` en tu PC.
   - Esa carpeta **está ignorada por git a propósito** (es enorme y por-PC): no se sube,
     cada quien la instala con este paso.
   - Godot crea solo un `.gdignore` dentro de `android/` para no escanearla — **no lo borres**.

El **preset de exportación (`export_presets.cfg`) ya está en el repo**, así que la
plataforma "Android" te aparecerá lista; solo ajusta las rutas del punto 1.

---

## Exportar la APK

**Opción GUI (recomendada):**
1. **Proyecto → Exportar… → Android → "Exportar proyecto"**.
2. Guarda con nombre y extensión: `Eggodia.apk`.
3. El aviso amarillo *"C#/.NET en Android es experimental"* **no bloquea** — dale igual.
4. La primera vez Gradle descarga cosas y tarda varios minutos.

**Opción consola (más rápida para iterar):**
```powershell
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-17...-hotspot"
$env:ANDROID_HOME = "C:\Android\Sdk"
& "<ruta a>\godot.exe" --headless --path Frontend --export-debug "Android" "C:\ruta\Eggodia.apk"
```

---

## Instalar y probar en el celular

- **Por archivo:** copia el `.apk` al cel, ábrelo con *Mis archivos/Chrome*, y concede
  **"Instalar apps desconocidas"** a esa app cuando lo pida.
- **Por cable (adb):** con Depuración USB activada:
  ```powershell
  & "C:\Android\Sdk\platform-tools\adb.exe" install -r "C:\ruta\Eggodia.apk"
  ```

---

## Cosas ya resueltas en el código (contexto)

- **Rendimiento/memoria móvil:** la pantalla de carga **no** precarga todas las escenas en
  móvil (antes usaba ~2.6 GB y el sistema mataba la app). Se cargan bajo demanda.
- **Orientación:** bloqueada a horizontal (`sensor_landscape`).
- **Texturas:** compresión **ETC2/ASTC** activada para Android (menos memoria).
- **Backend/online:** la URL se resuelve sola con `ApiConfig` (localhost en PC,
  IP de la LAN en móvil). El modo **"Jugar como invitado"** funciona sin backend.
  Para login online desde el cel: backend corriendo en la PC (`dotnet run` en
  `Backend/Eggodia`, escuchando en `0.0.0.0:5289`), mismo Wi-Fi, y permitir HTTP *cleartext*.

---

## Recomendación para el equipo

Para un grupo pequeño, lo práctico es que **una persona exporte la APK y la comparta**
como archivo. Los demás solo la instalan en su celular — **no** necesitan montar todo el
toolchain de Android cada uno.
