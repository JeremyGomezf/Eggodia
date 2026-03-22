using Godot;
using System;
using System.Text;
using System.Collections.Generic;

public partial class ApiConnector : Node
{
	// Asegúrate de que tu API esté corriendo en VS Code con 'dotnet run'
	private string _url = "http://localhost:5289/api/Cartas"; 
	private HttpRequest _httpRequest;

	public override void _Ready()
	{
		// Creamos el nodo de red
		_httpRequest = new HttpRequest();
		AddChild(_httpRequest);
		_httpRequest.RequestCompleted += OnRequestCompleted;

		GD.Print("Conectando a Age of Duels API...");
		_httpRequest.Request(_url);
	}

	private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
	{
		if (responseCode == 200)
		{
			string json = Encoding.UTF8.GetString(body);
			GD.Print("¡DATOS RECIBIDOS!: " + json);
			// Aquí es donde luego haremos que las cartas aparezcan en el puente
		}
		else
		{
			GD.Print("Error de conexión: " + responseCode);
		}
	}
}
