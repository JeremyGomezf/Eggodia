using Godot;

/// <summary>
/// Botón de RETIRADA para las partidas en línea (rival real o bot).
///
/// En línea no hay pausa (el rival sigue jugando), así que el botón de pausa se oculta y en su mismo
/// lugar aparece este: el huevo roto (el mismo dibujo de la pantalla de derrota). Al tocarlo pregunta
/// "¿Deseas retirarte?" con el marco de cristal y los botones del juego; si confirma, el servidor le da
/// la victoria al rival (ver RendirseOnline). La partida sigue corriendo detrás mientras se decide.
/// </summary>
public partial class Campo1 : Node2D
{
	private const string RUTA_ICONO_RETIRADA = "res://imagenes/botonescampo1/huevoroto.png";
	private CanvasLayer _capaRetirada;

	/// <summary>Reemplaza el botón de pausa por el de retirada (solo en línea). Se llama después de
	/// acomodar el HUD, así el botón nuevo copia la posición y escala finales del de pausa.</summary>
	private void ConfigurarBotonRetirada(TextureButton btnPausa)
	{
		if (btnPausa == null || !PareceOnline) return;
		var icono = GD.Load<Texture2D>(RUTA_ICONO_RETIRADA);
		if (icono == null) return;

		var btn = (TextureButton)btnPausa.Duplicate();
		btn.Name = "RetiradaButton";
		btn.TextureNormal = icono;
		btn.TextureHover = icono;
		btn.TexturePressed = icono;
		btn.IgnoreTextureSize = true;
		btn.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
		btn.TooltipText = "Retirarse";
		btnPausa.AddSibling(btn);
		btnPausa.Visible = false;
		btn.Pressed += MostrarConfirmacionRetirada;
		AgregarJuiceBoton(btn);
	}

	private void MostrarConfirmacionRetirada()
	{
		if (juegoTerminado) return;
		if (_capaRetirada != null && IsInstanceValid(_capaRetirada)) { _capaRetirada.Visible = true; return; }

		_capaRetirada = new CanvasLayer { Layer = 120 };
		AddChild(_capaRetirada);

		var velo = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
		velo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_capaRetirada.AddChild(velo);

		var centro = new CenterContainer();
		centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		velo.AddChild(centro);

		var panel = new PanelContainer();
		EstiloUI.MarcoCristal(panel, 80, 56);
		centro.AddChild(panel);

		var vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddThemeConstantOverride("separation", 26);
		panel.AddChild(vbox);

		var huevo = new TextureRect
		{
			Texture = GD.Load<Texture2D>(RUTA_ICONO_RETIRADA),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(200, 140),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		vbox.AddChild(huevo);

		var titulo = new Label { Text = "¿Deseas retirarte?", HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Titulo(titulo, 60);
		vbox.AddChild(titulo);

		var detalle = new Label { Text = "El rival ganará la partida.", HorizontalAlignment = HorizontalAlignment.Center };
		EstiloUI.Texto(detalle, 36, EstiloUI.TextoClaro);
		vbox.AddChild(detalle);

		var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		fila.AddThemeConstantOverride("separation", 40);
		vbox.AddChild(fila);

		var btnNo = new Button { Text = "NO", CustomMinimumSize = new Vector2(300, 110) };
		EstiloUI.Boton(btnNo, 42, accion: true);
		btnNo.Pressed += CerrarConfirmacionRetirada;
		fila.AddChild(btnNo);

		var btnSi = new Button { Text = "SÍ, RETIRARME", CustomMinimumSize = new Vector2(380, 110) };
		EstiloUI.Boton(btnSi, 42, rojo: true);
		btnSi.Pressed += () =>
		{
			CerrarConfirmacionRetirada();
			RendirseOnline();
		};
		fila.AddChild(btnSi);

		SonidoUI.EngancharBotones(_capaRetirada);
	}

	private void CerrarConfirmacionRetirada()
	{
		if (_capaRetirada != null && IsInstanceValid(_capaRetirada)) _capaRetirada.Visible = false;
	}
}
