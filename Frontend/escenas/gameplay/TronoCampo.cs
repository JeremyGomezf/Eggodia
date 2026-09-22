using Godot;
using System;

public partial class TronoCampo : StaticBody2D
{
	private Node2D huevoInstancia;
	/// <summary>El personaje-huevo posado en este trono (null si CargarHuevo todavía no corrió). Lo usa
	/// la intro cinemática (Campo1.Intro.cs) para la caída desde el cielo.</summary>
	public Node2D Huevo => huevoInstancia;

	// Los 8 PNG de Tronos/ son 570x427. Escala agrandada a pedido (antes 0.27 se veía chico en el
	// tablero) — ajustar visualmente en editor si hace falta afinar más.
	private static readonly Vector2 ESCALA_TRONO_NUEVO = new Vector2(0.4f, 0.4f);

	/// <summary>Cambia la textura del trono (Sprite2D "TronoAlpha") y su escala para los PNG
	/// nuevos de res://imagenes/Tronos/. "espejar" voltea el trono horizontalmente — el trono del
	/// jugador mira hacia la derecha (hacia el centro) y el del rival debe mirar hacia la
	/// izquierda (también hacia el centro), igual que el resto de los elementos rivales.</summary>
	public void CambiarTrono(Texture2D nuevaTextura, bool espejar = false)
	{
		var sprite = GetNodeOrNull<Sprite2D>("TronoAlpha");
		if (sprite == null || nuevaTextura == null) return;
		sprite.Texture = nuevaTextura;
		sprite.Scale = ESCALA_TRONO_NUEVO;
		sprite.FlipH = espejar;
	}

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
			else
			{
				// Sin AnimatedSprite2D "idle" (la mayoría de los huevos son Sprite2D estático):
				// respiración sutil por Tween para que todos los huevos en batalla se sientan vivos.
				Vector2 escalaBase = huevoInstancia.Scale;
				Tween idleBreath = huevoInstancia.CreateTween().SetLoops();
				idleBreath.TweenProperty(huevoInstancia, "scale:y", escalaBase.Y * 1.03f, 1.1f).SetTrans(Tween.TransitionType.Sine);
				idleBreath.TweenProperty(huevoInstancia, "scale:y", escalaBase.Y, 1.1f).SetTrans(Tween.TransitionType.Sine);
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
