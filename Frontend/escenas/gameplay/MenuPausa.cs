using Godot;
using System;

public partial class MenuPausa : CanvasLayer
{
	private ColorRect _overlay;
	private PanelSettings _panelSettings;
	private VBoxContainer _vboxPausa;
	private PanelContainer _panelConfirmacion;

	public override void _Ready()
	{
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
	}

	private void Reanudar()
	{
		_overlay.Visible = false;
		if (_panelSettings != null) _panelSettings.Visible = false;
		GetTree().Paused = false;
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
