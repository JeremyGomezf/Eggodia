using Godot;
using System;

public partial class Carta : Control 
{
	public bool EstaEnMano = true; 
	public bool EstaArrastrando = false;
	
	public PackedScene EscenaTropa;
	public string NombreSpot = ""; 
	public int IdCarta;

	private Vector2 _posicionOriginal;
	private bool _bloqueada = false;
	private Vector2 _offsetMouse;
	
	private Vector2 _escalaNormalMano  = new Vector2(3.0f, 3.0f); // COMPACTA en mano
	private Vector2 _escalaHover       = new Vector2(6.5f, 6.5f); // GRANDE al pasar dedo
	private Vector2 _escalaAlArrastrar = new Vector2(3.5f, 3.5f); // MEDIANA al arrastrar

	public override void _Ready()
	{
		PivotOffset = Size / 2;
		MouseFilter = MouseFilterEnum.Stop;
		Scale = _escalaNormalMano;
	}

	public void GuardarEstadoOriginal() => _posicionOriginal = Position;

	public void AsignarDatos(string rutaImg, string rutaTropa, int id)
	{
		IdCarta = id;
		var foto = GetNodeOrNull<TextureRect>("foto");
		if (foto != null && ResourceLoader.Exists(rutaImg))
		{
			foto.Texture = GD.Load<Texture2D>(rutaImg);
			foto.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			foto.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		}
		if (ResourceLoader.Exists(rutaTropa))
			EscenaTropa = GD.Load<PackedScene>(rutaTropa);
		else
			GD.PrintErr($"[Carta] Escena de tropa no encontrada: {rutaTropa}");
	}

	public override void _GuiInput(InputEvent @event)
	{
		// 1. BUSCAMOS EL CAMPO PARA VALIDAR TURNO
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		
		// BLOQUEO SI EL JUEGO TERMINÓ O SI NO ES MI TURNO O NO HAY MOVIMIENTOS
		if (campo != null)
		{
			if (campo.juegoTerminado || !campo.esTurnoJugador || campo.movimientosRestantes <= 0) 
				return;
		}

		if (!EstaEnMano || _bloqueada) return;

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				EstaArrastrando = true;
				_offsetMouse = GetGlobalMousePosition() - GlobalPosition;
				ZIndex = 200;
				Tween t = CreateTween();
				t.TweenProperty(this, "scale", _escalaAlArrastrar, 0.1f);
			}
			else if (EstaArrastrando)
			{
				EstaArrastrando = false;
				VerificarSoltado();
			}
		}

		if (@event is InputEventMouseMotion mm && EstaArrastrando)
		{
			GlobalPosition = GetGlobalMousePosition() - _offsetMouse;
		}
	}

	private void VerificarSoltado()
	{
		var zonas = GetTree().GetNodesInGroup("zonas_invocacion");
		bool exito = false;
		foreach (Node2D puntoMod in zonas)
		{
			if (GlobalPosition.DistanceTo(puntoMod.GlobalPosition) < 180)
			{
				if (puntoMod.GetNodeOrNull("Ocupado") == null)
				{
					exito = true;
					ConfirmarInvocacion(puntoMod);
					break;
				}
			}
		}
		if (!exito) RegresarAMano();
	}

	private void ConfirmarInvocacion(Node2D puntoMod)
	{
		EstaEnMano = false;
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		if (campo != null) campo.TropaInvocada(puntoMod, EscenaTropa);
		QueueFree();
	}

	private void RegresarAMano()
	{
		ZIndex = 1;
		Tween t = CreateTween().SetParallel(true);
		t.TweenProperty(this, "position", _posicionOriginal, 0.2f);
		t.TweenProperty(this, "scale", _escalaNormalMano, 0.2f);
	}

	public void _on_mouse_entered()
	{
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		if (campo != null && (!campo.esTurnoJugador || campo.movimientosRestantes <= 0)) return;

		if (!EstaEnMano || EstaArrastrando || _bloqueada) return;
		ZIndex = 150;
		Vector2 pivotAntes = PivotOffset;
		PivotOffset = new Vector2(Size.X * 0.5f, Size.Y);   // crece hacia arriba (no tapa las otras)
		Tween t = CreateTween();
		t.TweenProperty(this, "scale", _escalaHover, 0.12f)
		 .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public void _on_mouse_exited()
	{
		if (!EstaArrastrando)
		{
			ZIndex = 1;
			Tween t = CreateTween();
			t.TweenProperty(this, "scale", _escalaNormalMano, 0.12f);
			t.Finished += () => { if (IsInstanceValid(this)) PivotOffset = Size / 2; };
		}
	}
}
