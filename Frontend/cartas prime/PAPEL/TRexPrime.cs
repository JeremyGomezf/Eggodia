using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// T-Rex Prime — Tropa pesada de naturaleza (850 HP / 370 ATK / Sin Escudo).
/// Ataque: Muerde e inflige daño en el Frame 2 de la animación.
/// UI: Sin botón de defensa ni barra de escudo. Ignora poses defensivas.
/// Habilidad "Rugido": Reduce el ataque de todos los enemigos al 70% durante 2 turnos.
/// </summary>
public partial class TRexPrime : TropaBase
{
	public override string Tipo => Tipos.NATURALEZA;

	// ── CONSTANTES Y CONFIGURACIÓN ───────────────────────────────────────────
	private const int FRAME_GOLPE = 2; // Frame exacto de la animación de mordisco

	// ── ESTADO HABILIDAD (RUGIDO) ─────────────────────────────────────────────
	private bool _rugidoActivo;
	private int  _turnosRugido;
	private List<(Node2D tropa, int ataqueOrig)> _afectados = new();

	// ── CONTROL DE OBJETIVO ──────────────────────────────────────────────────
	private Node2D _objetivoPendiente;

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

	// ── EVENTO: FRAME CHANGED (DAÑO EN FRAME 2) ──────────────────────────────
	private void OnFrameChanged()
	{
		if (_anim == null) return;

		string animActual = _anim.Animation.ToString();
		if (animActual != "ataque" && animActual != "atacar") return;

		// Inflige daño únicamente al llegar al Frame 2 de impacto
		if (_anim.Frame == FRAME_GOLPE)
		{
			AplicarDañoAObjetivo();
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

		_objetivoPendiente = null; // Evita daño duplicado en la misma secuencia
	}

	// ── EVENTO: FIN DE ANIMACIÓN ───────────────────────────────────────────────
	private void OnAnimationFinished()
	{
		if (_anim == null) return;

		string animActual = _anim.Animation.ToString();
		if (animActual == "ataque" || animActual == "atacar" || animActual == "daño")
		{
			if (!_estaMuerto) ReproducirIdle();
		}
	}

	// ── BUSCAR ENEMIGO EN CARRIL ──────────────────────────────────────────────
	private Node2D BuscarObjetivoEnCarril()
	{
		if (!HasMeta("carril")) return null;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c == miCarril) return e;
		}
		return null;
	}

	// ── HABILIDAD: RUGIDO DEBILITADOR ─────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;

		_yaActuo = true;
		habilidadUsada = true;
		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";

		// Animación visual de rugido (escalado elástico sutil)
		Tween tw = CreateTween();
		tw.TweenProperty(this, "scale", Scale * 1.25f, 0.15f).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(this, "scale", Scale, 0.2f);

		_afectados.Clear();
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e)) continue;
			int atk = 0;
			try { atk = (int)e.Get("puntosAtaque"); } catch { continue; }
			
			_afectados.Add((e, atk));
			try { e.Set("puntosAtaque", (int)(atk * 0.7f)); } catch { }

			Tween te = e.CreateTween();
			te.TweenProperty(e, "modulate", new Color(1f, 0.5f, 0.5f), 0.2f);
		}

		_rugidoActivo = true;
		_turnosRugido = 2;
	}

	public override void TickHabilidad()
	{
		if (!_rugidoActivo) return;

		_turnosRugido--;
		if (_turnosRugido > 0) return;

		foreach (var (tropa, ataqueOrig) in _afectados)
		{
			if (!IsInstanceValid(tropa)) continue;
			try { tropa.Set("puntosAtaque", ataqueOrig); } catch { }
			tropa.Modulate = Colors.White;
		}

		_afectados.Clear();
		_rugidoActivo = false;
	}
}
