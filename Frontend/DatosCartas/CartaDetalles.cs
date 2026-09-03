using Godot;
using System;

public partial class CartaDetalles : Control
{
	[Export] private Label _nombreLabel;
	[Export] private TextureRect _imagenRect;
	[Export] private Label _costoLabel;
	[Export] private Label _ataqueLabel;
	[Export] private Label _defensaLabel;
	[Export] private Label _vidaLabel;
	[Export] private Label _velocidadLabel;
	[Export] private Label _rangoLabel;
	[Export] private RichTextLabel _descripcionLabel; 
	[Export] private Label _fuerteContraLabel;
	[Export] private Label _debilContraLabel;
	[Export] private Label _elementoLabel;
	[Export] private Label _eraLabel;
	[Export] private Label _costoBadgeLabel;

	public void MostrarDatos(CartaData datos)
	{
		if (datos == null) return;

		if (_nombreLabel != null) 
		{
			_nombreLabel.Text = datos.Nombre;
			_nombreLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		}

		if (_imagenRect != null) _imagenRect.Texture = datos.Imagen;
		if (_ataqueLabel != null) _ataqueLabel.Text = datos.Ataque.ToString();
		if (_defensaLabel != null) _defensaLabel.Text = datos.Defensa.ToString();
		if (_vidaLabel != null) _vidaLabel.Text = datos.Vida.ToString();
		if (_velocidadLabel != null) _velocidadLabel.Text = datos.Velocidad;
		if (_rangoLabel != null) _rangoLabel.Text = datos.Rango;
		if (_descripcionLabel != null) _descripcionLabel.Text = $"[i]{datos.Descripcion}[/i]";

		// El juego no tiene sistema de costo ni de elementos — se ocultan por completo
		// (no solo se deja el texto en blanco) para que no quede ni el círculo ni el renglón.
		if (_costoBadgeLabel != null) _costoBadgeLabel.GetParent<Control>().Visible = false;
		if (_costoLabel != null)
		{
			_costoLabel.Visible = false;
			var lblCostoKey = _costoLabel.GetParent()?.GetNodeOrNull<Label>("LabelCosto");
			if (lblCostoKey != null) lblCostoKey.Visible = false;
		}
		if (_elementoLabel != null) _elementoLabel.GetParent<Control>().Visible = false;

		if (_eraLabel != null) _eraLabel.Text = string.IsNullOrEmpty(datos.Era) ? "Era Medieval" : datos.Era;
		
		// Fortalezas y debilidades
		if (_fuerteContraLabel != null)
		{
			if (!string.IsNullOrEmpty(datos.FuerteContra))
			{
				_fuerteContraLabel.Text = "FUERTE CONTRA: " + datos.FuerteContra.ToUpper();
				_fuerteContraLabel.GetParent<Control>().Visible = true;
			}
			else
			{
				_fuerteContraLabel.GetParent<Control>().Visible = false;
			}
		}
		
		if (_debilContraLabel != null)
		{
			if (!string.IsNullOrEmpty(datos.DebilContra))
			{
				_debilContraLabel.Text = "DÉBIL CONTRA: " + datos.DebilContra.ToUpper();
				_debilContraLabel.GetParent<Control>().Visible = true;
			}
			else
			{
				_debilContraLabel.GetParent<Control>().Visible = false;
			}
		}
	}
}
