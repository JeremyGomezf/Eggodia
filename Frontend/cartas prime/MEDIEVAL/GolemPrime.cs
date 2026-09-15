using Godot;

/// <summary>
/// Gólem — daño dividido en dos oleadas: frame 2 (50%) + frame 4 (50%).
/// Si el frame 2 agota el escudo del rival, el frame 4 golpea directo a la vida.
/// Habilidad: invoca muros de piedra frente a los aliados de los otros dos carriles.
/// </summary>
public partial class GolemPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 4;

	private const string RUTA_MURO = "res://efectos/muro_golem.tscn";

	private Node2D _objetivo;
	private bool   _golpeF2Disparado = false;
	private bool   _golpeF4Disparado = false;

	private PackedScene _escenaMuro;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 450; escudoActual = escudoMaximo = 500; puntosAtaque = 330; }
		base._Ready();
		_anim.FrameChanged += OnFrameChanged;

		if (ResourceLoader.Exists(RUTA_MURO)) _escenaMuro = GD.Load<PackedScene>(RUTA_MURO);
	}

	// ── CAPACIDADES ────────────────────────────────────────────────────────────
	public override bool AutogestionaDañoAtaque() => true;

	// ── ATAQUE: daño en frames 2 y 4 ──────────────────────────────────────────
	public override void EjecutarAccion(string accion)
	{
		if (accion == "atacar")
		{
			_yaActuo            = true;
			_objetivo           = BuscarObjetivoEnCarril();
			_golpeF2Disparado   = false;
			_golpeF4Disparado   = false;
			ReproducirAtaque();
			return;
		}
		base.EjecutarAccion(accion);
	}

	private void OnFrameChanged()
	{
		string anim = (string)_anim.Animation;
		if (anim != "ataque") return;

		int hit = puntosAtaque / 2;

		if (_anim.Frame == 2 && !_golpeF2Disparado)
		{
			_golpeF2Disparado = true;
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				_objetivo.Call("RecibirDaño", hit);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, hit);
			}
		}

		if (_anim.Frame == 4 && !_golpeF4Disparado)
		{
			_golpeF4Disparado = true;
			if (_objetivo != null && IsInstanceValid(_objetivo))
			{
				_objetivo.Call("RecibirDaño", hit);
				var campo = GetTree().Root.FindChild("Campo1", true, false);
				if (campo != null) campo.Call("RegistrarDañoTropa", this, hit);
			}
		}
	}

	// ── HABILIDAD: INVOCAR MUROS FRENTE A LOS ALIADOS DE LOS OTROS CARRILES ──
	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || !HasMeta("carril")) return;
		habilidadUsada = true;
		_yaActuo        = true;

		DestelloHabilidad();

		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		// Bloquea el tablero durante los 3s de carga: la IA no debe emitir otra acción
		// mientras los muros del Gólem todavía no han aparecido.
		var campo = GetTree().Root.FindChild("Campo1", true, false);
		campo?.Call("IniciarBloqueoTablero");

		GetTree().CreateTimer(3.0).Timeout += () =>
		{
			if (IsInstanceValid(this))
			{
				foreach (string otroCarril in new[] { "1", "2", "3" })
				{
					if (otroCarril == miCarril) continue;
					InvocarMuroParaCarril(otroCarril);
				}
			}
			campo?.Call("FinalizarBloqueoTablero");
		};
	}

	private void InvocarMuroParaCarril(string carrilNormalizado)
	{
		if (_escenaMuro == null) return;

		bool esJugador      = IsInGroup("tropas_jugador");
		string grupoAliado  = esJugador ? "tropas_jugador" : "tropas_rival";
		string grupoMuro    = esJugador ? "muros_jugador"  : "muros_rival";

		// Ya hay un muro en ese carril: no duplicar.
		foreach (Node n in GetTree().GetNodesInGroup(grupoMuro))
		{
			if (!(n is Node2D m) || !IsInstanceValid(m) || !m.HasMeta("carril")) continue;
			string cm = ((string)m.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (cm == carrilNormalizado) return;
		}

		Node2D aliado = null;
		foreach (Node n in GetTree().GetNodesInGroup(grupoAliado))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == this || !a.HasMeta("carril")) continue;
			string c = ((string)a.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (c != carrilNormalizado) continue;
			int v = 0; try { v = (int)a.Get("vidaActual"); } catch { }
			if (v > 0) { aliado = a; break; }
		}
		if (aliado == null) return;

		Node2D muro = (Node2D)_escenaMuro.Instantiate();
		GetTree().Root.AddChild(muro);

		// Posición fija por tropa (SpotInvocacion), no calculada por caja de colisión.
		var aliadoTB   = aliado as TropaBase;
		var spotAliado = aliado.GetNodeOrNull<Marker2D>("SpotInvocacion");
		muro.GlobalPosition = (aliadoTB != null && spotAliado != null)
			? aliadoTB.ObtenerSpotOrientado(spotAliado)
			: aliado.GlobalPosition + new Vector2(esJugador ? 80f : -80f, 0f); // fallback si faltara el marker

		// Z-Index por módulo (mismo tier base que las tropas, Mod1 < Mod2 < Mod3) más el
		// escalón de "Muro" — la capa más al frente del carril, por delante de tentáculos,
		// fuegos y humos que pudieran coincidir sobre la tropa que protege.
		int tierMuro = carrilNormalizado switch { "3" => 100, "2" => 50, _ => 10 };
		muro.ZIndex = tierMuro + NivelZIndex.Muro;
		muro.SetMeta("carril", aliado.GetMeta("carril"));
		muro.AddToGroup(grupoMuro);

		if (!esJugador)
		{
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null && campo.HasMethod("AsegurarOrientacionRival"))
			{
				campo.Call("AsegurarOrientacionRival", muro);
			}
			else
			{
				var animMuro = muro.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
				if (animMuro != null) animMuro.FlipH = true;
			}
		}
	}
}
