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
		if (_costoLabel != null) _costoLabel.Text = datos.Costo.ToString();
		if (_ataqueLabel != null) _ataqueLabel.Text = datos.Ataque.ToString();
		if (_defensaLabel != null) _defensaLabel.Text = datos.Defensa.ToString();
		if (_vidaLabel != null) _vidaLabel.Text = datos.Vida.ToString();
		if (_velocidadLabel != null) _velocidadLabel.Text = datos.Velocidad;
		if (_rangoLabel != null) _rangoLabel.Text = datos.Rango;
		if (_descripcionLabel != null) _descripcionLabel.Text = $"[i]{datos.Descripcion}[/i]";
		if (_costoBadgeLabel != null) _costoBadgeLabel.Text = datos.Costo > 0 ? datos.Costo.ToString() : "4";
		
		if (_eraLabel != null) _eraLabel.Text = string.IsNullOrEmpty(datos.Era) ? "Era Medieval" : datos.Era;
		
		if (_elementoLabel != null)
		{
			string elem = string.IsNullOrEmpty(datos.Elemento) ? "Metal" : datos.Elemento;
			_elementoLabel.Text = elem.ToUpper();
			
			// Cambiar color de gema según elemento
			var badgePanel = _elementoLabel.GetParent<Control>();
			if (badgePanel != null)
			{
				Color colorElem = elem.ToLower() switch
				{
					"fuego" => new Color(0.92f, 0.35f, 0.15f),
					"agua" => new Color(0.23f, 0.55f, 0.93f),
					"naturaleza" => new Color(0.30f, 0.68f, 0.31f),
					"sombra" => new Color(0.55f, 0.27f, 0.68f),
					_ => new Color(0.55f, 0.57f, 0.67f) // Metal / Defecto
				};
				badgePanel.Modulate = colorElem;
			}
		}
		
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
