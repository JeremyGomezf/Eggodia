using Godot;
using System.Collections.Generic;

/// <summary>Caballo — habilidad: ataca los 2 carriles adyacentes en patrón L con doble daño.</summary>
public partial class CaballoPrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 230; escudoActual = escudoMaximo = 250; puntosAtaque = 200; }
		base._Ready();
	}

	// Método público para refrescar UI desde Campo1 tras curación
	public void RefrescarUI()
	{
		if (_contenedorStats != null) { _contenedorStats.Visible = true; ActualizarBarrasUI(); }
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		if (!HasMeta("carril")) { GD.PrintErr("CaballoPrime: falta meta 'carril'"); return; }

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		var carrilesL = new List<string>();
		switch (miCarril)
		{
			case "1": carrilesL.Add("2"); carrilesL.Add("3"); break;
			case "2": carrilesL.Add("1"); carrilesL.Add("3"); break;
			case "3": carrilesL.Add("1"); carrilesL.Add("2"); break;
			default: GD.PrintErr($"CaballoPrime: carril desconocido '{miCarril}'"); return;
		}

		int dañoDoble = puntosAtaque * 2;

		Tween tw = CreateTween();
		tw.TweenProperty(this, "position:y", Position.Y - 45f, 0.15f);
		tw.TweenProperty(this, "position:y", Position.Y,       0.15f);

		var objetivos = new List<Node2D>();
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			string idEne = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
			if (carrilesL.Contains(idEne)) objetivos.Add(e);
		}

		foreach (Node2D objetivo in objetivos)
		{
			if (!IsInstanceValid(objetivo)) continue;
			objetivo.Call("RecibirDaño", dañoDoble);
		}

		habilidadUsada = true;
		_yaActuo       = true;
	}
}
