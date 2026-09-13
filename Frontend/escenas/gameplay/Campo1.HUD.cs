using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── REGISTRO DE COMBATE (sin UI propia: el botón/panel HISTORIAL del HUD viejo se
	// eliminó; Campo1.Combate.cs sigue llamando a RegistrarEvento, que ahora no hace nada
	// visible ya que _historialContenido nunca se asigna) ──────────────────────────────
	private VBoxContainer _historialContenido;

	public void RegistrarEvento(string texto, Color acento)
	{
		if (_historialContenido == null) return;
		var linea = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.10f, 0.13f, 0.19f, 0.75f);
		sb.BorderWidthLeft = 3;
		sb.BorderColor = acento;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 4;
		sb.ContentMarginLeft = sb.ContentMarginRight = 8;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 5;
		linea.AddThemeStyleboxOverride("panel", sb);
		var l = new Label();
		l.Text = texto;
		l.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 1f));
		l.AddThemeFontSizeOverride("font_size", 12);
		l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		linea.AddChild(l);
		_historialContenido.AddChild(linea);
		_historialContenido.MoveChild(linea, 0);   // más reciente arriba
		while (_historialContenido.GetChildCount() > 12)
		{
			var last = _historialContenido.GetChild(_historialContenido.GetChildCount() - 1);
			last.QueueFree();
		}
	}

	// ── MANO DE HECHIZOS — ahora son cartas reales (Carta.cs, EsHechizo=true), con el MISMO
	// tamaño y las MISMAS reglas visuales de hover/arrastre/achicado que las cartas de tropa, en
	// vez de una interfaz de HUD "pegada". Se instancian en el contenedor "ManoHechizos" del
	// .tscn, sobre los Marker2D "SpotH1"/"SpotH2" (igual que ManoManual/Spot1-3 con las tropas).
	private const float HECHIZO_W = 160f; // tamaño de referencia: igual a carta_base.tscn
	private const float HECHIZO_H = 224f;

	private void CrearPanelHechizos()
	{
		_hechizosMano[0] = ElegirHechizoElegible();
		_hechizosMano[1] = ElegirHechizoElegible(_hechizosMano[0]);
		for (int i = 0; i < 2; i++) CrearCartaHechizo(i);

		// El botón "CAMBIAR" morado creado por código se reemplazó por el nodo
		// "ArdidBarButton" del .tscn — se cablea en ConfigurarInterfazNueva().

		// Instrucción genérica (oculta por defecto) — hoy solo la usa el Enroque de la Torre
		// ("Elige el carril de destino"); los hechizos ya no la necesitan porque se arrastran.
		Vector2 vp = GetViewport().GetVisibleRect().Size;
		_lblInstruccion = new Label();
		_lblInstruccion.Visible = false;
		_lblInstruccion.Position = new Vector2(vp.X / 2f - 170f, 130f);
		_lblInstruccion.Size     = new Vector2(340, 34);
		_lblInstruccion.HorizontalAlignment = HorizontalAlignment.Center;
		_lblInstruccion.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.4f));
		_lblInstruccion.AddThemeFontSizeOverride("font_size", 13);
		CapaHUD().AddChild(_lblInstruccion);
	}

	// Crea la carta del slot 0/1 en su Marker2D (SpotH1/SpotH2) dentro de "ManoHechizos". El
	// slot 0 (la que se elige primero) queda al frente a tamaño normal; el slot 1 queda detrás,
	// recta también (sin el volteo/rotación del abanico de tropas) pero un poco más chica, dando
	// la sensación de mazo apilado en vez de dos cartas sueltas idénticas.
	private void CrearCartaHechizo(int slotIdx)
	{
		if (juegoTerminado || escenaCartaBase == null) return;
		var contenedor = GetNodeOrNull<Control>("ManoHechizos");
		if (contenedor == null) return;
		string nombreSpot = slotIdx == 0 ? "SpotH1" : "SpotH2";
		Marker2D spot = contenedor.GetNodeOrNull<Marker2D>(nombreSpot);
		if (spot == null) return;

		int pi = _hechizosMano[slotIdx];
		Carta n = (Carta)escenaCartaBase.Instantiate();
		n.NombreSpot = nombreSpot;
		contenedor.AddChild(n);
		n.Rotation = 0f; // rectas, no "volteadas" como el abanico de la mano de tropas
		Vector2 esc = slotIdx == 0 ? new Vector2(0.85f, 0.85f) : new Vector2(0.76f, 0.76f);
		n.Scale  = esc;
		n.ZIndex = slotIdx == 0 ? 2 : 1;
		n.GlobalPosition = spot.GlobalPosition - (n.Size * esc / 2f);
		n.GuardarEstadoOriginal();
		n.AsignarDatosHechizo(POOL_HECHIZO_RUTA[pi], pi, slotIdx);
		_tarjetasHechizoCarta[slotIdx] = n;
	}

	// Reemplaza automáticamente la carta usada con otro hechizo elegible (cooldown en 0, y para
	// "Robar Carta" sin una carta robada pendiente de jugar) — o deja el slot vacío si por ahora
	// no hay ninguno disponible; se reintenta solo al empezar cada turno del jugador (ver
	// CambiarTurno). Siempre nace en el Marker2D correcto — nunca en la posición donde se soltó
	// la anterior, así jamás queda "pegada" junto al objetivo.
	private void AutoReemplazarHechizo(int slotIdx)
	{
		if (slotIdx < 0 || slotIdx >= 2) return;
		var actual = _tarjetasHechizoCarta[slotIdx];
		if (actual != null && IsInstanceValid(actual)) actual.QueueFree();
		_tarjetasHechizoCarta[slotIdx] = null;

		int otro = slotIdx == 0 ? 1 : 0;
		int excluir = (_tarjetasHechizoCarta[otro] != null && IsInstanceValid(_tarjetasHechizoCarta[otro])) ? _hechizosMano[otro] : -1;
		int nuevoPi = ElegirHechizoElegible(excluir);
		if (nuevoPi < 0) return; // nada elegible todavía: el slot queda vacío

		_hechizosMano[slotIdx] = nuevoPi;
		CrearCartaHechizo(slotIdx);
	}

	// Un hechizo (0..4) es elegible si su cooldown ya llegó a 0 y, para "Robar Carta" (1), si no
	// hay una carta robada anterior todavía sin jugar. "excluirPi" evita repetir el mismo hechizo
	// que ya se ve en el otro slot. Devuelve -1 si no hay ninguno disponible.
	private int ElegirHechizoElegible(int excluirPi = -1)
	{
		var candidatos = new System.Collections.Generic.List<int>();
		for (int pi = 0; pi < 5; pi++)
		{
			if (pi == excluirPi) continue;
			if (_cooldownHechizo[pi] > 0) continue;
			if (pi == 1 && _cartaRobadaPendiente) continue;
			candidatos.Add(pi);
		}
		return candidatos.Count == 0 ? -1 : candidatos[random.Next(candidatos.Count)];
	}

	// Pone en cooldown (5 turnos, contando jugador y rival) el hechizo pi recién usado.
	private void MarcarHechizoUsado(int pi)
	{
		if (pi < 0 || pi >= 5) return;
		_cooldownHechizo[pi] = 5;
	}

	// ── Puente entre Carta.cs (EsHechizo=true) y la lógica de hechizos ────────────────────────
	public bool IntentarIniciarArrastreHechizo(int slotIdx)
	{
		if (!ValidarHechizo() || EsHechizoUsado(slotIdx)) return false;
		if (_hechizoUsadoEsteTurno) { MostrarAvisoHechizoLimite(); return false; }
		MostrarResaltadoObjetivosHechizo(_hechizosMano[slotIdx]);
		return true;
	}

	public void FinalizarArrastreHechizo() => LimpiarResaltadoObjetivosHechizo();

	// Resuelve el soltado de una carta de hechizo sobre el tablero. "Robar Carta" (pi=1) tiene
	// su propio flujo (ResolverSueltaRoboDesdeCarta, en Campo1.RoboCarta.cs) porque no aplica un
	// efecto directo sino que abre la mini-pantalla de robo.
	public bool ResolverSueltaHechizoDesdeCarta(int slotIdx, int pi)
	{
		if (pi == 1) return ResolverSueltaRoboDesdeCarta(slotIdx);

		Vector2 mouseMundo = GetGlobalMousePosition();
		bool aliados = pi == 0 || pi == 4;
		string grupo = aliados ? "tropas_jugador" : "tropas_rival";
		Node2D objetivo = null; float mejor = 110f;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
			if (n is Node2D t && IsInstanceValid(t))
			{
				float d = t.GlobalPosition.DistanceTo(mouseMundo);
				if (d < mejor) { mejor = d; objetivo = t; }
			}

		if (objetivo == null || !AplicarHechizoADestino(pi, objetivo)) return false;

		string[] nombresHechizo = { "Curación", "Robar Carta", "Veneno", "Bloqueo", "Encebollado" };
		Preferencias.RegistrarUsoHechizo(nombresHechizo[pi]);
		_hechizoUsadoEsteTurno = true;
		AutoReemplazarHechizo(slotIdx);
		RegistrarGastoMovimiento();
		return true;
	}

	// Aros brillantes sobre los objetivos válidos mientras se arrastra un hechizo — mismo
	// tratamiento visual (aro redondeado con brillo pulsante) que los carriles de invocación de
	// tropas, pero a un tamaño discreto (parecido al círculo de slot de los Mod), no gigante.
	// "Robar Carta" resalta SOLO el huevo rival (tronoRival), nunca las 3 tropas.
	private void MostrarResaltadoObjetivosHechizo(int pi)
	{
		LimpiarResaltadoObjetivosHechizo();
		if (pi == 1)
		{
			if (tronoRival != null && IsInstanceValid(tronoRival))
				_resaltadosHechizoActivos.Add(CrearAroResaltadoHechizo(tronoRival, new Color(1f, 0.82f, 0.3f, 0.9f), 90f));
			return;
		}
		bool aliados = pi == 0 || pi == 4;
		string grupo = aliados ? "tropas_jugador" : "tropas_rival";
		Color colorAro = aliados ? new Color(0.35f, 1f, 0.5f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.9f);
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
			if (n is Node2D t && IsInstanceValid(t)) _resaltadosHechizoActivos.Add(CrearAroResaltadoHechizo(t, colorAro, 48f));
	}

	private Node CrearAroResaltadoHechizo(Node2D objetivo, Color color, float diametro = 48f)
	{
		var panel = new Panel();
		panel.Name = "AroHechizo";
		panel.CustomMinimumSize = new Vector2(diametro, diametro);
		panel.Size = new Vector2(diametro, diametro);
		panel.Position = new Vector2(-diametro / 2f, -diametro / 2f);
		panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0, 0, 0, 0);
		style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 3;
		style.BorderColor = color;
		style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
		style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = (int)(diametro / 2f);
		style.ShadowColor = color; style.ShadowSize = 6;
		panel.AddThemeStyleboxOverride("panel", style);
		objetivo.AddChild(panel);
		Tween tw = panel.CreateTween().SetLoops();
		tw.TweenProperty(panel, "modulate:a", 0.4f, 0.5f);
		tw.TweenProperty(panel, "modulate:a", 1.0f, 0.5f);
		return panel;
	}

	private void LimpiarResaltadoObjetivosHechizo()
	{
		foreach (var n in _resaltadosHechizoActivos) if (IsInstanceValid(n)) n.QueueFree();
		_resaltadosHechizoActivos.Clear();
	}

	public bool EsHechizoUsado(int slotIdx)
	{
		if (slotIdx < 0 || slotIdx >= 2 || _tarjetasHechizoCarta[slotIdx] == null) return false;
		int pi = _hechizosMano[slotIdx];
		if (pi == 1 && _cartaRobadaPendiente) return true;
		return _cooldownHechizo[pi] > 0;
	}

	// Botón "Ardid/Barajar": cambia las DOS cartas de hechizo de una sola vez (se van y vienen
	// las nuevas), sin pasos intermedios — antes había que activar un "modo cambio" y después
	// tocar una carta específica.
	private void ActivarModoCambio()
	{
		if (_usosCambioHechizo >= MAX_CAMBIO_HECHIZO || !ValidarHechizo()) return;
		_usosCambioHechizo++;
		for (int i = 0; i < 2; i++) AutoReemplazarHechizo(i);

		bool agotado = _usosCambioHechizo >= MAX_CAMBIO_HECHIZO;
		if (_btnCambiarHechizo != null)
		{
			_btnCambiarHechizo.Disabled = agotado;
			_btnCambiarHechizo.Modulate = agotado ? new Color(0.55f, 0.55f, 0.55f) : Colors.White;
		}
	}

	/// <summary>Aviso de "solo 1 hechizo por turno": reutiliza el mismo toast general del HUD.</summary>
	private void MostrarAvisoHechizoLimite() => MostrarAviso("Solo 1 Hechizo por turno", new Color(1f, 0.4f, 0.35f));

	// El gear (⚙) creado por código se reemplazó por el nodo "PausaButton" del .tscn,
	// cableado en Campo1.Extra.cs → ConfigurarInterfazNueva().

	private CanvasLayer _capaHUD;
	private CanvasLayer CapaHUD()
	{
		if (_capaHUD != null && IsInstanceValid(_capaHUD)) return _capaHUD;
		_capaHUD = GetNodeOrNull<CanvasLayer>("InterfazMenu");
		if (_capaHUD == null)
		{
			_capaHUD = new CanvasLayer();
			_capaHUD.Name = "HUDLayer";
			_capaHUD.Layer = 10;
			AddChild(_capaHUD);
		}
		return _capaHUD;
	}

	// ── NÚMEROS FLOTANTES DE DAÑO ─────────────────────────────────────────
	private void MostrarDañoFlotante(Vector2 posGlobal, int cantidad, bool esCuracion = false)
	{
		var lbl = new Label();
		lbl.Text = esCuracion ? $"+{cantidad}" : $"-{cantidad}";
		lbl.AddThemeColorOverride("font_color", esCuracion ? Colors.LightGreen : Colors.Red);
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.ZIndex       = 300;
		lbl.GlobalPosition = posGlobal + new Vector2(-20, -60);
		AddChild(lbl);

		// Flotar hacia arriba y desvanecerse
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 60f, 0.9f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.9f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	// ── ÍCONOS DE ESTADO (veneno / bloqueo) ───────────────────────────────
	private void ActualizarIconosEstado(Node2D tropa)
	{
		// Limpiar íconos anteriores
		Node iconosViejos = tropa.GetNodeOrNull("IconosEstado");
		iconosViejos?.QueueFree();

		bool envenenado = tropa.HasMeta("envenenado") && ((bool)tropa.GetMeta("envenenado") == true);
		bool bloqueado  = tropa.HasMeta("bloqueado")  && ((bool)tropa.GetMeta("bloqueado")  == true);

		if (!envenenado && !bloqueado) return;

		var contenedor = new HBoxContainer();
		contenedor.Name     = "IconosEstado";
		contenedor.Position = new Vector2(-20, -85);
		tropa.AddChild(contenedor);

		if (envenenado)
		{
			var ico = new Label();
			ico.Text = "☠";
			ico.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.2f));
			ico.AddThemeFontSizeOverride("font_size", 20);
			contenedor.AddChild(ico);

			// El ícono es solo un aviso visual pasajero; el veneno en sí sigue activo el resto de sus turnos.
			GetTree().CreateTimer(3.0).Timeout += () => { if (IsInstanceValid(ico)) ico.QueueFree(); };
		}
		if (bloqueado)
		{
			var ico = new Label();
			ico.Text = "🔒";
			ico.AddThemeFontSizeOverride("font_size", 20);
			contenedor.AddChild(ico);
		}
	}
}
