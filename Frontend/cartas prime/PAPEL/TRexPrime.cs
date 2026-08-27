using Godot;
using System;

/// <summary>
/// T-Rex Prime — Tropa pesada de naturaleza (850 HP / 370 ATK / Sin Escudo).
/// Ataque: Muerde e inflige daño en el Frame 2 de la animación.
/// UI: Sin botón de defensa ni barra de escudo. Ignora poses defensivas.
/// Habilidad: Muestra destello amarillo por 1s, ejecuta animación de ataque y en Frame 2 
/// inflige 400 de daño verdadero a la tropa enemiga de su carril (respeta muro).
/// </summary>
public partial class TRexPrime : TropaBase
{
	public override string Tipo => Tipos.NATURALEZA;
	protected override int TurnoDesbloqueoHabilidad => 4;

	// ── CONSTANTES Y CONFIGURACIÓN ───────────────────────────────────────────
	private const int FRAME_GOLPE      = 2;   // Frame exacto de la animación de mordisco
	private const int DAÑO_VERDADERO   = 400; // Daño de la habilidad: ignora defensa/escudo

	// ── CONTROL DE OBJETIVO Y ESTADO DE HABILIDAD ────────────────────────────
	private Node2D _objetivoPendiente;
	private bool   _esAtaqueHabilidad = false;

	public override void _Ready()
	{
		// Estadísticas: 850 HP / 370 ATK (0 Escudo)
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 850; 
			escudoActual = escudoMaximo = 0; 
			puntosAtaque = 370; 
		}
		base._Ready();

		// Ocultar e inactivar componentes de defensa/escudo en la UI
		var barraEscudo = GetNodeOrNull<ProgressBar>("StatsTropa/BarraEscudo");
		if (barraEscudo != null) barraEscudo.Visible = false;

		var btnDefensa = GetNodeOrNull<Control>("UI/BtnDefensa") ?? GetNodeOrNull<Control>("BtnDefensa");
		if (btnDefensa != null) btnDefensa.Visible = false;

		// Conectar eventos de animación
		if (_anim != null)
		{
			_anim.Connect(AnimatedSprite2D.SignalName.FrameChanged, Callable.From(OnFrameChanged));
			_anim.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(OnAnimationFinished));
		}
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	public override bool TieneHabilidadEspecial() => true;
	public override bool MostrarBotonDefensa()   => false; // Sin botón de defensa
	public override bool TienePosturaDefensiva()  => false; // Sin mecánica defensiva

	// ── CONTROL DE ACCIONES ───────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		switch (accion)
		{
			case "atacar":
				_yaActuo = true;
				_esAtaqueHabilidad = false;
				_objetivoPendiente = BuscarObjetivoEnCarril();
				IniciarAnimacionAtaque();
				break;

			case "preparar_defensa":
			case "defender":
				// Sin postura defensiva: se mantiene en idle consumiendo el turno
				_yaActuo = true;
				ReproducirIdle();
				break;

			case "usar_habilidad":
				UsarHabilidadPropia();
				break;

			default:
				base.EjecutarAccion(accion);
				break;
		}
	}

	private void IniciarAnimacionAtaque()
	{
		if (_anim == null) return;
		string nombreAnim = _anim.SpriteFrames.HasAnimation("ataque") ? "ataque" : "atacar";
		_anim.Play(nombreAnim);
	}

	// ── EVENTO: FRAME CHANGED (IMPACTO EN FRAME 2) ───────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;

		string animActual = _anim.Animation.ToString();
		if (animActual != "ataque" && animActual != "atacar") return;

		// Inflige daño únicamente al llegar al Frame 2 de impacto
		if (_anim.Frame == FRAME_GOLPE)
		{
			if (_esAtaqueHabilidad)
			{
				AplicarDañoVerdaderoHabilidad();
			}
			else
			{
				AplicarDañoAObjetivo();
			}
		}
	}

	private void AplicarDañoAObjetivo()
	{
		if (_objetivoPendiente == null || !IsInstanceValid(_objetivoPendiente)) return;

		if (_objetivoPendiente.HasMethod("RecibirDañoDe"))
		{
			_objetivoPendiente.Call("RecibirDañoDe", puntosAtaque, this);
		}
		else if (_objetivoPendiente.HasMethod("RecibirDaño"))
		{
			_objetivoPendiente.Call("RecibirDaño", puntosAtaque);
		}
		var campoAtaque = GetTree().Root.FindChild("Campo1", true, false);
		if (campoAtaque != null) campoAtaque.Call("RegistrarDañoTropa", this, puntosAtaque);

		_objetivoPendiente = null;
	}

	private void AplicarDañoVerdaderoHabilidad()
	{
		if (_objetivoPendiente == null || !IsInstanceValid(_objetivoPendiente) || _objetivoPendiente == this) 
		{
			_esAtaqueHabilidad = false;
			return;
		}

		bool esMuro = _objetivoPendiente.IsInGroup("muros_jugador") || _objetivoPendiente.IsInGroup("muros_rival");
		var campoHabilidad = GetTree().Root.FindChild("Campo1", true, false);
		if (campoHabilidad != null) campoHabilidad.Call("RegistrarDañoTropa", this, DAÑO_VERDADERO);

		if (esMuro)
		{
			// El muro recibe daño a su durabilidad
			_objetivoPendiente.Call("RecibirDaño", DAÑO_VERDADERO);
		}
		else
		{
			// Daño verdadero directo a la vida
			int vidaAntes = 0; try { vidaAntes = (int)_objetivoPendiente.Get("vidaActual"); } catch { }
			int vidaNueva = Mathf.Max(0, vidaAntes - DAÑO_VERDADERO);
			try { _objetivoPendiente.Set("vidaActual", vidaNueva); } catch { }

			if (vidaNueva <= 0)
			{
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", _objetivoPendiente);
			}
			else if (_objetivoPendiente.HasMethod("EjecutarAccion"))
			{
				_objetivoPendiente.Call("EjecutarAccion", "recibir_daño");
			}

			Tween te = _objetivoPendiente.CreateTween();
			te.TweenProperty(_objetivoPendiente, "modulate", new Color(1f, 0.5f, 0.5f), 0.2f);
			te.TweenProperty(_objetivoPendiente, "modulate", Colors.White, 0.4f);
		}

		_objetivoPendiente = null;
		_esAtaqueHabilidad = false;
	}

	// ── EVENTO: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;

		string animActual = _anim.Animation.ToString();
		if (animActual == "ataque" || animActual == "atacar" || animActual == "daño")
		{
			_esAtaqueHabilidad = false;
			if (!_estaMuerto) ReproducirIdle();
		}
	}

	// ── HABILIDAD: MORDISCO DEVASTADOR (CON DESTELLO AMARILLO Y ATAQUE SINCRO) ──
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;

		Node2D objetivo = BuscarObjetivoEnCarril(); // respeta muro
		if (objetivo == null || objetivo == this) return;

		_yaActuo = true;
		habilidadUsada = true;
		_esAtaqueHabilidad = true;
		_objetivoPendiente = objetivo;

		// Muestra el destello/aura amarilla durante 1 segundo
		DestelloHabilidad();

		// Inicia animación de ataque (el golpe de 400 se asesta en OnFrameChanged cuando Frame == 2)
		IniciarAnimacionAtaque();
	}
}
