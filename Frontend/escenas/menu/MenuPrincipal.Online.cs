using Godot;

/// <summary>Interfaz de BtnOnline. El multijugador es SOLO para cuentas registradas: si el jugador
/// entró como invitado, se le pide crear cuenta / iniciar sesión en vez de emparejarlo. Si tiene
/// cuenta, abre el emparejamiento 1v1 real (MatchmakingOnline.cs).</summary>
public partial class MenuPrincipal : Control
{
	private MatchmakingOnline _matchmaking;
	private CanvasLayer _avisoCuenta;

	private void MostrarPantallaOnline()
	{
		// Solo cuentas registradas pueden jugar en línea (los invitados tienen UsuarioId <= 0).
		if (SesionJuego.Instance == null || !SesionJuego.Instance.EstaLogueado)
		{
			MostrarAvisoCuentaRequerida();
			return;
		}

		if (_matchmaking != null && IsInstanceValid(_matchmaking)) _matchmaking.QueueFree();
		_matchmaking = new MatchmakingOnline();
		AddChild(_matchmaking);
	}

	private void MostrarAvisoCuentaRequerida()
	{
		if (_avisoCuenta != null && IsInstanceValid(_avisoCuenta)) _avisoCuenta.QueueFree();

		var capa = new CanvasLayer { Layer = 300 };
		AddChild(capa);
		_avisoCuenta = capa;

		var fondo = new ColorRect();
		fondo.Color = new Color(0.02f, 0.03f, 0.06f, 0.85f);
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop;
		capa.AddChild(fondo);

		var caja = new VBoxContainer();
		caja.SetAnchorsPreset(LayoutPreset.Center);
		caja.OffsetLeft = -440; caja.OffsetRight = 440; caja.OffsetTop = -230; caja.OffsetBottom = 230;
		caja.Alignment = BoxContainer.AlignmentMode.Center;
		caja.AddThemeConstantOverride("separation", 30);
		fondo.AddChild(caja);

		var titulo = new Label { Text = "Necesitas una cuenta" };
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		titulo.AddThemeFontSizeOverride("font_size", 54);
		titulo.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		caja.AddChild(titulo);

		var texto = new Label { Text = "El modo en línea es solo para cuentas registradas.\nInicia sesión o crea una cuenta para jugar contra otros." };
		texto.HorizontalAlignment = HorizontalAlignment.Center;
		texto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		texto.AddThemeFontSizeOverride("font_size", 32);
		texto.AddThemeColorOverride("font_color", new Color(0.9f, 0.93f, 0.98f));
		caja.AddChild(texto);

		var fila = new HBoxContainer();
		fila.Alignment = BoxContainer.AlignmentMode.Center;
		fila.AddThemeConstantOverride("separation", 24);
		caja.AddChild(fila);

		var btnLogin = CrearBotonAviso("INICIAR SESIÓN", new Color(0.95f, 0.76f, 0.3f), new Color(0.17f, 0.12f, 0.04f));
		btnLogin.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/PanelLogin.tscn");
		fila.AddChild(btnLogin);

		var btnVolver = CrearBotonAviso("VOLVER", new Color(0.32f, 0.32f, 0.38f), Colors.White);
		btnVolver.Pressed += () => { if (IsInstanceValid(_avisoCuenta)) _avisoCuenta.QueueFree(); };
		fila.AddChild(btnVolver);
	}

	private static Button CrearBotonAviso(string texto, Color fondo, Color fuente)
	{
		var b = new Button { Text = texto };
		b.CustomMinimumSize = new Vector2(300, 90);
		b.AddThemeFontSizeOverride("font_size", 32);
		b.AddThemeColorOverride("font_color", fuente);
		var sb = new StyleBoxFlat { BgColor = fondo };
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		b.AddThemeStyleboxOverride("normal", sb);
		return b;
	}
}
