using Godot;
using System;

public partial class Carta : Control 
{
	// --- VARIABLES DE ESTADO ---
	public int Daño = 0;
	public bool EstaEnMano = true; 
	public PackedScene EscenaTropa;
	public string NombreSpot = ""; // Guarda en qué asiento está sentada

	private Vector2 _posicionOriginal;
	private float _rotacionOriginal;
	private int _zIndexOriginal;

	[Export] private Vector2 _escalaHover = new Vector2(1.3f, 1.3f); 
	[Export] private float _tiempoAnimacion = 0.15f; 

	public override void _Ready()
	{
		// Punto de control para que crezca desde abajo
		PivotOffset = new Vector2(Size.X / 2, Size.Y);
		
		// IMPORTANTE: Asegurar que la carta detecte el mouse al inicio
		MouseFilter = MouseFilterEnum.Stop;
	}

	public void GuardarEstadoOriginal()
	{
		_posicionOriginal = Position;
		_rotacionOriginal = Rotation;
		_zIndexOriginal = ZIndex;
	}

	// --- ANIMACIÓN AL ENTRAR EL MOUSE ---
	public void _on_mouse_entered()
	{
		if (!EstaEnMano) return; 

		Tween tween = CreateTween().SetParallel(true);
		
		// Se agranda y se endereza
		tween.TweenProperty(this, "scale", _escalaHover, _tiempoAnimacion).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "rotation", 0f, _tiempoAnimacion);
		
		// Salto hacia arriba para leerla bien
		Vector2 posicionElevada = new Vector2(_posicionOriginal.X, _posicionOriginal.Y - 150f);
		tween.TweenProperty(this, "position", posicionElevada, _tiempoAnimacion).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
		
		ZIndex = 100; 
	}

	// --- ANIMACIÓN AL SALIR EL MOUSE ---
	public void _on_mouse_exited()
	{
		if (!EstaEnMano) return; 

		Tween tween = CreateTween().SetParallel(true);
		tween.TweenProperty(this, "scale", new Vector2(1f, 1f), _tiempoAnimacion);
		tween.TweenProperty(this, "rotation", _rotacionOriginal, _tiempoAnimacion);
		tween.TweenProperty(this, "position", _posicionOriginal, _tiempoAnimacion);

		tween.Finished += () => { if (EstaEnMano) ZIndex = _zIndexOriginal; };
	}
}
