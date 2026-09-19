using Godot;

/// <summary>
/// Soldado Real — Habilidad: Parada (Parry).
/// </summary>
public partial class SoldadoRealPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 3;

	private bool   _enParry            = false;
	private int    _parryTicksRestantes = 0;
	private Node2D _objetivo;
	private bool   _esContraataque     = false;
	private Tween  _tweenParry; // Guardamos la referencia para detener la animación al recibir golpe

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 250; escudoActual = escudoMaximo = 300; puntosAtaque = 170; }
		base._Ready();

		if (_anim != null)
		{
			_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
			_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));
		}
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => true;

	// ── ACCIONES ───────────────────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		if (accion == "atacar")
		{
			LimpiarEstadoParry();
			_yaActuo = true;
			_objetivo = BuscarObjetivoEnCarril();
			_anim.Play("ataque");
			return;
		}
		base.EjecutarAccion(accion);
	}

	// ── HABILIDAD: PARADA (PARRY) ──────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _enParry) return;
		habilidadUsada       = true;
		_yaActuo             = true;
		_enParry             = true;
		// 4 rondas completas (8 cambios de turno: cada ronda = mi turno + turno rival) — se
		// desactiva antes si lo golpean mientras está en parry (ver RecibirDaño), o al agotarse
		// estas 4 rondas sin que lo toquen.
		_parryTicksRestantes = 8;

		_anim.Play("pre defensa");

		// --- AURA AMARILLA SUAVE Y PULSANTE ---
		IniciarAuraParrySuave();
	}

	// ── RECIBIR DAÑO: el parry SOLO responde al golpe directo de una tropa enemiga ──
	// Veneno, hechizos, efectos (fuego, tentáculos, misiles...) NO lo disparan: esos le pegan normal
	// y la Parada sigue activa. Antes cualquier daño lo hacía contraatacar, y como no sabía quién le
	// había pegado, le devolvía el golpe a la tropa que estuviera en su carril aunque no fuera ella.
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		if (_enParry)
		{
			Node2D atacante = BuscarAtacanteEnCarril();
			if (atacante != null) Contraatacar(BuscarMuroEnCarrilDe(atacante) ?? atacante);
			else                  RecibirDañoDeEfectoEnParry(cantidad);
			return;
		}

		base.RecibirDaño(cantidad);
	}

	/// <summary>Golpe con atacante CONOCIDO (Dama, Arfil, Caballo y T-Rex llaman a esto). Las piezas de
	/// ajedrez pueden llegar saltando DESDE OTRO CARRIL, así que el contraataque va directo contra la
	/// pieza que le pegó (que está parada a su lado), no contra quien esté en su carril.</summary>
	public void RecibirDañoDe(int cantidad, Node2D atacante)
	{
		if (_estaMuerto) return;

		bool esEnemigo = atacante != null && IsInstanceValid(atacante) && atacante is TropaBase
			&& atacante.IsInGroup("tropas_jugador") != IsInGroup("tropas_jugador");
		if (!_enParry || !esEnemigo) { RecibirDaño(cantidad); return; }

		// Si viene de MI carril y el rival tiene un muro ahí, el contraataque pega primero al muro
		// (igual que antes); si llegó saltando desde otro carril, le pega directo a la pieza.
		bool mismoCarril = atacante.HasMeta("carril") && HasMeta("carril")
			&& NumeroCarril((string)atacante.GetMeta("carril")) == NumeroCarril((string)GetMeta("carril"));
		Contraatacar(mismoCarril ? (BuscarMuroEnCarrilDe(atacante) ?? atacante) : atacante);
	}

	private static string NumeroCarril(string carril) =>
		carril.ToLower().Replace("modrival", "").Replace("mod", "").Trim();

	private void Contraatacar(Node2D objetivo)
	{
		LimpiarEstadoParry();
		_esContraataque = true;
		_objetivo       = objetivo;
		_anim.Play("ataque");
	}

	// Daño de un EFECTO (no de una tropa) mientras está en Parada: le baja la vida normal, sin
	// contraatacar y sin salir de la guardia (no pasa por la pre-defensa genérica de TropaBase, que
	// lo sacaba de la pose de parry con la animación "defensa").
	private void RecibirDañoDeEfectoEnParry(int cantidad)
	{
		if (Campo1.SoloVisualOnline) return;
		EfectoGolpe();
		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();

		if (vidaActual <= 0)
		{
			LimpiarEstadoParry();
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
	}

	/// <summary>La bomba Nuclear lo agarra en Parada: se cubre (animación "defensa"), NO contraataca a
	/// nadie, el escudo queda en 0 y vuelve a "idle" (lo hace OnAnimationFinished al terminar
	/// "defensa"). No pierde vida. Devuelve false si no estaba en Parada (recibe el daño normal).</summary>
	public bool CubrirseDeNuclear()
	{
		if (_estaMuerto || !_enParry) return false;
		LimpiarEstadoParry();
		escudoActual = 0;
		ActualizarBarrasUI();
		_anim.Play("defensa");
		return true;
	}

	// El hechizo Bloqueo cancela la Parada igual que si lo golpearan mientras la tenía activa
	// (sin el contraataque): apaga el aura dorada y vuelve a "idle" antes de quedar congelado/oscuro.
	public override void AlSerBloqueado()
	{
		if (_enParry) LimpiarEstadoParry();
		if (!_estaMuerto) _anim.Play("idle");
	}

	// ── MÉTODOS AUXILIARES PARA EL AURA Y LIMPIEZA ────────────────────────────
	private void IniciarAuraParrySuave()
	{
		_tweenParry?.Kill(); // Cancelar cualquier tween previo
		_tweenParry = CreateTween().SetLoops(); // Bucle infinito mientras dure el Parry

		// Tinte base muy suave (Ligeramente cálido/dorado: R=1.2, G=1.1, B=0.8)
		Color colorNormal = new Color(1.0f, 1.0f, 1.0f, 1.0f);
		Color colorBrilloSuave = new Color(1.25f, 1.15f, 0.75f, 1.0f); 

		_tweenParry.TweenProperty(this, "modulate", colorBrilloSuave, 0.6f);
		_tweenParry.TweenProperty(this, "modulate", colorNormal, 0.6f);
	}

	private void LimpiarEstadoParry()
	{
		_enParry = false;
		_parryTicksRestantes = 0;
		_esContraataque = false;
		
		// Detener el pulso del aura y restaurar el color original al instante
		_tweenParry?.Kill();
		Modulate = Colors.White;
	}

	// ── EVENTO: FRAME CHANGED ──────────────────────────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		if (anim == "ataque" && _anim.Frame == 1)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				int danioFinal = _esContraataque ? 300 : puntosAtaque;
				_objetivo.Call("RecibirDaño", danioFinal);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, danioFinal);
			}
		}

		if (anim == "pre defensa" && _anim.Frame == _anim.SpriteFrames.GetFrameCount("pre defensa") - 1)
		{
			if (_enParry) _anim.Frame = 0;
		}
	}

	// ── EVENTO: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;
		string anim = _anim.Animation.ToString();

		if (anim == "ataque" || anim == "daño" || anim == "defensa")
		{
			_esContraataque = false;
			if (!_estaMuerto) _anim.Play("idle");
		}
	}

	// ── TICK: EXPIRAR PARADA EN 4 RONDAS (8 CAMBIOS DE TURNO) ──────────────────
	public override void TickHabilidad()
	{
		if (!_enParry) return;
		_parryTicksRestantes--;
		if (_parryTicksRestantes <= 0)
		{
			LimpiarEstadoParry();
			if (!_estaMuerto) _anim.Play("idle");
		}
	}

	// ── HELPERS DE BÚSQUEDA EN CARRIL ──────────────────────────────────────────
	private Node2D BuscarAtacanteEnCarril()
	{
		if (!HasMeta("carril")) return null;
		string grupo    = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != miCarril) continue;
			var animSprite = e.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (animSprite != null && animSprite.Animation.ToString().Contains("ataque")) return e;
		}
		return null;
	}

	/// <summary>Si el carril de <paramref name="refNode"/> tiene un muro enemigo, lo devuelve
	/// para que el contraataque de Parry golpee el muro en vez de saltárselo.</summary>
	private Node2D BuscarMuroEnCarrilDe(Node2D refNode)
	{
		if (refNode == null || !IsInstanceValid(refNode) || !refNode.HasMeta("carril")) return null;
		string grupoMuro = IsInGroup("tropas_jugador") ? "muros_rival" : "muros_jugador";
		string carril = ((string)refNode.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
		foreach (Node n in GetTree().GetNodesInGroup(grupoMuro))
		{
			if (!(n is Node2D m) || !IsInstanceValid(m) || !m.HasMeta("carril")) continue;
			string c = ((string)m.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == carril) return m;
		}
		return null;
	}
}
