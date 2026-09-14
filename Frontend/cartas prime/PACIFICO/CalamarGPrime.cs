using Godot;
using System.Collections.Generic;

/// <summary>Calamar Gigante — daño de ataque en frame 1. Habilidad (turno 3): atrapa con
/// tentáculos a las tropas enemigas de sus DOS carriles ADYACENTES (nunca la de su propio
/// carril/frente), regenera su escudo al 100% y queda en postura de defensa permanente hasta
/// que ese escudo vuelve a llegar a 0.</summary>
public partial class CalamarGPrime : TropaBase
{
	public override string Tipo => Tipos.SOMBRA;
	protected override int TurnoDesbloqueoHabilidad => 4;

	private const string RUTA_TENTACULO = "res://efectos/tentaculos_habilidad.tscn";

	private Node2D _objetivo;
	private bool _posturaPermanente = false;
	private readonly List<TentaculoHabilidadPrime> _tentaculosActivos = new();

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 400; escudoActual = escudoMaximo = 380; puntosAtaque = 370; }
		base._Ready();
		_anim.FrameChanged      += OnFrameChanged;
		_anim.AnimationFinished += OnAnimationFinished;
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: daño en frame 1 ────────────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo  = true;
			_objetivo = BuscarObjetivoEnCarril();
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		if ((string)_anim.Animation == "ataque" && _anim.Frame == 1)
		{
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				_objetivo.Call("RecibirDaño", puntosAtaque);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, puntosAtaque);
			}
		}
	}

	// Mientras dure la postura permanente, tras cada bloqueo ("defensa") vuelve a "pre defensa"
	// en vez de a "idle" — el Calamar se queda plantado en guardia, no un solo golpe.
	private void OnAnimationFinished()
	{
		if (_posturaPermanente && !_estaMuerto && (string)_anim.Animation == "defensa")
			_anim.Play("pre defensa");
	}

	// ── HABILIDAD: BLOQUEO POR TENTÁCULOS ─────────────────────────────────────
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || !HasMeta("carril")) return;
		if (!ResourceLoader.Exists(RUTA_TENTACULO)) return;

		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
		var carrilesObjetivo = new List<string> { "1", "2", "3" };
		carrilesObjetivo.Remove(miCarril);

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		bool esCalamarJugador = IsInGroup("tropas_jugador");

		var objetivos = new List<Node2D>();
		foreach (string carril in carrilesObjetivo)
		{
			foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
			{
				// El Tanque es la única carta inmune a los tentáculos: puede seguir siendo
				// alcanzado por el ataque directo del Maguín, pero nunca queda atrapado.
				if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril") || e is TanqueCartoonPrime) continue;
				string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
				if (c != carril) continue;
				int v = 0; try { v = (int)e.Get("vidaActual"); } catch { }
				if (v > 0) { objetivos.Add(e); break; }
			}
		}

		if (objetivos.Count == 0) return; // sin nada que atrapar, no gasta la habilidad

		habilidadUsada       = true;
		_yaActuo              = true;
		_posturaPermanente    = true;
		escudoActual          = escudoMaximo; // regenera al 100%
		// Mientras dura la postura, Campo1.EstaBlockeada también reconoce esta meta — el propio
		// Calamar pierde su menú de acciones (ATACAR/DEFENSA/HABILIDAD) igual que una tropa
		// atrapada, tanto si lo controla el jugador como la IA.
		SetMeta("en_postura_permanente", true);
		ActualizarBarrasUI();
		DestelloHabilidad();
		_anim.Play("pre defensa");

		var escenaTentaculo = GD.Load<PackedScene>(RUTA_TENTACULO);
		foreach (Node2D obj in objetivos)
		{
			var tentaculo = (Area2D)escenaTentaculo.Instantiate();
			GetTree().Root.AddChild(tentaculo);

			Vector2 posAncla = obj is TropaBase objTB ? objTB.ObtenerSlotEfectoSecundario() : obj.GlobalPosition;
			tentaculo.GlobalPosition = posAncla;
			// Por delante de la tropa (para cubrirla), pero por detrás de un muro si lo hubiera
			// en ese carril (el muro usa tier + NivelZIndex.Muro, más alto que Tentaculo).
			tentaculo.ZIndex = obj.ZIndex + NivelZIndex.Tentaculo;

			if (tentaculo is TentaculoHabilidadPrime tp)
			{
				tp.objetivo = obj;
				// Referencia directa: si esta tropa muere por cualquier vía, Campo1 la libera
				// de inmediato en vez de esperar al próximo tick de daño (cada 10s).
				obj.SetMeta("tentaculo_activo", tp);
				tp.AplicarOrientacion(esCalamarJugador);
				_tentaculosActivos.Add(tp);
			}
		}
	}

	// ── RECIBIR DAÑO: mientras dura la postura permanente, siempre bloquea ────
	public override void RecibirDaño(int cantidad)
	{
		if (_estaMuerto) return;

		if (!_posturaPermanente) { base.RecibirDaño(cantidad); return; }

		EfectoGolpe();
		cantidad = (int)(cantidad * 0.5f);
		_anim.Play("defensa");

		if (escudoActual > 0)
		{
			int escudoAntes = escudoActual;
			// Anti-overkill: el exceso nunca traspasa a la vida mientras haya escudo.
			if (cantidad <= escudoActual) { escudoActual -= cantidad; cantidad = 0; }
			else                          { cantidad = 0; escudoActual = 0; }
			ActualizarBarrasUI();

			if (escudoAntes > 0 && escudoActual == 0) LiberarTentaculos();
		}

		if (cantidad > 0) vidaActual -= cantidad;
		ActualizarBarrasUI();

		if (vidaActual <= 0)
		{
			LiberarTentaculos();
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("EjecutarMuerteTropaSacrificada", this);
		}
	}

	// Escudo roto (o el Calamar murió): termina la habilidad y libera a las tropas atrapadas.
	private void LiberarTentaculos()
	{
		if (!_posturaPermanente) return; // ya se liberó — evita relanzar "idle" sobre "derrota"
		_posturaPermanente = false;
		RemoveMeta("en_postura_permanente");
		foreach (var t in _tentaculosActivos)
			if (IsInstanceValid(t)) t.Liberar();
		_tentaculosActivos.Clear();

		if (!_estaMuerto) _anim.Play("idle");
	}

	// Red de seguridad: EjecutarMuerteTropaSacrificada (usada tanto por combate normal como por
	// el sacrificio manual del jugador) llama ReproducirDerrota() ANTES de nada — sin este
	// override, si el Calamar muere por la vía del sacrificio (que no pasa por RecibirDaño), sus
	// tentáculos quedarían atrapando enemigos para siempre, sin nadie que los libere.
	public override void ReproducirDerrota()
	{
		LiberarTentaculos();
		base.ReproducirDerrota();
	}
}
