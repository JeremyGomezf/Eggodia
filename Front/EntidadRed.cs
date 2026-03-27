using Godot;
using System;
using Godot.Collections;

public partial class EntidadRed : Node2D
{
	[Export] public string IdPersonaje = "trono";
	private HttpRequest _peticion;

	public override void _Ready()
	{
		_peticion = GetNode<HttpRequest>("HTTPRequest");
		_peticion.RequestCompleted += AlRecibirDatos;
		
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
			// Esto saca el título del post de prueba de internet
			string textoDeInternet = datos["title"].ToString();
			
			GD.Print("¡Dato recibido!: " + textoDeInternet);
			
			// Aquí le decimos al Label que muestre el dato
			GetNode<Label>("Label").Text = "NUBE: " + textoDeInternet;
		}
		else 
		{
			GD.PrintErr("Error al procesar el JSON del servidor");
		}
	}
	}
