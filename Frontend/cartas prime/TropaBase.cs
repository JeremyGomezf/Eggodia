using Godot;

/// <summary>
/// Clase base abstracta para todas las tropas del juego.
/// Aplica: Herencia, Encapsulamiento, Polimorfismo, Abstracción (POO).
/// Cada carta prime hereda de aquí y solo define sus stats y habilidad única.
/// </summary>
public abstract partial class TropaBase : Area2D
{
	// ── STATS (exportados — visibles en el Inspector de Godot) ────────────
	[Export] public int vidaActual;
	[Export] public int vidaMaxima;
	[Export] public int escudoActual;
	[Export] public int escudoMaximo;
	[Export] public int puntosAtaque;

	// ── ESTADO ────────────────────────────────────────────────────────────
	public  bool habilidadUsada  = false;
	protected bool _estaMuerto   = false;
	protected bool _yaActuo      = false;
	private  Tween _tweenGolpe;

	/// <summary>Turno propio de esta tropa: 0 al entrar al tablero (el turno de invocación
	/// nunca cuenta como progreso de habilidad). Solo lo incrementa Campo1, al inicio del turno
	/// activo de su propio dueño y únicamente tras haber sobrevivido el turno del rival, vía
	/// AvanzarTurnoTropa(). Ninguna tropa puede usar su habilidad hasta llegar a su propio
	/// TurnoDesbloqueoHabilidad, sin excepciones (ni siquiera las que desbloquean en el turno 1).</summary>
	public int turnoActualCarta = 0;

	/// <summary>Bloqueo anti-spam: mientras es true, esta tropa ignora clics por completo
	/// (usado durante animaciones críticas como el Enroque de la Torre).</summary>
	public bool estaProcesandoHabilidad = false;

	// ── REFERENCIAS ───────────────────────────────────────────────────────
	protected AnimatedSprite2D _anim;
	protected Control          _contenedorStats;

	// ══════════════════════════════════════════════════════════════════════
	// MÉTODOS DE PLANTILLA (Template Method Pattern)
	// Las subclases sobreescriben solo lo que cambia.
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Las subclases llaman a base._Ready() después de configurar sus stats.
	/// Los valores del Inspector (.tscn) ya están aplicados cuando esto corre.
	/// </summary>
	public override void _Ready()
	{
		InputPickable = true;
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_contenedorStats = GetNodeOrNull<Control>("StatsTropa");
		if (_contenedorStats != null) _contenedorStats.Visible = false;
		CrearBadgeTipo();
		ReproducirIdle();
	}

	/// <summary>Si false, Campo1 oculta/deshabilita el botón de defensa para esta tropa.</summary>
	public virtual bool TienePosturaDefensiva() => true;

	/// <summary>Si false, Campo1 oculta completamente el botón de habilidad (no sólo lo deshabilita).</summary>
	public virtual bool MostrarBotonHabilidad() => true;

	/// <summary>Si false, Campo1 oculta completamente el botón de defensa y la barra de escudo.</summary>
	public virtual bool MostrarBotonDefensa() => true;

	/// <summary>Turno propio mínimo (turnoActualCarta) para poder usar la habilidad. 1 = disponible
	/// desde que entra al tablero (comportamiento por defecto). Las subclases con habilidad
	/// diferida sobreescriben este valor.</summary>
	protected virtual int TurnoDesbloqueoHabilidad => 1;

	/// <summary>Si true, el botón de habilidad se muestra en gris/bloqueado aunque no esté usada.
	/// Por defecto, bloqueada mientras la tropa no llegue a su turno propio de desbloqueo.</summary>
	public virtual bool HabilidadBloqueada() => turnoActualCarta < TurnoDesbloqueoHabilidad;

	/// <summary>Invocado exclusivamente por Campo1 al inicio del turno del dueño de esta tropa
	/// (tras sobrevivir la fase contraria). Nunca se llama más de una vez por turno propio.</summary>
	public void AvanzarTurnoTropa() => turnoActualCarta++;

	/// <summary>Polimorfismo: cada carta reacciona al clic o toque igual, pero puede extenderse.</summary>
	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (estaProcesandoHabilidad) return;

		bool presionado = (@event is InputEventMouseButton mb  && mb.Pressed  && mb.ButtonIndex == MouseButton.Left)
		               || (@event is InputEventScreenTouch st && st.Pressed);
		if (!presionado) return;

		if (IsInGroup("tropas_rival")) { MostrarBarras(true); return; }
		MostrarBarras(true);
		if (_estaMuerto || _yaActuo) return;
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		if (campo != null) campo.MostrarMenuTropa(this);
	}

	// ── COMPORTAMIENTO COMPARTIDO ──────────────────────────────────────────

	public void SetActivo(bool estado)
	{
		_yaActuo = !estado;
		if (estado) MostrarBarras(false);
	}

	public virtual void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;
		EfectoGolpe();

		if (_anim.Animation == "pre defensa")
		{
			EjecutarAccion("defender");
			cantidad = (int)(cantidad * 0.5f);
			if (escudoActual > 0)
			{
				if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
				else { cantidad = 0; escudoActual = 0; }  // anti-overkill: el exceso se absorbe
			}
		}

		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();

		if (vidaActual <= 0)
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
		else if (_anim.Animation != "defensa")
		{
			EjecutarAccion("recibir_daño");
		}
	}

	/// <summary>
	/// Si es true, la tropa aplica su propio daño de ataque internamente durante
	/// EjecutarAccion("atacar") (p. ej. multi-hit sincronizado a fotogramas).
	/// El llamador (Campo1 / CampoPruebas) NO debe aplicar "puntosAtaque" directo
	/// al objetivo en ese caso, para no duplicar el daño.
	/// </summary>
	public virtual bool AutogestionaDañoAtaque() => false;

	public virtual bool TieneHabilidadEspecial() => true;

	/// <summary>Tipo elemental de la tropa. Subclases lo sobreescriben. Ver Tipos.cs para matchups.</summary>
	public virtual string Tipo => Tipos.NEUTRO;

	protected void CrearBadgeTipo()
	{
		if (Tipo == Tipos.NEUTRO) return;
		var badge = new Panel();
		badge.Name = "BadgeTipo";
		badge.CustomMinimumSize = new Vector2(22, 22);
		badge.Size = new Vector2(22, 22);
		badge.Position = new Vector2(-42, -58);
		badge.ZIndex = 40;
		var sb = new StyleBoxFlat();
		sb.BgColor = Tipos.Color(Tipo);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 11;
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0, 0, 0, 0.8f);
		badge.AddThemeStyleboxOverride("panel", sb);
		var lbl = new Label();
		lbl.Text = Tipos.Inicial(Tipo);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		lbl.AddThemeConstantOverride("shadow_offset_x", 1);
		lbl.AddThemeConstantOverride("shadow_offset_y", 1);
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		lbl.AnchorRight = 1; lbl.AnchorBottom = 1;
		badge.AddChild(lbl);
		AddChild(badge);
	}

	protected void EfectoGolpe()
	{
		if (_estaMuerto) return;
		_tweenGolpe?.Kill();
		Color antes = Modulate;
		_tweenGolpe = CreateTween();
		_tweenGolpe.TweenProperty(this, "modulate", new Color(3f, 0.4f, 0.4f, 1f), 0.05f);
		_tweenGolpe.TweenProperty(this, "modulate", antes, 0.15f);
	}

	/// <summary>Polimorfismo: las subclases pueden extender este método.</summary>
	public virtual void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;
		switch (accion)
		{
			case "atacar":           _yaActuo = true; ReproducirAtaque();     break;
			case "preparar_defensa": _yaActuo = true; ReproducirPreDefensa(); break;
			case "recibir_daño":     ReproducirDaño();     break;
			case "defender":         ReproducirDefensa();  break;
			case "usar_habilidad":   UsarHabilidadPropia(); break;
		}
	}

	/// <summary>
	/// Habilidad especial. Las subclases sobreescriben para implementar la suya.
	/// Por defecto no hace nada (cartas sin habilidad).
	/// </summary>
	protected virtual void UsarHabilidadPropia() { }

	/// <summary>
	/// Llamado cada turno por Campo1 para habilidades con duración.
	/// TorrePrime lo sobreescribe para desactivar su forma gigante.
	/// </summary>
	public virtual void TickHabilidad() { }

	/// <summary>Punto de aterrizaje para un salto/vuelo de ataque (Caballo/Arfil/Dama): el
	/// SpotInvocacion del objetivo — ya orientado hacia el lado de quien ataca, derivado de su
	/// propia caja de colisión — en vez de un offset fijo en píxeles. Si el objetivo no tiene
	/// SpotInvocacion (p. ej. un muro), cae en su GlobalPosition tal cual.</summary>
	protected static Vector2 ObtenerDestinoAtaque(Node2D objetivo)
	{
		if (objetivo is TropaBase objTB)
		{
			var spot = objTB.GetNodeOrNull<Marker2D>("SpotInvocacion");
			if (spot != null) return objTB.ObtenerSpotOrientado(spot);
		}
		return objetivo.GlobalPosition;
	}

	/// <summary>Posición canónica del carril propio (GlobalPosition del Marker2D de la zona),
	/// para que el regreso de un salto/vuelo aterrice siempre exacto en su slot, sin depender de
	/// una posición cacheada que pudiera arrastrar desvíos. <paramref name="posDeRespaldo"/> se
	/// usa solo si esta tropa no tiene carril asignado o la zona no existe.</summary>
	protected Vector2 ObtenerPosicionCarrilPropio(Vector2 posDeRespaldo)
	{
		if (!HasMeta("carril")) return posDeRespaldo;
		string carril = (string)GetMeta("carril");
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		return zona != null ? zona.GlobalPosition : posDeRespaldo;
	}

	/// <summary>Fuerza a esta tropa a coincidir exactamente con el Marker2D de un slot/carril,
	/// usando siempre GlobalPosition (nunca Position, que es relativa al padre y no sirve si la
	/// tropa y el slot no comparten el mismo Node2D padre). Es el punto único de "recalce" que
	/// puede llamar Campo1 tras cualquier invocación, promoción o Enroque para garantizar que el
	/// root de la tropa quede exactamente sobre el círculo rojo del slot. No corrige por sí solo
	/// un desfase visual causado por el offset propio del AnimatedSprite2D dentro de la escena de
	/// la tropa (eso requiere ajustar la posición del sprite hijo en su .tscn); solo garantiza que
	/// el ORIGEN (0,0) de la tropa esté exactamente donde está el slot.</summary>
	public void RealinearConCarril(Marker2D slotTarget)
	{
		if (slotTarget == null || !IsInstanceValid(slotTarget)) return;
		GlobalPosition = slotTarget.GlobalPosition;
	}

	/// <summary>Desvanece un nodo (Modulate:a de 1 a 0 en <paramref name="duracion"/> segundos) y
	/// lo libera al terminar — para que efectos como explosiones no desaparezcan de golpe.</summary>
	protected static void DesvanecerYLiberar(Node2D nodo, float duracion = 0.2f)
	{
		if (!IsInstanceValid(nodo)) return;
		Tween tw = nodo.CreateTween();
		tw.TweenProperty(nodo, "modulate:a", 0.0f, duracion);
		tw.Finished += () => { if (IsInstanceValid(nodo)) nodo.QueueFree(); };
	}

	/// <summary>Igual que DesvanecerYLiberar, pero dispara el desvanecimiento apenas la animación
	/// de <paramref name="anim"/> alcanza su antepenúltimo frame (~80% de progreso) en vez de
	/// esperar a que termine — el sprite sigue animando mientras se desvanece, sin congelarse en
	/// el último fotograma antes de desaparecer.</summary>
	protected static void DesvanecerAlAntepenultimoFrame(Node2D nodo, AnimatedSprite2D anim, float duracion = 0.18f)
	{
		if (anim == null) { DesvanecerYLiberar(nodo, duracion); return; }
		bool disparado = false;
		anim.FrameChanged += () =>
		{
			if (disparado || !IsInstanceValid(nodo)) return;
			int total = anim.SpriteFrames != null ? anim.SpriteFrames.GetFrameCount(anim.Animation) : 0;
			if (total >= 3 && anim.Frame >= total - 3)
			{
				disparado = true;
				DesvanecerYLiberar(nodo, duracion);
			}
		};
	}

	/// <summary>Posición global de un Marker2D "spot" de lanzamiento (SpotFuego, SpotCañon,
	/// SpotInvocacion, etc.), reflejando su offset X si esta tropa es del rival. Los spots se
	/// definen en la escena asumiendo la orientación del jugador; el volteo rival normal (FlipH)
	/// solo cambia la textura, no la posición de los Marker2D hijos, así que hay que reflejarlo
	/// a mano. Si esta tropa ya usa Scale.X negativo para orientarse (convención de
	/// campo_de_pruebas), el propio transform del nodo ya refleja a sus hijos — en ese caso se
	/// usa GlobalPosition tal cual.</summary>
	public Vector2 ObtenerSpotOrientado(Marker2D spot)
	{
		if (spot == null) return GlobalPosition;
		if (Scale.X < 0) return spot.GlobalPosition;
		bool esJugador = IsInGroup("tropas_jugador");
		float offsetX = esJugador ? spot.Position.X : -spot.Position.X;
		return GlobalPosition + new Vector2(offsetX, spot.Position.Y);
	}

	/// <summary>Destello amarillo momentáneo al activar una habilidad (no ataque): sube y vuelve
	/// a blanco en ~1s. Pensado para reemplazar tweens de "rebote" de escala.</summary>
	protected void DestelloHabilidad()
	{
		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(1.6f, 1.5f, 0.4f), 0.35f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.65f);
	}

	/// <summary>
	/// Busca el objetivo enemigo en el mismo carril. Si un muro del Gólem ocupa ese carril,
	/// se devuelve el muro en su lugar salvo que <paramref name="ignorarMuro"/> sea true
	/// (ataques que pasan por encima del muro: granadas, saltos de ajedrez, fantasma de Kabar).
	/// </summary>
	protected Node2D BuscarObjetivoEnCarril(bool ignorarMuro = false)
	{
		if (!HasMeta("carril")) return null;
		bool esJugador      = IsInGroup("tropas_jugador");
		string grupoTropas  = esJugador ? "tropas_rival" : "tropas_jugador";
		string grupoMuros   = esJugador ? "muros_rival"  : "muros_jugador";
		string miCarril     = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		// Diagnóstico: el grupo (tropas_jugador/tropas_rival) y el prefijo del carril propio
		// (Mod.../ModRival...) deben coincidir siempre. Si no coinciden, algo en la invocación
		// de esta tropa la etiquetó mal — se avisa aquí para poder rastrear el origen.
		bool carrilDiceRival = ((string)GetMeta("carril")).ToLower().Contains("modrival");
		if (esJugador == carrilDiceRival)
			GD.PrintErr($"[TropaBase] {Name} ({GetType().Name}): el grupo dice bando=" +
				$"{(esJugador ? "jugador" : "rival")} pero su carril ('{GetMeta("carril")}') es de bando " +
				$"{(carrilDiceRival ? "rival" : "jugador")}. Revisa dónde se invocó esta tropa.");

		if (!ignorarMuro)
		{
			foreach (Node n in GetTree().GetNodesInGroup(grupoMuros))
			{
				if (!(n is Node2D m) || !IsInstanceValid(m) || m == this || !m.HasMeta("carril")) continue;
				string cm = ((string)m.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
				if (cm == miCarril) return m;
			}
		}

		Node2D mejor = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupoTropas))
		{
			// "e == this" es una segunda barrera además del filtro por grupo: si por cualquier
			// motivo esta tropa terminara perteneciendo también al grupo contrario, jamás debe
			// poder encontrarse a sí misma como objetivo.
			if (!(n is Node2D e) || !IsInstanceValid(e) || e == this || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != miCarril) continue;
			int v = 0; try { v = (int)e.Get("vidaActual"); } catch { }
			if (v > 0 && v < min) { min = v; mejor = e; }
		}

		return mejor;
	}

	// ── ANIMACIONES (encapsuladas — las subclases no las duplican) ────────

	public void ReproducirIdle() { if (!_estaMuerto) _anim.Play("idle"); }

	public async void ReproducirAtaque()
	{
		if (_estaMuerto) return;
		_anim.Play("ataque");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public void ReproducirPreDefensa() { if (!_estaMuerto) _anim.Play("pre defensa"); }

	public async void ReproducirDefensa()
	{
		if (_estaMuerto) return;
		_anim.Play("defensa");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public async void ReproducirDaño()
	{
		if (_estaMuerto) return;
		_anim.Play("daño");
		await ToSignal(_anim, "animation_finished");
		ReproducirIdle();
	}

	public virtual void ReproducirDerrota() { _estaMuerto = true; _anim.Play("derrota"); }

	// ── UI (encapsulada) ──────────────────────────────────────────────────

	private int _tokenBarras = 0;

	protected void MostrarBarras(bool mostrar)
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = mostrar; ActualizarBarrasUI(); }
		if (!mostrar) return;

		// Auto-ocultado a los 3s de inactividad (mismo comportamiento para jugador y rival).
		int miToken = ++_tokenBarras;
		GetTree().CreateTimer(3.0).Timeout += () =>
		{
			if (!IsInstanceValid(this) || miToken != _tokenBarras) return;
			if (_contenedorStats != null) _contenedorStats.Visible = false;
		};
	}

	protected void ActualizarBarrasUI()
	{
		var bv = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraVida");
		var be = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraEscudo");
		if (bv != null) bv.Value = vidaMaxima  > 0 ? (float)vidaActual  / vidaMaxima  * 100 : 0;
		if (be != null) be.Value = escudoMaximo > 0 ? (float)escudoActual / escudoMaximo * 100 : 0;
	}
}
