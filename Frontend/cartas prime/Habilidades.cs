using Godot;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
// HABILIDADES — compatible con TorrePrime, CaballoPrime, CalamarGPrime, GolemPrime
// Agrega cada nodo hijo a la escena de la tropa correspondiente con el nombre exacto.
// ═══════════════════════════════════════════════════════════════════════════

// ── TORRE: nodo hijo llamado "HabilidadTorre" ─────────────────────────────
// Efecto: se agranda x1.6, dobla daño y escudo por 2 turnos
public partial class HabilidadTorre : Node
{
	private const float ESCALA  = 1.6f;
	private const int   TURNOS  = 2;

	private Node2D  tropa;
	private int     ataqueOrig, escudoMaxOrig;
	private Vector2 escalaOrig;
	public  bool    habilidadActiva  = false;
	private int     turnosRestantes  = 0;

	public override void _Ready() { tropa = GetParent<Node2D>(); }

	public void ActivarHabilidad()
	{
		if (tropa == null || habilidadActiva) return;
		bool usada; try { usada = (bool)tropa.Get("habilidadUsada"); } catch { usada = false; }
		if (usada) return;

		ataqueOrig    = GetI("puntosAtaque");
		escudoMaxOrig = GetI("escudoMaximo");
		escalaOrig    = tropa.Scale;

		SetI("puntosAtaque", ataqueOrig * 2);
		SetI("escudoMaximo", escudoMaxOrig * 2);
		SetI("escudoActual", Mathf.Min(GetI("escudoActual") * 2, escudoMaxOrig * 2));

		Tween tw = tropa.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(tropa, "scale", escalaOrig * ESCALA, 0.45f);
		Tween tw2 = tropa.CreateTween();
		tw2.TweenProperty(tropa, "modulate", new Color(1.4f,1.1f,0.2f), 0.3f);
		tw2.TweenProperty(tropa, "modulate", Colors.White, 0.5f);

		habilidadActiva = true;
		turnosRestantes = TURNOS;
		try { tropa.Set("habilidadUsada", true); } catch { }
	}

	public void OnNuevoTurno()
	{
		if (!habilidadActiva) return;
		turnosRestantes--;
		if (turnosRestantes <= 0) Desactivar();
	}

	private void Desactivar()
	{
		if (!IsInstanceValid(tropa)) return;
		SetI("puntosAtaque", ataqueOrig);
		SetI("escudoMaximo", escudoMaxOrig);
		if (GetI("escudoActual") > escudoMaxOrig) SetI("escudoActual", escudoMaxOrig);
		Tween tw = tropa.CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(tropa, "scale", escalaOrig, 0.4f);
		habilidadActiva = false;
	}

	private int  GetI(string p) { try { return (int)tropa.Get(p); }  catch { return 0; } }
	private void SetI(string p, int v) { try { tropa.Set(p, v); }    catch { } }
}


// ── CALAMAR: nodo hijo llamado "HabilidadCalamar" ─────────────────────────
// Efecto: bloquea las 2 tropas enemigas más cercanas (radio 280px) por 1 turno
public partial class HabilidadCalamar : Node
{
	private const float RADIO  = 280f;
	private const int   MAX    = 2;
	private const int   TURNOS = 1;

	private Node2D tropa;
	public override void _Ready() { tropa = GetParent<Node2D>(); }

	public void ActivarHabilidad()
	{
		if (tropa == null) return;
		bool usada; try { usada = (bool)tropa.Get("habilidadUsada"); } catch { usada = false; }
		if (usada) return;

		string grupo = tropa.IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		var lista = new List<(Node2D n, float d)>();

		foreach (Node n in tropa.GetTree().GetNodesInGroup(grupo))
			if (n is Node2D e && IsInstanceValid(e))
			{
				float d = tropa.GlobalPosition.DistanceTo(e.GlobalPosition);
				if (d <= RADIO) lista.Add((e, d));
			}

		lista.Sort((a,b) => a.d.CompareTo(b.d));

		int count = 0;
		foreach (var (e, _) in lista)
		{
			if (count >= MAX) break;
			// Aplicar bloqueo via Meta (Campo1 lo procesará cada turno)
			e.SetMeta("bloqueado",     true);
			e.SetMeta("turnosBloqueo", TURNOS);
			// Visual: tinte oscuro
			Tween tw = e.CreateTween();
			tw.TweenProperty(e, "modulate", new Color(0.2f, 0.1f, 0.35f, 0.9f), 0.2f);
			// Vibración
			Vector2 orig = e.Position;
			Tween sh = e.CreateTween();
			sh.TweenProperty(e,"position", orig + new Vector2(6,0),  0.05f);
			sh.TweenProperty(e,"position", orig - new Vector2(6,0),  0.05f);
			sh.TweenProperty(e,"position", orig,                     0.05f);
			count++;
		}

		// Visual del calamar
		Tween self = tropa.CreateTween();
		self.TweenProperty(tropa,"scale", tropa.Scale*1.15f, 0.2f);
		self.TweenProperty(tropa,"scale", tropa.Scale,       0.2f);

		if (count > 0) try { tropa.Set("habilidadUsada", true); } catch { }
	}
}


// ── CABALLO: nodo hijo llamado "HabilidadCaballo" ─────────────────────────
// Efecto: ataca los 2 carriles adyacentes en patrón de L con x2 daño
public partial class HabilidadCaballo : Node
{
	private Node2D tropa;
	private Node   campo;

	public override void _Ready()
	{
		tropa = GetParent<Node2D>();
		campo = tropa?.GetTree().Root.FindChild("Campo1", true, false);
	}

	public void ActivarHabilidad()
	{
		if (tropa == null) return;
		bool usada; try { usada = (bool)tropa.Get("habilidadUsada"); } catch { usada = false; }
		if (usada) return;

		string grupo  = tropa.IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)tropa.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");

		var carrilesL = new List<string>();
		switch (miCarril)
		{
			case "1": carrilesL.Add("2"); carrilesL.Add("3"); break;
			case "2": carrilesL.Add("1"); carrilesL.Add("3"); break;
			case "3": carrilesL.Add("1"); carrilesL.Add("2"); break;
		}

		int daño = 0; try { daño = (int)tropa.Get("puntosAtaque") * 2; } catch { }

		// Salto visual
		Tween tw = tropa.CreateTween();
		tw.TweenProperty(tropa,"position:y", tropa.Position.Y - 45f, 0.2f);
		tw.TweenProperty(tropa,"position:y", tropa.Position.Y,       0.2f);

		foreach (Node n in tropa.GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string id = ((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
			if (!carrilesL.Contains(id)) continue;

			// Llamar RecibirDaño — la tropa maneja el escudo internamente
			if (e.HasMethod("RecibirDaño")) e.Call("RecibirDaño", daño);
		}

		try { tropa.Set("habilidadUsada", true); } catch { }
	}
}


// ── GOLEM: nodo hijo llamado "HabilidadGolem" ─────────────────────────────
// Efecto: da +200 de escudo a los 2 aliados con menor % de escudo restante
public partial class HabilidadGolem : Node
{
	private const int ESCUDO = 200;
	private const int ROCAS  = 2;

	private Node2D tropa;
	public override void _Ready() { tropa = GetParent<Node2D>(); }

	public void ActivarHabilidad()
	{
		if (tropa == null) return;
		bool usada; try { usada = (bool)tropa.Get("habilidadUsada"); } catch { usada = false; }
		if (usada) return;

		string grupo = tropa.IsInGroup("tropas_jugador") ? "tropas_jugador" : "tropas_rival";
		var aliados  = new List<(Node2D n, float pct)>();

		foreach (Node n in tropa.GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == tropa) continue;
			int esc = 0, escMax = 1;
			try { esc    = (int)a.Get("escudoActual"); } catch { }
			try { escMax = (int)a.Get("escudoMaximo"); if (escMax <= 0) escMax = 1; } catch { }
			aliados.Add((a, (float)esc / escMax));
		}
		aliados.Sort((a,b) => a.pct.CompareTo(b.pct));

		// Golpe de tierra visual
		Tween tw = tropa.CreateTween();
		tw.TweenProperty(tropa,"modulate", new Color(0.7f,0.7f,0.9f), 0.15f);
		tw.TweenProperty(tropa,"modulate", Colors.White, 0.3f);
		Tween sc = tropa.CreateTween();
		sc.TweenProperty(tropa,"scale", tropa.Scale * new Vector2(1.2f,0.8f), 0.15f);
		sc.TweenProperty(tropa,"scale", tropa.Scale, 0.25f);

		int rocas = 0;
		foreach (var (a, _) in aliados)
		{
			if (rocas >= ROCAS) break;
			int escActual = 0, escMax = 0;
			try { escActual = (int)a.Get("escudoActual"); } catch { }
			try { escMax    = (int)a.Get("escudoMaximo"); } catch { }
			int nuevo = escActual + ESCUDO;
			try { a.Set("escudoActual", nuevo); }                               catch { }
			try { a.Set("escudoMaximo", Mathf.Max(escMax, nuevo)); }            catch { }

			// Visual del aliado
			Tween ta = a.CreateTween();
			ta.TweenProperty(a,"modulate", new Color(1.2f,1f,0.4f), 0.2f);
			ta.TweenProperty(a,"modulate", Colors.White, 0.4f);
			Tween sa = a.CreateTween();
			sa.TweenProperty(a,"scale", a.Scale * 1.1f, 0.15f);
			sa.TweenProperty(a,"scale", a.Scale, 0.2f);
			rocas++;
		}

		try { tropa.Set("habilidadUsada", true); } catch { }
	}
}
