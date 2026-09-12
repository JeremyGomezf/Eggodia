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

		if (_manoVisualCPU.Count < 3)
		{
			MostrarAviso("El rival todavía no tiene su mano completa", new Color(1f, 0.6f, 0.3f));
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
		for (int i = 0; i < _manoVisualCPU.Count && i < 3; i++)
		{
			int idxTropa = _manoVisualCPU[i];
			var carta = CrearCartaVisualRobo(idxTropa);
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
						ResolverRobo(idxTropa);
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

	private Panel CrearCartaVisualRobo(int idxTropa)
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
		if (idxTropa >= 0 && idxTropa < imagenesCartas.Length && ResourceLoader.Exists(imagenesCartas[idxTropa]))
			tex.Texture = GD.Load<Texture2D>(imagenesCartas[idxTropa]);
		panel.AddChild(tex);

		Tween tw = panel.CreateTween().SetLoops();
		tw.TweenProperty(panel, "modulate", new Color(1.15f, 1.15f, 1.0f), 0.7f);
		tw.TweenProperty(panel, "modulate", Colors.White, 0.7f);
		return panel;
	}

	private void ResolverRobo(int idxTropa)
	{
		_manoVisualCPU.Remove(idxTropa);
		_cartaRobada = CrearCartaConIndice("Spot4", idxTropa, ESCALA_MANO_ROBADA);
		_cartaRobadaPendiente = true; // "Robar Carta" no vuelve a estar disponible hasta jugar esta
		ReacomodarManoTropas();       // ahora hay 4: se acomodan juntas y un poco más chicas
		MostrarAviso("¡Le robaste una carta al rival!", new Color(1f, 0.85f, 0.3f));
		CerrarPantallaRobo();
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
