using Godot;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://DatosCartas/MenuConstructor.tscn";

	private AudioStreamPlayer _musicaFondo;
	private PanelContainer _panelSettings;

	public override void _Ready()
	{
		var btnJugar    = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		var btnOpciones = GetNodeOrNull<Button>("VBoxContainer/OPCIONES");
		var btnSalir    = GetNodeOrNull<Button>("VBoxContainer/SALIR");

		_panelSettings = GetNodeOrNull<PanelContainer>("PanelSettings");

		if (btnJugar    != null) 
		{
			btnJugar.Pressed += () => GetTree().ChangeSceneToFile(RutaConstructorMazo); 
			AgregarAnimacionHover(btnJugar);
		}
		if (btnOpciones != null) 
		{
			btnOpciones.Pressed += MostrarSettings;
			AgregarAnimacionHover(btnOpciones);
		}
		if (btnSalir    != null) 
		{
			btnSalir.Pressed += () => GetTree().Quit();
			AgregarAnimacionHover(btnSalir);
		}
	}

	private void AgregarAnimacionHover(Button btn)
	{
		btn.PivotOffset = btn.CustomMinimumSize / 2;
		btn.MouseEntered += () => 
		{
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", new Vector2(1.1f, 1.1f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () => 
		{
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		};
	}

	private void MostrarSettings()
	{
		if (_panelSettings != null) _panelSettings.Visible = true;
	}
}
