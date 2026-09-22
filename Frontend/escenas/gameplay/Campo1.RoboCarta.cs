using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HECHIZO "ROBAR CARTA": mini-juego de robo de mano rival ─────────────
	// El CPU no dibuja una mano real de antemano (elige tropa al azar recién en el momento de
	// jugar — ver Campo1.MazoRobo.cs). Para que "Robar Carta" tenga sentido, le reservamos una
	// mano VISUAL de 3 cartas que se muestra en esta pantalla y que sí se reduce/repone de
	// verdad con cada robo; no cambia cómo decide jugar la IA, solo qué le puede robar el jugador.
	private List<int> _manoVisualCPU = new();
	private CanvasLayer _pantallaRobo;

	// Tamaño de las 3 cartas reveladas en la pantalla de robo — bien grandes, son el centro de
	// atención de esa pantalla.
	private const float ROBO_CARD_W = 260f;
	private const float ROBO_CARD_H = 364f;

	// ── MANO REAL DEL RIVAL ───────────────────────────────────────────────
	// VS BOT: es _manoVisualCPU, y el bot juega DE AHÍ (robarle una se la quita de verdad).
	// ONLINE: es la mano real del otro jugador, que viaja en cada snapshot ("mano").
	private readonly List<string> _manoRivalOnline = new();

	/// <summary>Cartas que el rival tiene ahora mismo en la mano: (índice en mi mazo o -1, escena).</summary>
	private List<(int idx, string escena)> CartasEnManoDelRival()
	{
		var lista = new List<(int, string)>();
		if (EsOnline)
		{
			foreach (string escena in _manoRivalOnline)
				if (!string.IsNullOrEmpty(escena)) lista.Add((Array.IndexOf(escenasTropas, escena), escena));
		}
		else
		{
			foreach (int i in _manoVisualCPU)
				if (i >= 0 && i < escenasTropas.Length) lista.Add((i, escenasTropas[i]));
		}
		return lista;
	}

	/// <summary>Escenas de las cartas de MI mano (para que el rival en línea vea qué robarme).</summary>
	private Godot.Collections.Array MisCartasEnManoOnline()
	{
		var arr = new Godot.Collections.Array();
		if (contenedorMano == null) return arr;
		foreach (Node n in contenedorMano.GetChildren())
			if (n is Carta c && c.EstaEnMano && !c.IsQueuedForDeletion() && c.EscenaTropa != null)
				arr.Add(c.EscenaTropa.ResourcePath);
		return arr;
	}

	/// <summary>Online: el rival me robó esta carta — se va de mi mano de verdad.</summary>
	private void PerderCartaPorRoboOnline(string escena)
	{
		if (contenedorMano == null || string.IsNullOrEmpty(escena)) return;
		foreach (Node n in contenedorMano.GetChildren())
		{
			if (n is not Carta c || !c.EstaEnMano || c.IsQueuedForDeletion()) continue;
			if (c.EscenaTropa == null || c.EscenaTropa.ResourcePath != escena) continue;
			c.NombreSpot = "X";
			c.QueueFree();
			ReacomodarManoTropas();
			MostrarAviso("¡El rival te robó una carta de la mano!", new Color(1f, 0.45f, 0.4f));
			return;
		}
	}

	private void InicializarManoVisualCPU()
	{
		_manoVisualCPU.Clear();
		var fuente = new List<string>(_mazoCPU);
		BarajarLista(fuente);
		foreach (string ruta in fuente)
		{
			if (_manoVisualCPU.Count >= 3) break;
			int idx = Array.IndexOf(escenasTropas, ruta);
			if (idx >= 0 && !_manoVisualCPU.Contains(idx)) _manoVisualCPU.Add(idx);
		}
		RellenarManoVisualCPUSiFalta();
	}

	// Repone hasta 3 cartas — se llama al empezar cada turno del rival, igual que la mano del
	// jugador se completa al empezar el suyo (CompletarManoAlInicio).
	private void RellenarManoVisualCPUSiFalta()
	{
		if (escenasTropas == null || escenasTropas.Length == 0) return;
		while (_manoVisualCPU.Count < 3)
		{
			var candidatos = new List<int>();
			// Tras la bomba Nuclear: sin repetir lo que el bot tiene en sus carriles ni lo que murió
			// (si eso deja sin opciones, se relaja para no dejarlo nunca sin mano).
			for (int i = 0; i < escenasTropas.Length; i++)
				if (!_manoVisualCPU.Contains(i) && !(_excluirRepartoNuclearCPU?.Contains(i) ?? false)) candidatos.Add(i);
			if (candidatos.Count == 0)
				for (int i = 0; i < escenasTropas.Length; i++) if (!_manoVisualCPU.Contains(i)) candidatos.Add(i);
			if (candidatos.Count == 0) break;
			_manoVisualCPU.Add(candidatos[random.Next(candidatos.Count)]);
		}
	}

	// Se suelta la carta "Robar Carta" sobre el huevo rival (tronoRival) — único objetivo válido;
	// ya no sobre las tropas rivales, para que el aro amarillo que se ve al arrastrar coincida
	// exactamente con dónde hay que soltar. Solo se puede usar cuando el rival tiene su mano
	// completa (3 cartas); si no, se avisa y la carta vuelve a la mano.
	private bool ResolverSueltaRoboDesdeCarta(int slotIdx)
	{
		Vector2 mouseMundo = GetGlobalMousePosition();
		bool sobreHuevoRival = tronoRival != null && IsInstanceValid(tronoRival)
			&& tronoRival.GlobalPosition.DistanceTo(mouseMundo) < 160f;
		if (!sobreHuevoRival) return false;

		if (CartasEnManoDelRival().Count == 0)
		{
			MostrarAviso("El rival no tiene cartas en la mano", new Color(1f, 0.6f, 0.3f));
			return false;
		}

		Preferencias.RegistrarUsoHechizo("Robar Carta");
		MarcarHechizoUsado(1);
		_hechizoUsadoEsteTurno = true;
		AutoReemplazarHechizo(slotIdx);
		RegistrarGastoMovimiento();
		MostrarPantallaRobo();
		return true;
	}

	// Pantalla semi-oscura con las 3 cartas de mano del rival, centradas y con brillo. Doble
	// toque sobre una la roba: aparece en Spot4 de ManoManual (el 4º spot, reservado y fuera del
	// ciclo normal de reposición de 3 cartas) y el rival se queda con 2 hasta su próximo turno.
	private void MostrarPantallaRobo()
	{
		if (_pantallaRobo != null && IsInstanceValid(_pantallaRobo)) return;

		var capa = new CanvasLayer();
		capa.Layer = 250;
		AddChild(capa);
		_pantallaRobo = capa;

		var fondo = new ColorRect();
		fondo.Color = new Color(0.02f, 0.02f, 0.05f, 0.88f);
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop; // bloquea clics al tablero de atrás
		fondo.Modulate = new Color(1, 1, 1, 0);
		capa.AddChild(fondo);

		var titulo = new Label();
		titulo.Text = "ROBAR LA CARTA DEL RIVAL";
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		titulo.AddThemeColorOverride("font_outline_color", Colors.Black);
		titulo.AddThemeConstantOverride("outline_size", 8);
		titulo.AddThemeFontSizeOverride("font_size", 44);
		titulo.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		titulo.OffsetTop = 45; titulo.OffsetBottom = 105;
		titulo.MouseFilter = Control.MouseFilterEnum.Ignore;
		fondo.AddChild(titulo);

		var sub = new Label();
		sub.Text = "Presiona DOS VECES una carta para robarla";
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
		sub.AddThemeFontSizeOverride("font_size", 25);
		sub.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		sub.OffsetTop = 108; sub.OffsetBottom = 145;
		sub.MouseFilter = Control.MouseFilterEnum.Ignore;
		fondo.AddChild(sub);

		var centro = new CenterContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centro.MouseFilter = Control.MouseFilterEnum.Ignore;
		fondo.AddChild(centro);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 34);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		centro.AddChild(hbox);

		var resuelto = new bool[] { false }; // referencia compartida entre los 3 closures de abajo
		var manoRival = CartasEnManoDelRival();
		for (int i = 0; i < manoRival.Count && i < 4; i++) // hasta 4: puede tener una robada
		{
			int    idxTropa   = manoRival[i].idx;
			string escenaRival = manoRival[i].escena;
			var carta = CrearCartaVisualRobo(idxTropa, escenaRival);
			hbox.AddChild(carta);

			var ultimoClic = new double[] { -10 };
			carta.GuiInput += (@event) =>
			{
				if (resuelto[0]) return;
				if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					double ahora = Time.GetTicksMsec() / 1000.0;
					if (ahora - ultimoClic[0] < 0.45)
					{
						resuelto[0] = true;
						ResolverRobo(idxTropa, escenaRival);
					}
					else
					{
						ultimoClic[0] = ahora;
						Tween tw = carta.CreateTween();
						tw.TweenProperty(carta, "scale", new Vector2(1.06f, 1.06f), 0.08f);
						tw.TweenProperty(carta, "scale", Vector2.One, 0.08f);
					}
				}
			};
		}

		Tween twIn = fondo.CreateTween();
		twIn.TweenProperty(fondo, "modulate:a", 1f, 0.25f);
	}

	private Panel CrearCartaVisualRobo(int idxTropa, string escena = "")
	{
		var panel = new Panel();
		panel.CustomMinimumSize = new Vector2(ROBO_CARD_W, ROBO_CARD_H);
		panel.PivotOffset = new Vector2(ROBO_CARD_W, ROBO_CARD_H) / 2f;
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.08f, 0.06f, 0.03f, 0.97f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 4;
		sb.BorderColor = new Color(1f, 0.82f, 0.3f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 16;
		sb.ShadowColor = new Color(1f, 0.82f, 0.3f, 0.55f); sb.ShadowSize = 20;
		panel.AddThemeStyleboxOverride("panel", sb);

		var tex = new TextureRect();
		tex.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		tex.OffsetLeft = 10; tex.OffsetRight = -10; tex.OffsetTop = 10; tex.OffsetBottom = -10;
		tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		tex.MouseFilter = Control.MouseFilterEnum.Ignore;
		// La carta del rival en línea puede no estar en MI mazo: su imagen se saca de su escena.
		string imagen = idxTropa >= 0 && idxTropa < imagenesCartas.Length
			? imagenesCartas[idxTropa]
			: ClasificacionCartas.ImagenBatalla(escena);
		if (!string.IsNullOrEmpty(imagen) && ResourceLoader.Exists(imagen))
			tex.Texture = GD.Load<Texture2D>(imagen);
		panel.AddChild(tex);

		Tween tw = panel.CreateTween().SetLoops();
		tw.TweenProperty(panel, "modulate", new Color(1.15f, 1.15f, 1.0f), 0.7f);
		tw.TweenProperty(panel, "modulate", Colors.White, 0.7f);
		return panel;
	}

	// Si ya hay un slot libre en la mano (Spot1/2/3, porque jugaste una carta este turno y todavía
	// no se rellenó), la carta robada ocupa ESE lugar y la mano se sigue viendo de 3. Solo cuando
	// los 3 están ocupados de verdad se usa Spot4 y entra el modo compacto de 4 cartas.
	private void ResolverRobo(int idxTropa, string escenaRival = "")
	{
		// Se la saca de la mano del rival DE VERDAD: el bot ya no la puede jugar, y en línea se le
		// quita de su mano en su propia pantalla (acción "robar").
		if (EsOnline)
		{
			_manoRivalOnline.Remove(escenaRival);
			EmitirAccionOnline("robar", new Godot.Collections.Dictionary { { "escena", escenaRival } });
		}
		else _manoVisualCPU.Remove(idxTropa);

		string spotDestino = "Spot4";
		float escalaDestino = ESCALA_MANO_ROBADA;
		foreach (string s in SPOTS_MANO)
		{
			bool ocupado = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && c.EstaEnMano && !c.IsQueuedForDeletion()) { ocupado = true; break; }
			if (!ocupado) { spotDestino = s; escalaDestino = ESCALA_MANO_NORMAL; break; }
		}

		_cartaRobada = idxTropa >= 0
			? CrearCartaConIndice(spotDestino, idxTropa, escalaDestino)
			: CrearCartaDeOtroMazo(spotDestino, escenaRival, escalaDestino); // carta que no está en mi mazo
		_cartaRobadaPendiente = true; // "Robar Carta" no vuelve a estar disponible hasta jugar esta
		if (spotDestino == "Spot4") ReacomodarManoTropas(); // ahora sí hay 4: modo compacto
		MostrarAviso("¡Le robaste una carta al rival!", new Color(1f, 0.85f, 0.3f));
		CerrarPantallaRobo();
	}

	/// <summary>Crea en la mano una carta que NO pertenece a mi mazo (robada a un rival en línea): se
	/// arma con su escena y su imagen de batalla, con IdCarta -1 (no entra en los cooldowns del mazo).</summary>
	private Carta CrearCartaDeOtroMazo(string spot, string escena, float escalaBase)
	{
		if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return null;
		if (string.IsNullOrEmpty(escena) || !ResourceLoader.Exists(escena)) return null;
		Marker2D marcador = contenedorMano.GetNodeOrNull<Marker2D>(spot);
		if (marcador == null) return null;

		Carta n = (Carta)escenaCartaBase.Instantiate();
		n.NombreSpot = spot;
		contenedorMano.AddChild(n);
		n.Rotation = marcador.Rotation;
		Vector2 esc = new Vector2(escalaBase, escalaBase);
		n.Scale = esc;
		n.GlobalPosition = marcador.GlobalPosition - (n.Size * esc / 2);
		n.GuardarEstadoOriginal();
		n.AsignarDatos(ClasificacionCartas.ImagenBatalla(escena), escena, -1);
		return n;
	}

	// "Robar Carta" no puede volver a ofrecerse mientras la carta robada anterior (Spot4) siga
	// en la mano — se llama cada cambio de turno para detectar en cuanto se juegue.
	private void ActualizarEstadoCartaRobada()
	{
		if (!_cartaRobadaPendiente) return;
		// Se rastrea por REFERENCIA (no por NombreSpot), porque al volver a 3 la robada se reasigna a
		// un slot normal (Spot1/2/3). Sigue pendiente mientras esa carta siga en la mano.
		bool sigue = _cartaRobada != null && IsInstanceValid(_cartaRobada)
			&& _cartaRobada.EstaEnMano && !_cartaRobada.IsQueuedForDeletion();
		if (!sigue) { _cartaRobadaPendiente = false; _cartaRobada = null; }
	}

	// Cierra y libera la pantalla de robo de forma robusta: la referencia se limpia ANTES de
	// pedir el QueueFree, así nada puede quedar "enganchado" a una capa que ya se está muriendo.
	private void CerrarPantallaRobo()
	{
		if (_pantallaRobo == null || !IsInstanceValid(_pantallaRobo)) { _pantallaRobo = null; return; }
		var capa = _pantallaRobo;
		_pantallaRobo = null;
		capa.QueueFree();
	}
}
