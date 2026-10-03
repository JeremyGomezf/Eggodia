using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Guía por pasos para explicar una pantalla la primera vez que se abre (menú, tienda, mazo, ajustes,
/// pausa contra el bot, retirada en línea).
///
/// En cada paso, lo que se explica queda NÍTIDO y con un aro dorado que late (enfoque) y todo lo demás
/// se difumina y oscurece (desenfoque). El foco viaja suave de un paso al siguiente. El texto va en el
/// mismo cuadro "INF" del tutorial (efectos/guiatexto.tscn), con su misma animación de chico a grande,
/// y se avanza tocando cualquier parte de la pantalla, igual que en el tutorial.
///
/// Mientras está abierta se come todos los toques (y el botón "atrás"), así detrás no se compra, se
/// juega ni se navega nada por accidente. Cada guía se ve una sola vez por aparato
/// (Preferencias.GuiaVista): se marca como vista al terminarla.
/// </summary>
public partial class GuiaPasos : CanvasLayer
{
	public sealed class Paso
	{
		/// <summary>Zona a enfocar, en coordenadas de pantalla. Se consulta en cada cuadro, así el foco
		/// sigue a lo que se mueva (listas que se desplazan, paneles que se acomodan). null = sin foco.</summary>
		public readonly Func<Rect2?> Foco;
		public readonly string Texto;
		/// <summary>Se ejecuta al empezar el paso (p. ej. abrir el menú de pausa o desplazar una lista).</summary>
		public readonly Action AlEntrar;

		public Paso(string texto, Func<Rect2?> foco = null, Action alEntrar = null)
		{
			Texto = texto; Foco = foco; AlEntrar = alEntrar;
		}
	}

	public const string GRUPO = "guia_pasos";

	private const string RUTA_CUADRO = "res://efectos/guiatexto.tscn";
	// El cuadro del tutorial mide 631×228; así escalado la letra (22) queda en ~30 px, cómoda en celular.
	private static readonly Vector2 ESCALA_CUADRO = new(1.35f, 1.35f);
	// Límites del dibujo del cuadro dentro de su propio nodo (el Sprite2D centrado en 315,117).
	private static readonly Rect2 RECT_CUADRO_LOCAL = new(0f, 3f, 631f, 228f);
	private const float MARGEN_PANTALLA = 24f;
	private const float SEPARACION_FOCO = 26f;
	private const float ALTO_PISTA = 44f;
	// Toques que llegan apenas cambió el paso no cuentan: un doble toque no se salta un texto sin leerlo.
	private const ulong ESPERA_MIN_MS = 450;

	private string _clave;
	private IReadOnlyList<Paso> _pasos;
	private Action _alTerminar;
	private bool _pausarJuego;

	/// <summary>Si devuelve true, la guía se cierra sola SIN marcarse como vista (p. ej. la partida
	/// terminó mientras se explicaba): se volverá a mostrar la próxima vez.</summary>
	public Func<bool> CerrarSi;

	private int _indice = -1;
	private ulong _inicioPasoMs;
	private bool _terminando;

	private ColorRect _velo;
	private ShaderMaterial _material;
	private Node2D _cuadro;
	private Label _lblTexto;
	private Label _lblPista;
	private Tween _tweenCuadro;

	private Rect2 _focoActual;
	private bool _hayFoco;
	private float _fuerza; // 0 = pantalla normal, 1 = desenfoque completo alrededor del foco

	/// <summary>Abre la guía si este aparato todavía no la vio (y no hay otra abierta). Devuelve null si
	/// no se abrió. Con <paramref name="pausarJuego"/> el árbol queda en pausa mientras dura; al terminar
	/// se llama a <paramref name="alTerminar"/>, que decide si se reanuda (si no hay callback, se reanuda).</summary>
	public static GuiaPasos Iniciar(Node padre, string clave, IReadOnlyList<Paso> pasos,
		Action alTerminar = null, bool pausarJuego = false)
	{
		if (padre == null || !IsInstanceValid(padre) || !padre.IsInsideTree() || pasos == null || pasos.Count == 0)
			return null;
		if (!string.IsNullOrEmpty(clave) && Preferencias.GuiaVista(clave)) return null;
		if (HayUnaAbierta(padre.GetTree())) return null;

		var guia = new GuiaPasos
		{
			Name = "GuiaPasos_" + clave,
			_clave = clave,
			_pasos = pasos,
			_alTerminar = alTerminar,
			_pausarJuego = pausarJuego,
		};
		padre.AddChild(guia);
		return guia;
	}

	public static bool HayUnaAbierta(SceneTree arbol) => arbol != null && arbol.GetNodesInGroup(GRUPO).Count > 0;

	/// <summary>Rectángulo en pantalla de un nodo visible (Control, Sprite2D o AnimatedSprite2D),
	/// agrandado un margen para que el aro no quede pegado. En los botones e imágenes se ciñe a lo que
	/// de verdad está dibujado (sin los bordes transparentes de la imagen ni el espacio sobrante de la
	/// caja). null si el nodo no existe o no se ve.</summary>
	public static Rect2? RectDe(Node nodo, float margen = 14f)
	{
		if (nodo == null || !IsInstanceValid(nodo) || nodo is not CanvasItem ci || !ci.IsVisibleInTree()) return null;
		Rect2 local;
		if (nodo is TextureButton tb && tb.TextureNormal != null)
			local = RectDibujado(tb.TextureNormal, tb.Size, ModoDe(tb.StretchMode));
		else if (nodo is TextureRect tr && tr.Texture != null)
			local = RectDibujado(tr.Texture, tr.Size, ModoDe(tr.StretchMode));
		else if (nodo is Control c) local = new Rect2(Vector2.Zero, c.Size);
		else if (nodo is Sprite2D s) local = s.GetRect();
		else if (nodo is AnimatedSprite2D anim && anim.SpriteFrames?.GetFrameTexture(anim.Animation, anim.Frame) is Texture2D tex)
		{
			Vector2 tam = tex.GetSize();
			local = new Rect2((anim.Centered ? -tam / 2f : Vector2.Zero) + anim.Offset, tam);
		}
		else return null;

		Transform2D t = ci.GetGlobalTransformWithCanvas();
		Vector2 a = t * local.Position;
		var r = new Rect2(a, Vector2.Zero)
			.Expand(t * (local.Position + new Vector2(local.Size.X, 0f)))
			.Expand(t * (local.Position + new Vector2(0f, local.Size.Y)))
			.Expand(t * local.End);
		return r.Grow(margen);
	}

	private enum Ajuste { Estirar, Original, OriginalCentrado, Proporcion, ProporcionCentrada, Cubrir }

	private static Ajuste ModoDe(TextureButton.StretchModeEnum m) => m switch
	{
		TextureButton.StretchModeEnum.Keep               => Ajuste.Original,
		TextureButton.StretchModeEnum.KeepCentered       => Ajuste.OriginalCentrado,
		TextureButton.StretchModeEnum.KeepAspect         => Ajuste.Proporcion,
		TextureButton.StretchModeEnum.KeepAspectCentered => Ajuste.ProporcionCentrada,
		TextureButton.StretchModeEnum.KeepAspectCovered  => Ajuste.Cubrir,
		_                                                => Ajuste.Estirar,
	};

	private static Ajuste ModoDe(TextureRect.StretchModeEnum m) => m switch
	{
		TextureRect.StretchModeEnum.Keep               => Ajuste.Original,
		TextureRect.StretchModeEnum.KeepCentered       => Ajuste.OriginalCentrado,
		TextureRect.StretchModeEnum.KeepAspect         => Ajuste.Proporcion,
		TextureRect.StretchModeEnum.KeepAspectCentered => Ajuste.ProporcionCentrada,
		TextureRect.StretchModeEnum.KeepAspectCovered  => Ajuste.Cubrir,
		_                                              => Ajuste.Estirar,
	};

	/// <summary>Dónde cae, dentro de la caja del nodo, la parte NO transparente de la imagen.</summary>
	private static Rect2 RectDibujado(Texture2D tex, Vector2 caja, Ajuste ajuste)
	{
		Vector2 tamTex = tex.GetSize();
		if (tamTex.X <= 0 || tamTex.Y <= 0) return new Rect2(Vector2.Zero, caja);
		Vector2 pos = Vector2.Zero, tam = caja;
		switch (ajuste)
		{
			case Ajuste.Original: tam = tamTex; break;
			case Ajuste.OriginalCentrado: tam = tamTex; pos = (caja - tamTex) / 2f; break;
			case Ajuste.Proporcion:
			case Ajuste.ProporcionCentrada:
			case Ajuste.Cubrir:
				float k = ajuste == Ajuste.Cubrir
					? Mathf.Max(caja.X / tamTex.X, caja.Y / tamTex.Y)
					: Mathf.Min(caja.X / tamTex.X, caja.Y / tamTex.Y);
				tam = tamTex * k;
				if (ajuste != Ajuste.Proporcion) pos = (caja - tam) / 2f;
				break;
		}
		Rect2 usado = ZonaVisible(tex);
		Vector2 escala = tam / tamTex;
		var r = new Rect2(pos + usado.Position * escala, usado.Size * escala);
		return r.Intersection(new Rect2(Vector2.Zero, caja)) is Rect2 dentro && dentro.HasArea() ? dentro : r;
	}

	// Zona no transparente de cada imagen (se calcula una vez por imagen).
	private static readonly Dictionary<ulong, Rect2> _zonasVisibles = new();

	private static Rect2 ZonaVisible(Texture2D tex)
	{
		var completa = new Rect2(Vector2.Zero, tex.GetSize());
		ulong id = tex.GetInstanceId();
		if (_zonasVisibles.TryGetValue(id, out Rect2 guardada)) return guardada;
		Rect2 zona = completa;
		try
		{
			Image img = tex.GetImage();
			if (img != null && img.IsCompressed()) img.Decompress();
			if (img != null && !img.IsCompressed() && img.GetWidth() > 0)
			{
				Rect2I usado = img.GetUsedRect();
				if (usado.HasArea())
				{
					// Por si la imagen guardada no tiene el mismo tamaño que la textura (mipmaps, escalas).
					Vector2 k = tex.GetSize() / new Vector2(img.GetWidth(), img.GetHeight());
					zona = new Rect2(usado.Position * k, usado.Size * k);
				}
			}
		}
		catch (Exception) { zona = completa; }
		_zonasVisibles[id] = zona;
		return zona;
	}

	/// <summary>Rectángulo que abarca a todos los nodos visibles de la lista.</summary>
	public static Rect2? RectDeVarios(float margen, params Node[] nodos)
	{
		Rect2? total = null;
		foreach (var n in nodos)
		{
			Rect2? r = RectDe(n, margen);
			if (!r.HasValue) continue;
			total = total.HasValue ? total.Value.Merge(r.Value) : r.Value;
		}
		return total;
	}

	public override void _Ready()
	{
		Layer = 900; // por encima de todo (pausa 100, avisos 300, tutorial 500/600)
		ProcessMode = ProcessModeEnum.Always; // sigue funcionando con el juego en pausa
		AddToGroup(GRUPO);

		_velo = new ColorRect { Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Stop };
		_velo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_material = new ShaderMaterial { Shader = ShaderFoco() };
		_material.SetShaderParameter("fuerza", 0f);
		_velo.Material = _material;
		AddChild(_velo);

		_cuadro = GD.Load<PackedScene>(RUTA_CUADRO)?.Instantiate<Node2D>();
		if (_cuadro != null)
		{
			AddChild(_cuadro);
			_lblTexto = _cuadro.FindChild("LblHabilidad", true, false) as Label;
			if (_lblTexto != null) _lblTexto.Text = "";
			_cuadro.Visible = false;
		}

		_lblPista = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		EstiloUI.Texto(_lblPista, 28, EstiloUI.TextoClaro);
		_lblPista.AddThemeConstantOverride("outline_size", 10);
		_lblPista.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		AddChild(_lblPista);

		if (_pausarJuego) GetTree().Paused = true;

		var entrada = CreateTween();
		entrada.TweenMethod(Callable.From<float>(v => _fuerza = v), 0f, 1f, 0.35f);
		entrada.Parallel().TweenProperty(_lblPista, "modulate:a", 1f, 0.35f);

		Avanzar();
	}

	public override void _Process(double delta)
	{
		// Si algo reanudó el juego por fuera (p. ej. el menú de pausa al cerrarse), se vuelve a pausar:
		// mientras la guía está abierta el reloj no puede correr.
		if (_pausarJuego && !_terminando && !GetTree().Paused) GetTree().Paused = true;
		if (!_terminando && CerrarSi != null && CerrarSi()) { Terminar(marcarVista: false); return; }

		Vector2 tamPantalla = GetViewport().GetVisibleRect().Size;
		Rect2? objetivo = FocoDelPasoActual();
		if (objetivo.HasValue)
		{
			// El foco arranca abarcando toda la pantalla y se va cerrando sobre lo que se explica; entre
			// pasos viaja suave de una zona a la otra.
			if (!_hayFoco) { _focoActual = new Rect2(Vector2.Zero, tamPantalla); _hayFoco = true; }
			float k = 1f - Mathf.Exp(-12f * (float)delta);
			_focoActual = new Rect2(_focoActual.Position.Lerp(objetivo.Value.Position, k),
				_focoActual.Size.Lerp(objetivo.Value.Size, k));
		}
		else _hayFoco = false;

		_material.SetShaderParameter("tam", tamPantalla);
		_material.SetShaderParameter("foco", _hayFoco
			? new Vector4(_focoActual.Position.X, _focoActual.Position.Y, _focoActual.Size.X, _focoActual.Size.Y)
			: new Vector4(0f, 0f, -1f, -1f));
		_material.SetShaderParameter("fuerza", _fuerza);

		// Lo que se enfoca puede acomodarse un instante después de entrar al paso (una lista que se
		// desplaza, un panel que se abre): durante ese primer momento el cuadro se reubica.
		if (!_terminando && Time.GetTicksMsec() - _inicioPasoMs < 300) UbicarCuadro(objetivo, tamPantalla);
	}

	public override void _Input(InputEvent e)
	{
		bool atras = e.IsActionPressed("ui_cancel");
		bool puntero = e is InputEventMouseButton || e is InputEventMouseMotion
			|| e is InputEventScreenTouch || e is InputEventScreenDrag;
		if (!atras && !puntero) return;

		// Nada pasa a lo que hay detrás mientras la guía está abierta.
		GetViewport().SetInputAsHandled();
		if (_terminando) return;

		// En el celular cada toque llega también como clic emulado: se avanza solo con el clic, así un
		// toque no cuenta doble.
		bool toque = e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left;
		if (!toque && !atras) return;
		if (Time.GetTicksMsec() - _inicioPasoMs < ESPERA_MIN_MS) return;

		SonidoUI.Reproducir(this);
		Avanzar();
	}

	private Rect2? FocoDelPasoActual()
	{
		if (_pasos == null || _indice < 0 || _indice >= _pasos.Count || _pasos[_indice].Foco == null) return null;
		try { return _pasos[_indice].Foco(); }
		catch (Exception ex) { GD.PushWarning($"[GuiaPasos] foco del paso {_indice}: {ex.Message}"); return null; }
	}

	private void Avanzar()
	{
		_indice++;
		if (_indice >= _pasos.Count) { Terminar(); return; }

		var paso = _pasos[_indice];
		try { paso.AlEntrar?.Invoke(); }
		catch (Exception ex) { GD.PushWarning($"[GuiaPasos] al entrar al paso {_indice}: {ex.Message}"); }

		_inicioPasoMs = Time.GetTicksMsec();
		_lblPista.Text = _pasos.Count > 1
			? $"{_indice + 1}/{_pasos.Count}   ·   Toca para continuar"
			: "Toca para continuar";
		MostrarCuadro(paso.Texto);
	}

	/// <summary>Mismo efecto del cuadro del tutorial (MostrarGuiaConTexto): aparece de chico a grande.</summary>
	private void MostrarCuadro(string texto)
	{
		if (_cuadro == null) return;
		if (_lblTexto != null) _lblTexto.Text = texto;
		_cuadro.Visible = true;
		UbicarCuadro(FocoDelPasoActual(), GetViewport().GetVisibleRect().Size);
		_cuadro.Scale = ESCALA_CUADRO * 0.15f;
		_tweenCuadro?.Kill();
		_tweenCuadro = CreateTween();
		_tweenCuadro.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tweenCuadro.TweenProperty(_cuadro, "scale", ESCALA_CUADRO, 0.35f);
	}

	/// <summary>Pone el cuadro (y la pista de abajo) donde no tape lo enfocado: debajo o encima si hay
	/// lugar; si lo enfocado es muy alto, a un costado. Siempre dentro de la pantalla.</summary>
	private void UbicarCuadro(Rect2? foco, Vector2 pantalla)
	{
		if (_cuadro == null) return;
		Vector2 tam = RECT_CUADRO_LOCAL.Size * ESCALA_CUADRO;
		float altoTotal = tam.Y + ALTO_PISTA;
		float m = MARGEN_PANTALLA;

		Vector2 Encajar(Vector2 p) => new(
			Mathf.Clamp(p.X, m, Mathf.Max(m, pantalla.X - m - tam.X)),
			Mathf.Clamp(p.Y, m, Mathf.Max(m, pantalla.Y - m - altoTotal)));

		Vector2 pos;
		if (!foco.HasValue)
		{
			pos = new Vector2((pantalla.X - tam.X) / 2f, (pantalla.Y - altoTotal) / 2f);
		}
		else
		{
			Rect2 f = foco.Value;
			float cx = f.GetCenter().X - tam.X / 2f;
			float cy = f.GetCenter().Y - tam.Y / 2f;
			var candidatos = new List<Vector2>();
			Vector2 abajo   = new(cx, f.End.Y + SEPARACION_FOCO);
			Vector2 arriba  = new(cx, f.Position.Y - SEPARACION_FOCO - altoTotal);
			Vector2 derecha = new(f.End.X + SEPARACION_FOCO, cy);
			Vector2 izq     = new(f.Position.X - SEPARACION_FOCO - tam.X, cy);
			bool alto = f.Size.Y > pantalla.Y * 0.45f;
			bool enMitadDeArriba = f.GetCenter().Y < pantalla.Y / 2f;
			if (alto) candidatos.AddRange(new[] { derecha, izq, abajo, arriba });
			else if (enMitadDeArriba) candidatos.AddRange(new[] { abajo, arriba, derecha, izq });
			else candidatos.AddRange(new[] { arriba, abajo, derecha, izq });

			pos = Encajar(candidatos[0]);
			bool encontrado = false;
			foreach (var c in candidatos)
			{
				Vector2 p = Encajar(c);
				var r = new Rect2(p, new Vector2(tam.X, altoTotal));
				if (!r.Intersects(f)) { pos = p; encontrado = true; break; }
			}
			// Ni abajo, ni arriba, ni a los costados: abajo de todo, tapando lo menos posible.
			if (!encontrado) pos = Encajar(new Vector2(cx, pantalla.Y - m - altoTotal));
		}

		// El nodo del cuadro tiene su origen arriba a la izquierda del dibujo (menos el borde de 3 px).
		_cuadro.Position = pos - RECT_CUADRO_LOCAL.Position * ESCALA_CUADRO;
		_lblPista.Position = new Vector2(pos.X, pos.Y + tam.Y + 2f);
		_lblPista.Size = new Vector2(tam.X, ALTO_PISTA);
	}

	private void Terminar(bool marcarVista = true)
	{
		if (_terminando) return;
		_terminando = true;
		if (marcarVista && !string.IsNullOrEmpty(_clave)) Preferencias.MarcarGuiaVista(_clave);

		_tweenCuadro?.Kill();
		var salida = CreateTween();
		salida.SetParallel();
		salida.TweenMethod(Callable.From<float>(v => _fuerza = v), _fuerza, 0f, 0.3f);
		salida.TweenProperty(_lblPista, "modulate:a", 0f, 0.2f);
		if (_cuadro != null)
			salida.TweenProperty(_cuadro, "scale", ESCALA_CUADRO * 0.15f, 0.2f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		salida.Chain().TweenCallback(Callable.From(() =>
		{
			if (_alTerminar != null) _alTerminar();
			else if (_pausarJuego) GetTree().Paused = false;
			QueueFree();
		}));
	}

	// ── SHADER: enfoque nítido + desenfoque oscuro alrededor + aro dorado que late ───────────────────
	private static Shader _shaderFoco;

	private static Shader ShaderFoco()
	{
		if (_shaderFoco != null) return _shaderFoco;
		_shaderFoco = new Shader
		{
			Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D pantalla : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform vec2 tam = vec2(2400.0, 1080.0);
uniform vec4 foco = vec4(0.0, 0.0, -1.0, -1.0);
uniform float radio = 28.0;
uniform float fuerza = 0.0;
uniform float oscuridad = 0.5;
uniform float desenfoque = 2.5;
uniform vec4 color_aro : source_color = vec4(1.0, 0.82, 0.30, 1.0);

float caja(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - b + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

void fragment() {
	vec2 p = UV * tam;
	float d = 100000.0;
	if (foco.z > 0.0) {
		float r = min(radio, min(foco.z, foco.w) * 0.5);
		d = caja(p - (foco.xy + foco.zw * 0.5), foco.zw * 0.5, r);
	}
	float fuera = smoothstep(-1.0, 12.0, d);

	vec2 px = SCREEN_PIXEL_SIZE * 6.0;
	vec3 b = textureLod(pantalla, SCREEN_UV, desenfoque).rgb * 0.36;
	b += textureLod(pantalla, SCREEN_UV + vec2( px.x,  px.y), desenfoque).rgb * 0.16;
	b += textureLod(pantalla, SCREEN_UV + vec2(-px.x,  px.y), desenfoque).rgb * 0.16;
	b += textureLod(pantalla, SCREEN_UV + vec2( px.x, -px.y), desenfoque).rgb * 0.16;
	b += textureLod(pantalla, SCREEN_UV + vec2(-px.x, -px.y), desenfoque).rgb * 0.16;
	vec3 fondo = b * (1.0 - oscuridad);

	float latido = 0.6 + 0.4 * sin(TIME * 4.0);
	float aro = (foco.z > 0.0) ? (1.0 - smoothstep(0.0, 4.0, abs(d - 3.0))) * latido : 0.0;

	float a = max(fuera, aro) * fuerza;
	COLOR = vec4(mix(fondo, color_aro.rgb, aro), a);
}
"
		};
		return _shaderFoco;
	}
}
