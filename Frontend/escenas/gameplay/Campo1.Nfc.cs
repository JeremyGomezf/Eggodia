using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Módulo de soporte NFC en combate para Campo1.
/// 
/// Reglas de invocación táctica física:
/// - Se activa durante el turno del jugador cuando se detecta el escaneo de una tarjeta física NFC.
/// - 0 casillas libres -> Aviso "No hay casillas disponibles" (no consume la tarjeta).
/// - 1 casilla libre   -> Invocación automática en el carril libre.
/// - 2 o 3 casillas    -> Popup flotante para seleccionar en qué carril desplegarla.
/// 
/// ════════════════════════════════════════════════════════════════════════════
/// GUÍA DE INTEGRACIÓN FUTURA PARA MÓVILES (NATIVE MOBILE BRIDGES):
/// ════════════════════════════════════════════════════════════════════════════
/// 1. ANDROID (Java/Kotlin Plugin):
///    - Utilizar NfcAdapter en el MainActivity o mediante plugin Godot Android:
///        NfcAdapter nfcAdapter = NfcAdapter.getDefaultAdapter(activity);
///        nfcAdapter.enableReaderMode(activity, callback, NfcAdapter.FLAG_READER_NFC_A | ..., null);
///    - Al recibir el Tag NDEF con el código/ID de la tarjeta física, emitir una señal
///      o llamar al método C#:
///        campo1.Call("ProcesarEscaneoNfcBatalla", cardId);
///
/// 2. iOS (Swift Plugin):
///    - Utilizar el framework CoreNFC:
///        class NFCReader: NSObject, NFCTagReaderSessionDelegate { ... }
///    - Al leer el payload de 5 dígitos/ID, despachar al hilo principal de Godot:
///        campo1.ProcesarEscaneoNfcBatalla(cardId)
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public partial class Campo1 : Node2D
{
	private static readonly Dictionary<string, (string nombre, string rutaEscena)> MAPA_CARTAS_NFC = new(StringComparer.OrdinalIgnoreCase)
	{
		{ "tiburon",          ("Tiburón",          "res://cartas prime/PACIFICO/Tiburon.tscn") },
		{ "calamar",          ("Calamar Gigante",  "res://cartas prime/PACIFICO/CalamarG_prime.tscn") },
		{ "calamar_gigante",  ("Calamar Gigante",  "res://cartas prime/PACIFICO/CalamarG_prime.tscn") },
		{ "soldadocartoon",   ("Soldado Cartoon",  "res://cartas prime/TOONS/Soldado_cartoon_prime.tscn") },
		{ "soldado_cartoon",  ("Soldado Cartoon",  "res://cartas prime/TOONS/Soldado_cartoon_prime.tscn") },
		{ "campero",          ("Campero",          "res://cartas prime/TOONS/Campero_cartoon_prime.tscn") },
		{ "kabar",            ("Ka-Bar",           "res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn") },
		{ "granadero",        ("Granadero",        "res://cartas prime/TOONS/Granadero_cartoon_prime.tscn") },
		{ "tanque",           ("Tanque",           "res://cartas prime/TOONS/Tanque_cartoon_prime.tscn") },
	};

	public override void _UnhandledInput(InputEvent @event)
	{
		// Tecla 'N' para simular escaneo de tarjeta NFC física en PC / pruebas de desarrollo
		if (@event is InputEventKey ek && ek.Pressed && !ek.Echo && ek.Keycode == Key.N)
		{
			ProcesarEscaneoNfcBatalla("tiburon");
		}
	}

	public List<Node2D> ObtenerSpotsLibresJugador()
	{
		var libres = new List<Node2D>();
		foreach (string nombre in new[] { "Mod1", "Mod2", "Mod3" })
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona != null && zona.GetNodeOrNull("Ocupado") == null)
			{
				libres.Add(zona);
			}
		}
		return libres;
	}

	public void ProcesarEscaneoNfcBatalla(string cardId)
	{
		if (juegoTerminado) return;

		if (!esTurnoJugador)
		{
			MostrarAviso("Solo puedes invocar cartas en tu turno", Colors.Yellow);
			return;
		}

		if (string.IsNullOrEmpty(cardId)) cardId = "tiburon";
		string key = cardId.ToLowerInvariant().Trim();

		if (!MAPA_CARTAS_NFC.TryGetValue(key, out var info))
		{
			info = ("Tropa Física", "res://cartas prime/PACIFICO/Tiburon.tscn");
		}

		var libres = ObtenerSpotsLibresJugador();

		if (libres.Count == 0)
		{
			// 0 casillas disponibles -> Notificar sin consumir
			MostrarAviso("No hay casillas disponibles", new Color(1f, 0.4f, 0.35f));
			return;
		}

		if (libres.Count == 1)
		{
			// 1 casilla libre -> Invocación automática directa
			InvocarTropaNfcDirecta(libres[0], info.nombre, info.rutaEscena);
		}
		else
		{
			// 2 o 3 casillas libres -> Popup modal para elegir carril
			MostrarPopupSeleccionCarrilNfc(libres, info.nombre, info.rutaEscena);
		}
	}

	private void InvocarTropaNfcDirecta(Node2D puntoMod, string nombre, string rutaEscena)
	{
		var escena = GD.Load<PackedScene>(rutaEscena);
		if (escena == null)
		{
			MostrarAviso($"Error cargando tropa: {nombre}", Colors.Red);
			return;
		}

		bool ok = TropaInvocada(puntoMod, escena);
		if (ok)
		{
			GlobalAudioManager.Instance?.PlayClickSound();
			MostrarAviso($"¡{nombre} invocado mediante NFC!", Colors.GreenYellow);
		}
	}

	private void MostrarPopupSeleccionCarrilNfc(List<Node2D> spotsLibres, string nombre, string rutaEscena)
	{
		var capa = new CanvasLayer { Layer = 320 };
		AddChild(capa);

		var fondo = new ColorRect();
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.Color = new Color(0.02f, 0.04f, 0.08f, 0.88f);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		var caja = new VBoxContainer();
		caja.SetAnchorsPreset(Control.LayoutPreset.Center);
		caja.AddThemeConstantOverride("separation", 24);
		caja.OffsetLeft = -380; caja.OffsetRight = 380;
		caja.OffsetTop = -200; caja.OffsetBottom = 200;
		fondo.AddChild(caja);

		var titulo = new Label();
		titulo.Text = $"INVOCACIÓN NFC: {nombre.ToUpper()}";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AddThemeFontSizeOverride("font_size", 34);
		titulo.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.35f));
		caja.AddChild(titulo);

		var subtexto = new Label();
		subtexto.Text = "Selecciona la casilla donde deseas desplegar tu tropa:";
		subtexto.HorizontalAlignment = HorizontalAlignment.Center;
		subtexto.AddThemeFontSizeOverride("font_size", 22);
		subtexto.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 1f));
		caja.AddChild(subtexto);

		var hboxBotones = new HBoxContainer();
		hboxBotones.Alignment = BoxContainer.AlignmentMode.Center;
		hboxBotones.AddThemeConstantOverride("separation", 20);
		caja.AddChild(hboxBotones);

		foreach (var spot in spotsLibres)
		{
			string nombreCarril = spot.Name.ToString() switch
			{
				"Mod1" => "Carril 1 (Izq)",
				"Mod2" => "Carril 2 (Centro)",
				"Mod3" => "Carril 3 (Der)",
				_      => spot.Name.ToString()
			};

			var btnSpot = new Button();
			btnSpot.Text = nombreCarril;
			btnSpot.CustomMinimumSize = new Vector2(210, 80);
			btnSpot.AddThemeFontSizeOverride("font_size", 22);

			var spotRef = spot;
			btnSpot.Pressed += () =>
			{
				capa.QueueFree();
				InvocarTropaNfcDirecta(spotRef, nombre, rutaEscena);
			};
			hboxBotones.AddChild(btnSpot);
		}

		var btnCancelar = new Button();
		btnCancelar.Text = "CANCELAR";
		btnCancelar.CustomMinimumSize = new Vector2(180, 56);
		btnCancelar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btnCancelar.AddThemeFontSizeOverride("font_size", 18);
		btnCancelar.Pressed += () => capa.QueueFree();
		caja.AddChild(btnCancelar);
	}
}
