using Godot;

/// <summary>Offsets de sub-capa dentro del ZIndex de un carril (tier base 10/50/100 por
/// Mod1-2-3, ver TropaInvocada/InvocacionRival/GolemPrime/Enroque). De atrás hacia adelante:
/// Tropa (0) &lt; Tentáculo &lt; Fuego &lt; Humo &lt; Muro. El hueco entre tiers (mínimo 40, entre
/// tier2=50 y tier3=100) es de sobra para estos 5 escalones sin invadir el carril vecino.</summary>
public static class NivelZIndex
{
	public const int Tropa     = 0;
	public const int Tentaculo = 2;
	public const int Fuego     = 4;
	public const int Humo      = 6;
	public const int Muro      = 8;
}

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
	/// <summary>True desde que empieza su derrota (aunque muera por sacrificio, sin llegar a vida 0). Sigue
	/// ocupando su carril hasta que termina la animación: esto la distingue de una tropa viva.</summary>
	public bool EstaMuerta => _estaMuerto;
	protected bool _yaActuo      = false;
	/// <summary>True si ya hizo su acción en este turno (atacar/defender/habilidad).</summary>
	public bool YaActuo => _yaActuo;
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
		// El juego no tiene sistema de elementos visible al jugador: sin círculo de tipo.
		ReproducirIdle();
		CrearAreaClicCuerpo();
	}

	/// <summary>Añade un CollisionShape2D grande ("ClickBody") que cubre TODO el sprite del
	/// personaje, para que la UI de la tropa (vida/defensa/botones) se abra al tocar cualquier parte
	/// del personaje y no solo el punto/colisión chica original. El combate es por carril (no usa
	/// overlap de áreas), así que agrandar el área clicable no afecta la jugabilidad.</summary>
	private void CrearAreaClicCuerpo()
	{
		if (_anim?.SpriteFrames == null) return;
		if (GetNodeOrNull<CollisionShape2D>("ClickBody") != null) return;
		var cuerpo = new CollisionShape2D { Name = "ClickBody" };
		AddChild(cuerpo);
		// Diferido: para una tropa rival recién invocada, en este mismo instante (dentro de
		// _Ready) todavía no se le asignó el grupo "tropas_rival" ni corrió
		// Campo1.AsegurarOrientacionRival() (eso pasa DESPUÉS, ya con el nodo fuera de _Ready) —
		// calcular la posición/tamaño ahora mismo daría el offset del lado equivocado. Diferir
		// hasta que todo eso ya haya corrido deja el área de clic siempre alineada con el punto
		// "oficial" (OffsetCentroColision), sea cual sea el lado.
		CallDeferred(nameof(ActualizarAreaClicCuerpo));
	}

	private void ActualizarAreaClicCuerpo()
	{
		if (!IsInstanceValid(this) || _anim?.SpriteFrames == null) return;
		var cuerpo = GetNodeOrNull<CollisionShape2D>("ClickBody");
		if (cuerpo == null) return;

		string anim = _anim.Animation;
		if (string.IsNullOrEmpty(anim))
		{
			var nombres = _anim.SpriteFrames.GetAnimationNames();
			if (nombres.Length == 0) return;
			anim = nombres[0];
		}
		var tex = _anim.SpriteFrames.GetFrameTexture(anim, 0);
		if (tex == null) return;

		// Generoso a propósito (110% del sprite, con un piso de 140px por lado): que alcance
		// con tocar cualquier parte visible del personaje, sin tener que adivinar el punto
		// exacto ni buscar el círculo del carril detrás suyo.
		Vector2 tam = tex.GetSize() * _anim.Scale.Abs() * 1.1f;
		tam.X = Mathf.Max(tam.X, 140f);
		tam.Y = Mathf.Max(tam.Y, 140f);
		cuerpo.Shape = new RectangleShape2D { Size = tam };
		// Centrado en el mismo punto "oficial" que usa el resto del posicionamiento
		// (OffsetCentroColision, que ya respeta la orientación rival) en vez de _anim.Position en
		// crudo, para que el área de clic no dependa de que cada sprite esté perfectamente
		// centrado en su propio Position.
		cuerpo.Position = OffsetCentroColision();
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
		// En línea, cuando el rival REPRODUCE una jugada, no se aplica daño real ni muerte: solo corren
		// los efectos/animaciones del atacante. La vida/estado autoritativos llegan por el snapshot.
		if (Campo1.SoloVisualOnline) return;
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

	/// <summary>Campo1 lo llama en cada cambio de turno para cancelar cualquier selección de
	/// objetivo por clic que haya quedado pendiente (Caballo, Dama, Maguín — todas las que
	/// activan la habilidad y luego esperan un clic sobre un enemigo para confirmarlo). Por
	/// defecto no hace nada (la mayoría de las cartas no tiene este modo). Sin esto, si el turno
	/// termina antes de completar la selección, la tropa quedaba "escuchando" un clic que ya no
	/// le corresponde a este turno — sin gastar la habilidad (nunca llegó a marcarse usada), pero
	/// con el aro de aviso brillando para siempre.</summary>
	public virtual void CancelarSeleccionPendiente() { }

	/// <summary>Campo1 lo llama al aplicarle el hechizo Bloqueo a esta tropa. Por defecto solo la
	/// deja en "idle" — toda tropa bloqueada queda congelada en reposo, nunca a mitad de una
	/// animación de ataque/habilidad. Las tropas con un estado propio activo y visible en curso
	/// (Parada del Soldado Real, postura de tentáculos del Calamar Gigante) sobreescriben esto
	/// para cancelar ESE estado antes de volver a "idle". Nunca debe revertir efectos que esta
	/// misma tropa ya le haya causado A OTRAS unidades (transformación del Maguín, muros del
	/// Gólem) — esos quedan tal cual aunque a esta tropa la bloqueen después.</summary>
	public virtual void AlSerBloqueado()
	{
		if (!_estaMuerto) ReproducirIdle();
	}

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
	/// usa solo si esta tropa no tiene carril asignado o la zona no existe. Ya viene corregida
	/// por el centro de colisión (ver <see cref="DestinoGlobalParaCentro"/>).</summary>
	protected Vector2 ObtenerPosicionCarrilPropio(Vector2 posDeRespaldo)
	{
		if (!HasMeta("carril")) return posDeRespaldo;
		string carril = (string)GetMeta("carril");
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		return zona != null ? DestinoGlobalParaCentro(zona.GlobalPosition) : posDeRespaldo;
	}

	// ── CENTRADO POR COLISIÓN ────────────────────────────────────────────────
	// "efecto_secundario_slot" (Marker2D presente en TODAS las escenas de tropa) marca el
	// centro real de su CollisionShape2D — no siempre coincide con el origen (0,0) del Area2D,
	// que es lo que arrastraba el desalineamiento visual contra los círculos de los carriles.
	// Todo el posicionamiento "oficial" (invocación, regreso de salto, Enroque, promoción,
	// transmutación) debe pasar por estos helpers en vez de comparar GlobalPosition en crudo.

	/// <summary>Offset local (respecto al origen del Area2D) del centro real de colisión.
	/// Cero si la escena no tiene "efecto_secundario_slot" (nunca debería faltar, pero evita
	/// romper nada si alguna escena todavía no lo tiene).</summary>
	public Vector2 OffsetCentroColision()
	{
		var slot = GetNodeOrNull<Marker2D>("efecto_secundario_slot");
		if (slot == null) return Vector2.Zero;
		// Las tropas rivales se dibujan volteadas (FlipH) pero el Marker2D del slot conserva su
		// offset "de fábrica" (pensado para el lado del jugador) — sin espejar aquí, tropas con
		// offset X grande (ajedrez: Peón, Torre, Caballo, Dama, Alfil) quedan visualmente lejos
		// de su círculo ModRival aunque el cálculo sea "correcto" para el lado jugador.
		return IsInGroup("tropas_rival") ? new Vector2(-slot.Position.X, slot.Position.Y) : slot.Position;
	}

	/// <summary>Posición global real del centro de colisión de esta tropa — el punto que de
	/// verdad se ve alineado con el carril, a diferencia de GlobalPosition (origen del nodo).</summary>
	public Vector2 PosicionCentroColision => GlobalPosition + OffsetCentroColision();

	/// <summary>GlobalPosition que hay que asignarle a esta tropa para que su CENTRO DE
	/// COLISIÓN (no su origen) caiga exactamente sobre <paramref name="posicionCentroDeseada"/>.</summary>
	public Vector2 DestinoGlobalParaCentro(Vector2 posicionCentroDeseada) => posicionCentroDeseada - OffsetCentroColision();

	/// <summary>Coloca esta tropa de modo que su centro de colisión caiga exactamente sobre
	/// <paramref name="posicionDestino"/> — el punto único de posicionamiento "oficial" que debe
	/// usar Campo1 tras cualquier invocación, promoción, Enroque o transmutación, independiente
	/// del tamaño/recorte del sprite de cada tropa.</summary>
	public void ColocarPorCentroColision(Vector2 posicionDestino) => GlobalPosition = DestinoGlobalParaCentro(posicionDestino);

	/// <summary>Fuerza a esta tropa a coincidir exactamente con el Marker2D de un slot/carril,
	/// por su centro de colisión (no el origen crudo del nodo).</summary>
	public void RealinearConCarril(Marker2D slotTarget)
	{
		if (slotTarget == null || !IsInstanceValid(slotTarget)) return;
		ColocarPorCentroColision(slotTarget.GlobalPosition);
	}

	/// <summary>Posición global orientada del "efecto_secundario_slot" — punto de anclaje para
	/// humo, fuego, tentáculos y cualquier otro efecto externo. Ya refleja el lado (jugador ve a
	/// la derecha, rival ve a la izquierda) igual que <see cref="ObtenerSpotOrientado"/>.</summary>
	public Vector2 ObtenerSlotEfectoSecundario()
	{
		var slot = GetNodeOrNull<Marker2D>("efecto_secundario_slot");
		return slot != null ? ObtenerSpotOrientado(slot) : GlobalPosition;
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

	/// <summary>Desvanece una tropa derrotada cuando su animación "derrota" llega al
	/// <paramref name="frameObjetivo"/> (en vez de un timer fijo desde que empieza) — así una
	/// derrota con animación larga no se corta a la mitad, ni una corta se queda esperando de
	/// más antes de dejar libre el carril para la siguiente invocación del rival/CPU. Si la
	/// animación ya venía en curso y pasó ese frame, o si nunca llega a tenerlo (menos frames en
	/// total), se dispara igual — al momento de llamar, o al terminar la animación, respectivamente.
	/// <paramref name="liberarAlTerminar"/> en false solo desvanece (sin QueueFree) — lo usa el
	/// fantasma de Ka-Bar, que recicla el mismo nodo para reaparecer después.</summary>
	public static void DesvanecerTrasFrameDerrota(Node2D tropa, AnimatedSprite2D anim, int frameObjetivo = 17, float duracion = 0.6f, bool liberarAlTerminar = true)
	{
		if (!IsInstanceValid(tropa)) return;
		bool disparado = false;

		void Disparar()
		{
			if (disparado || !IsInstanceValid(tropa)) return;
			disparado = true;
			Tween tw = tropa.CreateTween();
			tw.TweenProperty(tropa, "modulate:a", 0.0f, duracion);
			if (liberarAlTerminar) tw.Finished += () =>
			{
				if (!IsInstanceValid(tropa)) return;
				LiberarCarrilDe(tropa); // terminó su derrota → el carril queda habilitado sí o sí
				tropa.QueueFree();
			};
		}

		if (anim == null) { Disparar(); return; }
		if (((string)anim.Animation).Contains("derrota") && anim.Frame >= frameObjetivo) { Disparar(); return; }

		anim.FrameChanged += () =>
		{
			if (disparado || !IsInstanceValid(anim)) return;
			if (((string)anim.Animation).Contains("derrota") && anim.Frame >= frameObjetivo) Disparar();
		};
		anim.AnimationFinished += () =>
		{
			if (disparado || !IsInstanceValid(anim)) return;
			if (((string)anim.Animation).Contains("derrota")) Disparar();
		};
	}

	/// <summary>Red de seguridad al terminar una derrota (normal o por polvo nuclear): si el carril de
	/// la tropa todavía tiene su marca "Ocupado" apuntando a ELLA, se borra para que el círculo de
	/// invocación vuelva a estar disponible de inmediato (jugador y bot). Si el carril ya se liberó o
	/// ya lo ocupa otra tropa, no toca nada.</summary>
	public static void LiberarCarrilDe(Node2D tropa)
	{
		if (!IsInstanceValid(tropa) || !tropa.IsInsideTree() || !tropa.HasMeta("carril")) return;
		var zona = tropa.GetTree().Root.FindChild((string)tropa.GetMeta("carril"), true, false);
		var ocup = zona?.GetNodeOrNull("Ocupado");
		if (ocup == null || !ocup.HasMeta("tropa_instanciada")) return;
		if (ocup.GetMeta("tropa_instanciada").AsGodotObject() == tropa) ocup.Free();
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

	public void ReproducirIdle()
	{
		if (_estaMuerto || _anim == null) return;
		string idle = NombreAnimacionIdle();
		if (idle != null) _anim.Play(idle);
	}

	// Devuelve una animación de reposo válida para el SpriteFrames de esta tropa.
	// Evita el crash "There is no animation with name 'idle'" cuando el sprite nombra
	// su reposo distinto (p. ej. "idle 1", "reposo"). Si no hay ninguna reconocible,
	// usa la primera animación disponible; null solo si no hay ninguna.
	protected string NombreAnimacionIdle()
	{
		var frames = _anim?.SpriteFrames;
		if (frames == null) return null;

		foreach (string cand in new[] { "idle", "idle 1", "idle1", "idle_fantasma", "reposo", "quieto", "default" })
			if (frames.HasAnimation(cand)) return cand;

		foreach (string n in frames.GetAnimationNames())
		{
			string ln = n.ToLower();
			if (ln.Contains("idle") || ln.Contains("repos")) return n;
		}

		var todas = frames.GetAnimationNames();
		return todas.Length > 0 ? todas[0] : null;
	}

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
		if (!IsInstanceValid(this) || _estaMuerto) return;
		// Últimos 15s antes de la bomba Nuclear: si todavía le queda escudo, el golpe no le rompe la
		// guardia — vuelve a cubrirse. Solo con el escudo en 0 queda desprotegida.
		if (Campo1.GuardiaNuclearActiva && escudoActual > 0 && IsInGroup(Campo1.GrupoGuardiaNuclear))
		{ ReproducirPreDefensa(); return; }
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

	/// <summary>Permite que otra tropa/efecto (p. ej. la curación de Machi) refresque las barras
	/// de esta tropa tras cambiarle la vida desde afuera.</summary>
	public void RefrescarBarras() => ActualizarBarrasUI();

	/// <summary>Reproduce una animación SIN ejecutar lógica de daño (para el multijugador: el rival
	/// remoto solo replica el gesto visual; el daño real llega por la reconciliación del snapshot).
	/// Vuelve a "idle" al terminar.</summary>
	public void ReproducirSoloAnimacion(string anim)
	{
		if (_estaMuerto || _anim == null) return;
		if (_anim.SpriteFrames == null || !_anim.SpriteFrames.HasAnimation(anim)) return;
		_anim.Play(anim);
	}

	/// <summary>Fija los stats de esta tropa desde afuera (reconciliación online) y refresca barras.</summary>
	public void FijarStats(int vida, int vidaMax, int escudo, int escudoMax, int turnoCarta, bool habUsada)
	{
		vidaMaxima = vidaMax; vidaActual = vida;
		escudoMaximo = escudoMax; escudoActual = escudo;
		turnoActualCarta = turnoCarta; habilidadUsada = habUsada;
		if (vidaActual <= 0) { _estaMuerto = true; }
		ActualizarBarrasUI();
	}
}
