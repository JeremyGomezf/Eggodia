using Godot;

/// <summary>
/// Libera nodos de efectos que viven fuera del árbol de la escena de combate
/// (instanciados con GetTree().Root.AddChild) y que por eso no se destruyen solos
/// al recargar/cambiar de escena: muros del Gólem y efectos de fuego del Dragón.
/// </summary>
public static class LimpiezaEfectos
{
	private static readonly string[] GRUPOS = { "muros_jugador", "muros_rival", "efectos_dragon_root" };

	public static void LimpiarEfectosDeCampo()
	{
		if (Engine.GetMainLoop() is not SceneTree tree) return;
		foreach (string grupo in GRUPOS)
			foreach (Node n in tree.GetNodesInGroup(grupo))
				if (GodotObject.IsInstanceValid(n)) n.QueueFree();
	}
}
