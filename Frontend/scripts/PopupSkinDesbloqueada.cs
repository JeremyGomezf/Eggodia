using Godot;
using System;

/// <summary>Ventana de "¡NUEVO SKIN DE HUEVO DESBLOQUEADO!": imagen del huevo, su nombre y "¡GENIAL!".
/// La usan el canje de códigos (menú) y el premio por ganarle en línea a una cuenta dev (pantalla de
/// victoria o menú, si se salió antes de que llegara la respuesta del servidor).</summary>
public static class PopupSkinDesbloqueada
{
	public static void Mostrar(Node padre, string nombreSkin, string rutaImagen, Action alCerrar = null,
		string subtitulo = null)
	{
		if (padre == null || !GodotObject.IsInstanceValid(padre)) return;

		// Por encima de todo (pantallas de victoria/derrota, avisos, guías).
		var capa = new CanvasLayer { Layer = 950, ProcessMode = Node.ProcessModeEnum.Always };
		padre.AddChild(capa);

		var fondo = new ColorRect { Color = new Color(0.02f, 0.04f, 0.1f, 0.88f), MouseFilter = Control.MouseFilterEnum.Stop };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		capa.AddChild(fondo);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(780, 0) };
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical   = Control.GrowDirection.Both;

		var sb = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.12f, 0.22f, 0.98f),
			BorderColor = new Color(1f, 0.85f, 0.3f),
			ShadowColor = new Color(1f, 0.85f, 0.3f, 0.3f),
			ShadowSize = 20,
		};
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 3;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 24;
		sb.ContentMarginLeft = sb.ContentMarginRight = 30;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 30;
		panel.AddThemeStyleboxOverride("panel", sb);
		capa.AddChild(panel);

		var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddThemeConstantOverride("separation", 22);
		panel.AddChild(vbox);

		var lblHeader = new Label
		{
			Text = "¡NUEVO SKIN DE HUEVO DESBLOQUEADO!",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		EstiloUI.Texto(lblHeader, 40, new Color(1f, 0.88f, 0.3f));
		vbox.AddChild(lblHeader);

		if (!string.IsNullOrEmpty(subtitulo))
		{
			var lblSub = new Label { Text = subtitulo, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			EstiloUI.Texto(lblSub, 30, EstiloUI.TextoClaro);
			vbox.AddChild(lblSub);
		}

		var texRect = new TextureRect
		{
			CustomMinimumSize = new Vector2(300, 360),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		if (!string.IsNullOrEmpty(rutaImagen) && ResourceLoader.Exists(rutaImagen)) texRect.Texture = GD.Load<Texture2D>(rutaImagen);
		vbox.AddChild(texRect);

		var lblNombre = new Label { Text = nombreSkin, HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(lblNombre, 48, Colors.White);
		vbox.AddChild(lblNombre);

		var btn = new Button { Text = "¡GENIAL!", CustomMinimumSize = new Vector2(340, 92), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		EstiloUI.Boton(btn, 36, accion: true);
		btn.Pressed += () =>
		{
			if (GodotObject.IsInstanceValid(capa)) capa.QueueFree();
			alCerrar?.Invoke();
		};
		vbox.AddChild(btn);

		// Aparición elástica desde el centro.
		panel.Resized += () => { if (GodotObject.IsInstanceValid(panel)) panel.PivotOffset = panel.Size / 2f; };
		panel.Scale = Vector2.Zero;
		panel.CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.35f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}
}
