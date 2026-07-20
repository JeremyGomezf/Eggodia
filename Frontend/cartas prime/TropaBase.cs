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
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_contenedorStats = GetNodeOrNull<Control>("StatsTropa");
		if (_contenedorStats != null) _contenedorStats.Visible = false;
		ReproducirIdle();
	}

	/// <summary>Polimorfismo: cada carta reacciona al clic igual, pero puede extenderse.</summary>
	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (IsInGroup("tropas_rival")) { MostrarBarras(true); return; }
			MostrarBarras(true);
			if (_estaMuerto || _yaActuo) return;
			var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
			if (campo != null) campo.MostrarMenuTropa(this);
		}
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

		if (_anim.Animation == "pre defensa")
		{
			EjecutarAccion("defender");
			cantidad = (int)(cantidad * 0.5f);
			if (escudoActual > 0)
			{
				if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
				else { cantidad -= escudoActual; escudoActual = 0; }
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

	public void ReproducirDerrota() { _estaMuerto = true; _anim.Play("derrota"); }

	// ── UI (encapsulada) ──────────────────────────────────────────────────

	protected void MostrarBarras(bool mostrar)
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = mostrar; ActualizarBarrasUI(); }
	}

	protected void ActualizarBarrasUI()
	{
		var bv = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraVida");
		var be = _contenedorStats?.GetNodeOrNull<ProgressBar>("BarraEscudo");
		if (bv != null) bv.Value = vidaMaxima  > 0 ? (float)vidaActual  / vidaMaxima  * 100 : 0;
		if (be != null) be.Value = escudoMaximo > 0 ? (float)escudoActual / escudoMaximo * 100 : 0;
	}
}
