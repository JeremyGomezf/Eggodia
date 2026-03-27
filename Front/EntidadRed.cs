using Godot;
using System;
using Godot.Collections;

public partial class EntidadRed : Node2D
{
	[Export] public string IdPersonaje = "trono";
	private Godot.HttpRequest _peticion;

	public override void _Ready()
	{
		// 1. Buscamos el nodo de internet
		_peticion = GetNode<Godot.HttpRequest>("HTTPRequest");
		_peticion.RequestCompleted += AlRecibirDatos;
		
		// 2. Hacemos la llamada a la nube
		string url = "https://jsonplaceholder.typicode.com/posts/1";
		_peticion.Request(url);
		GD.Print($"Conectando con la nube para: {IdPersonaje}...");
	}

	private void AlRecibirDatos(long result, long responseCode, string[] headers, byte[] body)
	{
		var json = new Json();
		var error = json.Parse(body.GetStringFromUtf8());
		
		if (error == Error.Ok)
		{
			var datos = json.GetData().AsGodotDictionary();
			string textoDeInternet = datos["title"].ToString();
			
			GD.Print("¡Dato recibido!: " + textoDeInternet);
			
			// --- AQUÍ ESTÁ LO DEL LABEL ---
			// Buscamos el nodo Label que creaste en la escena
			var labelNodo = GetNodeOrNull<Label>("Label");
			
			if (labelNodo != null) 
			{
				labelNodo.Text = "NUBE: " + textoDeInternet;
			}
			else 
			{
				GD.PrintErr("¡OJO! No encontré el nodo llamado 'Label' en esta escena.");
			}
		}
	}
}
