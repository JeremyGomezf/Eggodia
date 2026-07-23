using Godot;
using System;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://DatosCartas/MenuConstructor.tscn";
	[Export] public string RutaCampoPruebas    = "res://escenas/gameplay/campo_pruebas.tscn";
	[Export] public string RutaComoJugar       = "res://escenas/menu/PantallaComoJugar.tscn";
	[Export] public string RutaBestiario       = "res://escenas/menu/PantallaBestiario.tscn";

	private AudioStreamPlayer _musicaFondo;
	private PanelContainer _panelSettings;

	public override void _Ready()
	{
		var btnJugar    = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		var btnOpciones = GetNodeOrNull<Button>("VBoxContainer/OPCIONES");
		var btnSalir    = GetNodeOrNull<Button>("VBoxContainer/SALIR");

		_panelSettings = GetNodeOrNull<PanelContainer>("PanelSettings");

		// HUD de monedas — esquina superior derecha
		var hudMonedas = MonedasHUD.Crear();
		hudMonedas.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		hudMonedas.OffsetLeft = -180; hudMonedas.OffsetTop = 20;
		hudMonedas.OffsetRight = -20; hudMonedas.OffsetBottom = 56;
		AddChild(hudMonedas);

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

		// Botón PRUEBAS — creado por código para no necesitar assets de imagen
		var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
		if (vbox != null)
		{
			var btnPruebas = new Button();
			btnPruebas.Name              = "PRUEBAS";
			btnPruebas.Text              = "PRUEBAS";
			btnPruebas.CustomMinimumSize = new Vector2(250, 70);
			btnPruebas.AddThemeFontSizeOverride("font_size", 32);

			var estilo = new StyleBoxFlat();
			estilo.BgColor     = new Color(0.10f, 0.22f, 0.40f, 0.90f);
			estilo.BorderColor = new Color(0.40f, 0.70f, 1.00f, 0.85f);
			estilo.SetBorderWidthAll(3);
			estilo.SetCornerRadiusAll(10);
			btnPruebas.AddThemeStyleboxOverride("normal", estilo);

			var estiloHover = new StyleBoxFlat();
			estiloHover.BgColor     = new Color(0.15f, 0.35f, 0.60f, 0.95f);
			estiloHover.BorderColor = new Color(0.55f, 0.85f, 1.00f, 1.00f);
			estiloHover.SetBorderWidthAll(3);
			estiloHover.SetCornerRadiusAll(10);
			btnPruebas.AddThemeStyleboxOverride("hover", estiloHover);

			btnPruebas.Pressed += () => GetTree().ChangeSceneToFile(RutaCampoPruebas);
			AgregarAnimacionHover(btnPruebas);

			// Insertar antes de SALIR (índice 2)
			vbox.AddChild(btnPruebas);
			vbox.MoveChild(btnPruebas, 2);

			var btnBestiario = CrearBotonAzul("BESTIARIO", 28, () => GetTree().ChangeSceneToFile(RutaBestiario));
			vbox.AddChild(btnBestiario);
			vbox.MoveChild(btnBestiario, 2);

			// "CÓMO JUGAR" solo aparece si el jugador aún no ha visto el tutorial
			if (!Preferencias.TutorialVisto)
			{
				var btnAyuda = CrearBotonAzul("CÓMO JUGAR", 28, AbrirComoJugar);
				vbox.AddChild(btnAyuda);
				vbox.MoveChild(btnAyuda, 2);
			}
		}

		// Primer inicio del juego: abrir el tutorial automáticamente (una sola vez)
		if (!Preferencias.TutorialVisto)
		{
			Preferencias.TutorialVisto = true;   // marcar ya visto (evita repetir y bucles)
			// Callable.From evita el fallo silencioso de CallDeferred(nombre) con métodos privados de C#
			Callable.From(AbrirComoJugar).CallDeferred();
		}
	}

	private void AbrirComoJugar()
	{
		Preferencias.TutorialVisto = true;
		GetTree().ChangeSceneToFile(RutaComoJugar);
	}

	private Button CrearBotonAzul(string texto, int fontSize, Action onPress)
	{
		var btn = new Button();
		btn.Text              = texto;
		btn.CustomMinimumSize = new Vector2(250, 70);
		btn.AddThemeFontSizeOverride("font_size", fontSize);

		var est = new StyleBoxFlat();
		est.BgColor     = new Color(0.10f, 0.22f, 0.40f, 0.90f);
		est.BorderColor = new Color(0.40f, 0.70f, 1.00f, 0.85f);
		est.SetBorderWidthAll(3);
		est.SetCornerRadiusAll(10);
		btn.AddThemeStyleboxOverride("normal", est);

		var estHover = new StyleBoxFlat();
		estHover.BgColor     = new Color(0.15f, 0.35f, 0.60f, 0.95f);
		estHover.BorderColor = new Color(0.55f, 0.85f, 1.00f, 1.00f);
		estHover.SetBorderWidthAll(3);
		estHover.SetCornerRadiusAll(10);
		btn.AddThemeStyleboxOverride("hover", estHover);

		btn.Pressed += () => onPress();
		AgregarAnimacionHover(btn);
		return btn;
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
