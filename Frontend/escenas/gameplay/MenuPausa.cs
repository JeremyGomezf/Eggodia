using Godot;
using System;

public partial class MenuPausa : CanvasLayer
{
	// Panel de "¿seguro que te rindes?": sin imagen de fondo, solo texto y botones, bien grandes.
	private const int TAM_FUENTE_TEXTO_RENDIRSE = 46;
	private const int TAM_FUENTE_BOTON_RENDIRSE = 42;
	private static readonly Vector2 TAM_BOTON_RENDIRSE = new(320, 110);

	private ColorRect _overlay;
	private PanelSettings _panelSettings;
	private VBoxContainer _vboxPausa;
	private PanelContainer _panelConfirmacion;

	public override void _Ready()
	{
		// Clic de interfaz en los botones de esta pantalla (se crea aparte de Campo1/MenuPrincipal).
		Callable.From(() => SonidoUI.EngancharBotones(this)).CallDeferred();

		_overlay = GetNode<ColorRect>("Overlay");
		_panelSettings = GetNodeOrNull<PanelSettings>("PanelSettings");
		// Dentro de una partida (VS BOT) no se puede cerrar sesión: se oculta ese botón. En el menú
		// principal el mismo panel sí lo muestra.
		_panelSettings?.OcultarCerrarSesion();
		_vboxPausa = GetNode<VBoxContainer>("Overlay/VBox");
		_panelConfirmacion = GetNode<PanelContainer>("Overlay/PanelConfirmacion");

		var btnContinue = GetNode<Button>("Overlay/VBox/BtnContinue");
		var btnSettings = GetNode<Button>("Overlay/VBox/BtnSettings");
		var btnExit     = GetNode<Button>("Overlay/VBox/BtnExit");

		var btnCancelar = GetNode<Button>("Overlay/PanelConfirmacion/VBox/HBox/BtnCancelar");
		var btnConfirmar = GetNode<Button>("Overlay/PanelConfirmacion/VBox/HBox/BtnConfirmar");

		// Estilo del juego (fuente Almendra + botones/paneles nuestros) en vez del gris por defecto.
		// Por código para que se vea siempre, sin depender de la .tscn.
		EstiloUI.Boton(btnContinue, 42);
		EstiloUI.Boton(btnSettings, 42);
		EstiloUI.Boton(btnExit, 42, rojo: true);      // RENDIRSE en rojo
		EstiloUI.Boton(btnCancelar, TAM_FUENTE_BOTON_RENDIRSE);
		EstiloUI.Boton(btnConfirmar, TAM_FUENTE_BOTON_RENDIRSE, rojo: true); // SALIR en rojo
		EstiloUI.Titulo(GetNodeOrNull<Label>("Overlay/VBox/Titulo"), 68);

		// Confirmación de rendirse: SIN marco de imagen (se pidió sacarlo, desentonaba) — queda el panel
		// plano de la escena y se agrandan texto y botones, que es lo único que tiene que leerse acá.
		EstiloUI.Texto(GetNodeOrNull<Label>("Overlay/PanelConfirmacion/VBox/Label"),
			TAM_FUENTE_TEXTO_RENDIRSE, EstiloUI.TextoClaro);
		btnCancelar.CustomMinimumSize  = TAM_BOTON_RENDIRSE;
		btnConfirmar.CustomMinimumSize = TAM_BOTON_RENDIRSE;

		// Fondo semi-transparente SOLO para este panel (el StyleBox de la escena es opaco y además lo
		// comparten los botones, por eso se aplica por código acá y no se toca aquel). Los botones
		// quedan tal cual: solo se suaviza la caja de atrás, que tapaba demasiado.
		if (_panelConfirmacion != null)
		{
			var fondo = new StyleBoxFlat
			{
				BgColor          = new Color(0.08f, 0.11f, 0.18f, 0.55f),
				BorderColor      = new Color(0.65f, 0.75f, 0.9f, 0.35f),
				BorderWidthLeft  = 2, BorderWidthTop    = 2,
				BorderWidthRight = 2, BorderWidthBottom = 2,
				CornerRadiusTopLeft     = 18, CornerRadiusTopRight    = 18,
				CornerRadiusBottomLeft  = 18, CornerRadiusBottomRight = 18,
				ContentMarginLeft = 30, ContentMarginRight  = 30,
				ContentMarginTop  = 26, ContentMarginBottom = 26,
			};
			_panelConfirmacion.AddThemeStyleboxOverride("panel", fondo);
		}

		btnContinue.Pressed += Reanudar;
		btnSettings.Pressed += AbrirSettings;
		btnExit.Pressed += MostrarConfirmacion;

		btnCancelar.Pressed += OcultarConfirmacion;
		btnConfirmar.Pressed += Rendirse;

		_overlay.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (GetTree().Paused)
			{
				if (_panelSettings != null && _panelSettings.Visible)
				{
					CerrarSettings();
				}
				else if (_panelConfirmacion.Visible)
				{
					OcultarConfirmacion();
				}
				else
				{
					Reanudar();
				}
			}
			else
			{
				Pausar();
			}
		}
	}

	public void Pausar()
	{
		GetTree().Paused = true;
		_overlay.Visible = true;
		_vboxPausa.Visible = true;
		_panelConfirmacion.Visible = false;
		// Tutorial: ocultar el cuadro "info" (capa 500) para que no quede encima del panel de pausa.
		GetParentOrNull<Campo1>()?.OcultarCapaTutorialEnPausa(true);
	}

	private void Reanudar()
	{
		_overlay.Visible = false;
		if (_panelSettings != null) _panelSettings.Visible = false;
		GetTree().Paused = false;
		GetParentOrNull<Campo1>()?.OcultarCapaTutorialEnPausa(false); // volver a mostrar el "info"
	}

	private void AbrirSettings()
	{
		if (_panelSettings == null) return;
		// Ocultar los botones de pausa mientras se ven los ajustes (si no, asoman por detrás del panel).
		_vboxPausa.Visible = false;
		_panelSettings.AlCerrar = CerrarSettings; // al cerrar los ajustes, restaurar los botones de pausa
		_panelSettings.Visible = true;
	}

	// Cierra el panel de ajustes y vuelve a mostrar los botones de pausa.
	private void CerrarSettings()
	{
		if (_panelSettings != null)
		{
			_panelSettings.AlCerrar = null;       // evitar recursión si se cierra por el botón CERRAR
			_panelSettings.Visible = false;
		}
		_vboxPausa.Visible = true;
	}

	private void MostrarConfirmacion()
	{
		_vboxPausa.Visible = false;
		_panelConfirmacion.Visible = true;
	}

	private void OcultarConfirmacion()
	{
		_panelConfirmacion.Visible = false;
		_vboxPausa.Visible = true;
	}

	private void Rendirse()
	{
		// Ocultar overlay de pausa para mostrar el final
		_overlay.Visible = false;
		
		var campo = GetParentOrNull<Campo1>();
		if (campo != null)
		{
			campo.FinalizarPartida("DERROTA");
		}
		else
		{
			LimpiezaEfectos.LimpiarEfectosDeCampo();
			GetTree().Paused = false;
			GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
		}
	}
}
