using Godot;
using System;

/// <summary>Interfaz de BtnOnline. El multijugador es SOLO para cuentas registradas: si el jugador
/// entró como invitado, se le pide crear cuenta / iniciar sesión en vez de emparejarlo. Si tiene
/// cuenta, abre el emparejamiento 1v1 real (MatchmakingOnline.cs).</summary>
public partial class MenuPrincipal : Control
{
	private MatchmakingOnline _matchmaking;
	private CanvasLayer _avisoGrande;

	private static readonly Color COLOR_BOTON_AVISO_PRINCIPAL = new(0.95f, 0.76f, 0.3f);
	private static readonly Color COLOR_TEXTO_BOTON_PRINCIPAL = new(0.17f, 0.12f, 0.04f);
	private static readonly Color COLOR_BOTON_AVISO_VOLVER    = new(0.32f, 0.32f, 0.38f);

	// Solo cuentas registradas pueden jugar en línea (los invitados tienen UsuarioId <= 0).
	private static bool EsCuentaRegistrada() => SesionJuego.Instance != null && SesionJuego.Instance.EstaLogueado;

	private void MostrarPantallaOnline()
	{
		if (!EsCuentaRegistrada())
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
		MostrarAvisoGrande("Necesitas una cuenta",
			"El modo en línea es solo para cuentas registradas. Inicia sesión o crea una cuenta para jugar contra otros.",
			("INICIAR SESIÓN", COLOR_BOTON_AVISO_PRINCIPAL, COLOR_TEXTO_BOTON_PRINCIPAL,
				() => GetTree().ChangeSceneToFile("res://escenas/menu/PanelLogin.tscn")),
			("VOLVER", COLOR_BOTON_AVISO_VOLVER, Colors.White, null));
	}

	/// <summary>Aviso grande del menú, pensado para el celular: caja centrada con título y texto en
	/// letra grande y botones anchos. Lo usan "Necesitas una cuenta" y "Mazo incompleto", así los dos
	/// se ven del mismo tamaño. Un botón con acción null solo cierra el aviso.</summary>
	private void MostrarAvisoGrande(string titulo, string mensaje,
		params (string texto, Color fondo, Color letra, Action accion)[] botones)
	{
		if (_avisoGrande != null && IsInstanceValid(_avisoGrande)) _avisoGrande.QueueFree();

		var capa = new CanvasLayer { Layer = 300 };
		AddChild(capa);
		_avisoGrande = capa;
		void Cerrar() { if (IsInstanceValid(capa)) capa.QueueFree(); }

		var fondo = new ColorRect { Color = new Color(0.02f, 0.03f, 0.06f, 0.8f), MouseFilter = Control.MouseFilterEnum.Stop };
		fondo.SetAnchorsPreset(LayoutPreset.FullRect);
		capa.AddChild(fondo);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(1100, 0) };
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.GrowHorizontal = GrowDirection.Both;
		panel.GrowVertical   = GrowDirection.Both;
		var sb = EstiloUI.CuadroDorado();
		sb.ContentMarginLeft = sb.ContentMarginRight = 56;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 44;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 22;
		panel.AddThemeStyleboxOverride("panel", sb);
		fondo.AddChild(panel);

		var caja = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		caja.AddThemeConstantOverride("separation", 34);
		panel.AddChild(caja);

		var lblTitulo = new Label { Text = titulo, HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(lblTitulo, 64, new Color(1f, 0.85f, 0.3f));
		caja.AddChild(lblTitulo);

		var lblTexto = new Label
		{
			Text = mensaje,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		EstiloUI.Texto(lblTexto, 42, new Color(0.9f, 0.93f, 0.98f));
		caja.AddChild(lblTexto);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 32);
		caja.AddChild(fila);
		foreach (var (texto, colorFondo, colorLetra, accion) in botones)
		{
			var b = CrearBotonAviso(texto, colorFondo, colorLetra);
			b.Pressed += () => { Cerrar(); accion?.Invoke(); };
			fila.AddChild(b);
		}

		// Entra con el mismo "pop" que los demás avisos del menú.
		panel.Resized += () => { if (IsInstanceValid(panel)) panel.PivotOffset = panel.Size / 2; };
		panel.Scale = new Vector2(0.6f, 0.6f);
		panel.CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.25f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private static Button CrearBotonAviso(string texto, Color fondo, Color fuente)
	{
		var b = new Button { Text = texto };
		b.CustomMinimumSize = new Vector2(380, 110);
		if (EstiloUI.Fuente != null) b.AddThemeFontOverride("font", EstiloUI.Fuente);
		b.AddThemeFontSizeOverride("font_size", 40);
		foreach (var n in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
			b.AddThemeColorOverride(n, fuente);
		StyleBoxFlat Caja(Color bg)
		{
			var sb = new StyleBoxFlat { BgColor = bg };
			sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
			return sb;
		}
		b.AddThemeStyleboxOverride("normal",  Caja(fondo));
		b.AddThemeStyleboxOverride("hover",   Caja(fondo.Lightened(0.1f)));
		b.AddThemeStyleboxOverride("pressed", Caja(fondo.Darkened(0.12f)));
		b.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
		return b;
	}
}
