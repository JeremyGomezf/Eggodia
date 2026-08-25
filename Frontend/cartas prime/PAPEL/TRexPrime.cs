using Godot;
using System;
using System.Collections.Generic;

public partial class TRexPrime : TropaBase
{
	public override string Tipo => Tipos.NATURALEZA;

	private bool _rugidoActivo;
	private int  _turnosRugido;
	private List<(Node2D tropa, int ataqueOrig)> _afectados = new();

	private Node2D _objetivoPendiente;
	private const int FRAME_GOLPE = 2; // 🎯 Frame exacto del mordisco/daño

	public override void _Ready()
	{
		// Estadísticas: 750 HP / 370 ATK (Sin Escudo)
		if (vidaMaxima == 0) 
		{ 
			vidaActual = vidaMaxima = 850; 
			escudoActual = escudoMaximo = 0; 
			puntosAtaque = 370; 
		}
		base._Ready();

		// Escuchar eventos del AnimatedSprite base (_anim viene de TropaBase)
		if (_anim != null)
		{
			_anim.FrameChanged += OnFrameChanged;
			_anim.AnimationFinished += OnAnimationFinished;
		}
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;
	// Sin escudo ni defensa: ocultar botón defensa completamente
	public override bool MostrarBotonDefensa() => false;

	// ── CONTROL DE ACCIONES Y ATAQUE ──────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (_estaMuerto) return;

		if (accion == "atacar")
		{
			_yaActuo = true;
			_objetivoPendiente = BuscarObjetivoEnCarril();
			IniciarAnimacionAtaque();
		}
		else
		{
			base.EjecutarAccion(accion);
		}
	}

	private void IniciarAnimacionAtaque()
	{
		if (_anim == null) return;

		// Busca si la animación se llama "ataque" o "atacar"
		string nombreAnim = _anim.SpriteFrames.HasAnimation("ataque") ? "ataque" : "atacar";
		_anim.Play(nombreAnim);
	}

	private void OnFrameChanged()
	{
		if (_anim == null) return;

		string animActual = _anim.Animation;
		if (animActual != "ataque" && animActual != "atacar") return;

		// 💥 Aplica el daño ÚNICAMENTE cuando el sprite llega al Frame 2
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

		_objetivoPendiente = null; // Evita infligir daño doble en el mismo turno
	}

	private void OnAnimationFinished()
	{
		string animActual = _anim.Animation;
		if (animActual == "ataque" || animActual == "atacar")
		{
			if (!_estaMuerto) ReproducirIdle();
		}
	}

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

	// ── HABILIDAD RUGIDO ─────────────────────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || _estaMuerto) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";

		Tween tw = CreateTween();
		tw.TweenProperty(this, "scale", Scale * 1.3f, 0.15f).SetTrans(Tween.TransitionType.Back);
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
		_rugidoActivo  = true;
		_turnosRugido  = 2;
		habilidadUsada = true;
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
