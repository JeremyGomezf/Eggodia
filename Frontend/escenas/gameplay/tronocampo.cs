using Godot;
using System;

public partial class tronocampo : StaticBody2D
{
	private Node2D huevoInstancia;

	public void CargarHuevo(PackedScene huevoEscena, bool voltear)
	{
		Marker2D marker = GetNodeOrNull<Marker2D>("PosicionHuevo");
		if (marker != null && huevoEscena != null)
		{
			huevoInstancia = (Node2D)huevoEscena.Instantiate();
			marker.AddChild(huevoInstancia);
			huevoInstancia.Position = Vector2.Zero;
			if (voltear) huevoInstancia.Scale = new Vector2(-1, 1);
			// Reproducir animación idle si el personaje la tiene
			var anim = huevoInstancia.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			if (anim != null && anim.SpriteFrames != null && anim.SpriteFrames.HasAnimation("idle"))
			{
				anim.Play("idle");
			}
		}
	}

	public void EfectoMuerteMinecraft()
	{
		if (huevoInstancia == null) return;

		// 1. Ponemos el huevo de color rojo (Minecraft Style)
		huevoInstancia.Modulate = new Color(1, 0, 0); // Rojo puro

		// 2. Animación de caída y desaparición
		Tween tween = GetTree().CreateTween();
		tween.SetParallel(true);
		
		// Cae hacia un lado
		tween.TweenProperty(huevoInstancia, "rotation", Mathf.DegToRad(90), 0.5f);
		tween.TweenProperty(huevoInstancia, "position", new Vector2(0, 50), 0.5f);
		
		// Se desvanece
		tween.Chain().TweenProperty(huevoInstancia, "modulate:a", 0, 0.5f);
		
		// Al terminar, se borra
		tween.Finished += () => huevoInstancia.QueueFree();
	}
}
