using Godot;

/// <summary>
/// Soldado Real — Habilidad: Parada (Parry).
/// </summary>
public partial class SoldadoRealPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	private bool   _enParry            = false;
	private int    _parryTicksRestantes = 0;
	private Node2D _objetivo;
	private bool   _esContraataque     = false;
	private Tween  _tweenParry; // Guardamos la referencia para detener la animación al recibir golpe

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 250; escudoActual = escudoMaximo = 300; puntosAtaque = 150; }
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
		_parryTicksRestantes = 2;

		_anim.Play("pre defensa");

		// --- AURA AMARILLA SUAVE Y PULSANTE ---
		IniciarAuraParrySuave();
	}

	// ── RECIBIR DAÑO: PARRY ABSOLUTO ──────────────────────────────────────────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		if (_enParry)
		{
			LimpiarEstadoParry();
			_esContraataque = true;

			Node2D atacanteOCarril = BuscarAtacanteEnCarril() ?? BuscarObjetivoEnCarril();
			_objetivo = BuscarMuroEnCarrilDe(atacanteOCarril) ?? atacanteOCarril;
			_anim.Play("ataque");
			return;
		}

		base.RecibirDaño(cantidad);
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

	// ── TICK: EXPIRAR PARADA EN 2 TURNOS ───────────────────────────────────────
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
