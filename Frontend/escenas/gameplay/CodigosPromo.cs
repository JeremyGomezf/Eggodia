using Godot;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Sistema de códigos promocionales respaldado por el backend de Eggodia.
/// Valida contra SQLite en el servidor, ligado a la cuenta del jugador (Usuario.Id).
/// </summary>
public static class CodigosPromo
{
	public enum TipoResultado { Canjeado, YaUsado, Erroneo, Expirado, RequiereLogin }

	public class ResultadoCanje
	{
		public TipoResultado Tipo { get; set; }
		public string Mensaje { get; set; } = "";
		public string TipoRecompensa { get; set; } = ""; // "skin" | "monedas"
		public string ValorRecompensa { get; set; } = ""; // ruta imagen skin o monto
		public string NombreRecompensa { get; set; } = "";
		public int Monedas { get; set; } = 0;
	}

	public static async Task<ResultadoCanje> CanjearAsync(string entrada, int userId, Node contextNode)
	{
		if (string.IsNullOrWhiteSpace(entrada))
		{
			return new ResultadoCanje
			{
				Tipo = TipoResultado.Erroneo,
				Mensaje = "Por favor ingresa un código válido."
			};
		}

		if (userId <= 0)
		{
			return new ResultadoCanje
			{
				Tipo = TipoResultado.RequiereLogin,
				Mensaje = "Debes iniciar sesión primero para canjear códigos promocionales."
			};
		}

		string codigo = entrada.Trim();

		// Crear HTTPRequest temporal para la llamada asíncrona
		var http = new HttpRequest();
		contextNode.AddChild(http);

		var tcs = new TaskCompletionSource<(long Result, long Code, byte[] Body)>();
		http.RequestCompleted += (res, code, hdrs, body) =>
		{
			tcs.TrySetResult((res, code, body));
		};

		string jsonBody = JsonSerializer.Serialize(new
		{
			userId = userId,
			codigo = codigo
		});

		string[] headers = { "Content-Type: application/json" };
		Error err = http.Request(ApiConfig.CodigosCanjear, headers, HttpClient.Method.Post, jsonBody);
		if (err != Error.Ok)
		{
			http.QueueFree();
			return new ResultadoCanje
			{
				Tipo = TipoResultado.Erroneo,
				Mensaje = "Error al intentar conectar con el servidor."
			};
		}

		var (result, responseCode, respBody) = await tcs.Task;
		http.QueueFree();

		if (result != (long)HttpRequest.Result.Success)
		{
			return new ResultadoCanje
			{
				Tipo = TipoResultado.Erroneo,
				Mensaje = "No se pudo comunicar con el servidor."
			};
		}

		string respuestaJson = Encoding.UTF8.GetString(respBody);
		try
		{
			using var doc = JsonDocument.Parse(respuestaJson);
			var root = doc.RootElement;

			if (responseCode == 200)
			{
				string tipo = root.TryGetProperty("tipo", out var pTipo) ? pTipo.GetString() ?? "" : "";
				string valor = root.TryGetProperty("valor", out var pVal) ? pVal.GetString() ?? "" : "";
				string nombre = root.TryGetProperty("nombre", out var pNom) ? pNom.GetString() ?? "" : "";
				string msg = root.TryGetProperty("mensaje", out var pMsg) ? pMsg.GetString() ?? "¡Código canjeado!" : "¡Código canjeado!";

				int monedas = 0;
				if (tipo == "skin")
				{
					Preferencias.DesbloquearSkinExclusiva(valor);
				}
				else if (tipo == "monedas" && int.TryParse(valor, out monedas))
				{
					Economia.Instancia()?.Agregar(monedas);
				}

				return new ResultadoCanje
				{
					Tipo = TipoResultado.Canjeado,
					Mensaje = msg,
					TipoRecompensa = tipo,
					ValorRecompensa = valor,
					NombreRecompensa = nombre,
					Monedas = monedas
				};
			}
			else if (responseCode == 409)
			{
				string msg = root.TryGetProperty("message", out var pMsg) ? pMsg.GetString() ?? "Código ya usado." : "Código ya usado.";
				return new ResultadoCanje { Tipo = TipoResultado.YaUsado, Mensaje = msg };
			}
			else
			{
				string msg = root.TryGetProperty("message", out var pMsg) ? pMsg.GetString() ?? "Código no válido." : "Código no válido.";
				return new ResultadoCanje { Tipo = TipoResultado.Erroneo, Mensaje = msg };
			}
		}
		catch
		{
			return new ResultadoCanje { Tipo = TipoResultado.Erroneo, Mensaje = "Respuesta del servidor no válida." };
		}
	}
}
