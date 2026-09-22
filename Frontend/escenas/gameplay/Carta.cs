using Godot;
using System;

public partial class Carta : Control
{
	public bool EstaEnMano = true;
	public bool EstaArrastrando = false;

	public PackedScene EscenaTropa;
	public string NombreSpot = "";
	public int IdCarta;

	// Cuando EsHechizo=true esta misma carta representa un hechizo/ardid (Curación, Robar Carta,
	// Veneno, Bloqueo, Encebollado) en vez de una tropa — mismo visual y mismas reglas de
	// hover/arrastre que las cartas de tropa, pero al soltar resuelve contra Campo1 en vez de
	// invocar. Ver Campo1.HUD.cs (creación) y Campo1.Hechizos.cs / Campo1.RoboCarta.cs (resolución).
	public bool EsHechizo      = false;
	public int  HechizoPi      = -1; // 0=curación,1=robar,2=veneno,3=bloqueo,4=encebollado
	public int  HechizoSlotIdx = -1; // 0 o 1

	private Vector2 _posicionOriginal;
	private float   _rotacionOriginal;
	private bool _bloqueada = false;

	/// <summary>Bloquea/desbloquea la carta desde el juego (modo sacrificio): queda semitransparente y
	/// no responde a toques, pero se sigue viendo.</summary>
	public void BloquearPorModo(bool bloquear)
	{
		_bloqueada  = bloquear;
		MouseFilter = bloquear ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
		Modulate    = bloquear ? new Color(1, 1, 1, 0.35f) : Colors.White;
		if (bloquear && EstaArrastrando) CancelarArrastre();
	}
	private Vector2 _offsetMouse;
	private Tween   _tweenAnim;

	// Antes era un valor fijo (0.72) que no coincidía con el 1.05 que Campo1 le asigna al crear
	// la carta — por eso, al soltar el hover o fallar un arrastre, la carta "encogía" a un
	// tamaño distinto al que tenía apenas apareció. Ahora se captura dinámicamente en
	// GuardarEstadoOriginal() el tamaño REAL con el que la carta ya quedó puesta, así "volver a
	// la normal" siempre significa volver a como se veía al principio, sin importar la acción.
	private Vector2 _escalaNormalMano  = Vector2.One;
	private Vector2 _escalaHover       = new Vector2(1.8f, 1.8f);   // GRANDE al pasar dedo / doble toque
	// Chica al arrastrar, para ver el carril debajo — pero NO exagerada (antes 0.45 se sentía
	// "demasiado pequeña" e interrumpía la lectura visual del tablero).
	private Vector2 _escalaAlArrastrar = new Vector2(0.68f, 0.68f);
	private bool    _achicadaPorOtra   = false; // true mientras OTRA carta de la mano está en hover/arrastre

	public override void _Ready()
	{
		PivotOffset = Size / 2;
		MouseFilter = MouseFilterEnum.Stop;
	}

	public void GuardarEstadoOriginal()
	{
		_posicionOriginal = Position;
		_rotacionOriginal = Rotation;
		_escalaNormalMano = Scale; // tamaño real ya asignado por Campo1 al crear la carta
	}

	// Cambia el "hogar" de esta carta (posición/rotación/escala a las que vuelve tras soltar o
	// perder el hover) y la anima hasta ahí — usado por Campo1 para reacomodar la mano cuando
	// pasa de 3 a 4 cartas (robo) o de vuelta a 3 (ver ReacomodarManoTropas). No interrumpe un
	// gesto en curso: si se está arrastrando, solo actualiza el destino para más adelante.
	public void ReubicarEnMano(Vector2 nuevaPosicionLocal, Vector2 nuevaEscala, float nuevaRotacion, float duracion = 0.25f)
	{
		_posicionOriginal = nuevaPosicionLocal;
		_escalaNormalMano  = nuevaEscala;
		_rotacionOriginal  = nuevaRotacion;
		if (EstaArrastrando || _achicadaPorOtra) return;
		_tweenAnim?.Kill();
		_tweenAnim = CreateTween().SetParallel(true);
		_tweenAnim.TweenProperty(this, "position", nuevaPosicionLocal, duracion);
		_tweenAnim.TweenProperty(this, "scale", nuevaEscala, duracion);
		_tweenAnim.TweenProperty(this, "rotation", nuevaRotacion, duracion);
	}

	public void AsignarDatos(string rutaImg, string rutaTropa, int id)
	{
		IdCarta = id;
		var foto = GetNodeOrNull<TextureRect>("foto");
		if (foto != null && ResourceLoader.Exists(rutaImg))
		{
			foto.Texture = GD.Load<Texture2D>(rutaImg);
			foto.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			foto.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		}
		if (ResourceLoader.Exists(rutaTropa))
			EscenaTropa = GD.Load<PackedScene>(rutaTropa);
		else
			GD.PrintErr($"[Carta] Escena de tropa no encontrada: {rutaTropa}");
	}

	public void AsignarDatosHechizo(string rutaImg, int pi, int slotIdx)
	{
		EsHechizo      = true;
		HechizoPi      = pi;
		HechizoSlotIdx = slotIdx;
		var foto = GetNodeOrNull<TextureRect>("foto");
		if (foto != null && ResourceLoader.Exists(rutaImg))
		{
			foto.Texture = GD.Load<Texture2D>(rutaImg);
			foto.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			foto.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		// 1. BUSCAMOS EL CAMPO PARA VALIDAR TURNO
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;

		// BLOQUEO SI EL JUEGO TERMINÓ O SI NO ES MI TURNO O NO HAY MOVIMIENTOS — pero SOLO para empezar
		// a arrastrar. Una carta que YA se está arrastrando siempre procesa el soltar/mover: antes, si
		// el turno terminaba en pleno arrastre, el "soltar" se ignoraba y la carta quedaba flotando para
		// siempre, y las demás cartas (achicadas mientras dura un arrastre) no volvían a responder.
		bool puedeJugar = campo == null
			|| !(campo.juegoTerminado || campo.IntroEnCurso || !campo.esTurnoJugador || campo.movimientosRestantes <= 0);
		if (!puedeJugar && !EstaArrastrando) return;

		if (!EstaEnMano || _bloqueada) { if (EstaArrastrando) CancelarArrastre(); return; }

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				if (EsHechizo)
				{
					// El botón "CAMBIAR" consume el toque en vez de iniciar un arrastre; y si el
					// hechizo ya se usó o no es nuestro turno, tampoco se puede arrastrar.
					if (campo == null || !campo.IntentarIniciarArrastreHechizo(HechizoSlotIdx)) return;
				}
				EstaArrastrando = true;
				// Si el toque llega en pleno hover, PivotOffset todavía está en el punto "crece
				// hacia arriba" (Size.X*0.5, Size.Y) en vez del centro — si se achica alrededor de
				// ese pivote en vez del centro, la carta se ve "correr" lejos del dedo/mouse
				// mientras se encoge. Se fuerza el pivote al centro ANTES de calcular el offset y
				// de animar la escala, para que el achicado sea siempre alrededor del centro real
				// (igual en mouse y en touch, que no dispara mouse_exited de forma confiable).
				PivotOffset = Size / 2;
				_offsetMouse = GetGlobalMousePosition() - GlobalPosition;
				ZIndex = 200;
				_tweenAnim?.Kill();
				_tweenAnim = CreateTween();
				_tweenAnim.TweenProperty(this, "scale", _escalaAlArrastrar, 0.1f);
				NotificarHermanas(true);
			}
			else if (EstaArrastrando)
			{
				// Se soltó cuando ya no se puede jugar (se acabó el turno, etc.): vuelve a su lugar.
				if (!puedeJugar) { CancelarArrastre(); return; }
				EstaArrastrando = false;
				if (EsHechizo) campo?.FinalizarArrastreHechizo();
				VerificarSoltado();
			}
		}

		if (@event is InputEventMouseMotion mm && EstaArrastrando)
		{
			GlobalPosition = GetGlobalMousePosition() - _offsetMouse;
			_segundosSinMover = 0f; // se sigue moviendo: no la devolvemos
		}
	}

	// Si la carta queda agarrada y quieta (no se suelta ni se mueve), a 1.5s vuelve sola a su lugar
	// con la misma animación de regreso de siempre.
	private const float SEGUNDOS_PARA_REGRESAR = 1.5f;
	private float _segundosSinMover = 0f;

	public override void _Process(double delta)
	{
		if (!EstaArrastrando) { _segundosSinMover = 0f; return; }
		_segundosSinMover += (float)delta;
		if (_segundosSinMover >= SEGUNDOS_PARA_REGRESAR) CancelarArrastre();
	}

	private void VerificarSoltado()
	{
		if (EsHechizo) { VerificarSoltadoHechizo(); return; }

		var zonas = GetTree().GetNodesInGroup("zonas_invocacion");
		bool exito = false;
		Vector2 centroCarta = GlobalPosition + Size / 2;
		foreach (Node2D puntoMod in zonas)
		{
			if (centroCarta.DistanceTo(puntoMod.GlobalPosition) < 180)
			{
				if (puntoMod.GetNodeOrNull("Ocupado") == null)
				{
					exito = ConfirmarInvocacion(puntoMod);
					break;
				}
			}
		}
		if (!exito) RegresarAMano();
	}

	private void VerificarSoltadoHechizo()
	{
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		bool exito = campo != null && campo.ResolverSueltaHechizoDesdeCarta(HechizoSlotIdx, HechizoPi);
		if (exito)
		{
			// Éxito: esta carta se gastó. Nunca debe quedar "pegada" junto al objetivo — Campo1 ya
			// creó (o no) la siguiente carta de ese slot en su posición correcta; esta se libera.
			EstaEnMano = false;
			NotificarHermanas(false);
			QueueFree();
		}
		else
		{
			RegresarAMano();
		}
	}

	private bool ConfirmarInvocacion(Node2D puntoMod)
	{
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		bool colocada = campo != null && campo.TropaInvocada(puntoMod, EscenaTropa, IdCarta);
		if (colocada)
		{
			EstaEnMano = false;
			QueueFree();
		}
		NotificarHermanas(false);
		return colocada;
	}

	/// <summary>Corta un arrastre en curso y devuelve la carta a su lugar en la mano (y desbloquea a las
	/// demás cartas). Campo1 lo llama al terminar el turno.</summary>
	public void CancelarArrastre()
	{
		if (!EstaArrastrando) return;
		EstaArrastrando = false;
		if (EsHechizo) (GetTree()?.Root.FindChild("Campo1", true, false) as Campo1)?.FinalizarArrastreHechizo();
		RegresarAMano();
	}

	// Si la carta desaparece en pleno arrastre (p. ej. la bomba Nuclear destruye la mano), las hermanas
	// no pueden quedar achicadas/bloqueadas esperando un "soltar" que nunca va a llegar.
	public override void _ExitTree()
	{
		if (EstaArrastrando) { EstaArrastrando = false; NotificarHermanas(false); }
	}

	private void RegresarAMano()
	{
		ZIndex = 1;
		_tweenAnim?.Kill();
		_tweenAnim = CreateTween().SetParallel(true);
		_tweenAnim.TweenProperty(this, "position", _posicionOriginal, 0.2f);
		_tweenAnim.TweenProperty(this, "scale", _escalaNormalMano, 0.2f);
		_tweenAnim.TweenProperty(this, "rotation", _rotacionOriginal, 0.2f);
		NotificarHermanas(false);
	}

	public void _on_mouse_entered()
	{
		var campo = GetTree().Root.FindChild("Campo1", true, false) as Campo1;
		if (campo != null && (!campo.esTurnoJugador || campo.movimientosRestantes <= 0)) return;

		if (!EstaEnMano || EstaArrastrando || _bloqueada) return;
		ZIndex = 150;
		Vector2 pivotAntes = PivotOffset;
		PivotOffset = new Vector2(Size.X * 0.5f, Size.Y);   // crece hacia arriba (no tapa las otras)
		_tweenAnim?.Kill();
		_tweenAnim = CreateTween().SetParallel(true);
		_tweenAnim.TweenProperty(this, "scale", _escalaHover, 0.12f)
		 .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tweenAnim.TweenProperty(this, "rotation", 0.0f, 0.12f);
		NotificarHermanas(true);
	}

	public void _on_mouse_exited()
	{
		if (!EstaArrastrando)
		{
			ZIndex = 1;
			_tweenAnim?.Kill();
			_tweenAnim = CreateTween().SetParallel(true);
			_tweenAnim.TweenProperty(this, "scale", _escalaNormalMano, 0.12f);
			_tweenAnim.TweenProperty(this, "rotation", _rotacionOriginal, 0.12f);
			_tweenAnim.Finished += () => { if (IsInstanceValid(this)) PivotOffset = Size / 2; };
			NotificarHermanas(false);
		}
	}

	// Mientras esta carta está en hover/arrastre, las OTRAS cartas de la mano se achican (mismo
	// tamaño "chico" que al arrastrar) para que quede claro cuál está seleccionada; al soltar
	// vuelven siempre a su tamaño original real (ver GuardarEstadoOriginal).
	private void NotificarHermanas(bool achicar)
	{
		Node padre = GetParent();
		if (padre == null) return;
		foreach (Node n in padre.GetChildren())
			if (n is Carta hermana && hermana != this && IsInstanceValid(hermana))
				hermana.RecibirSeleccionAjena(achicar);
	}

	public void RecibirSeleccionAjena(bool achicar)
	{
		if (achicar)
		{
			if (!EstaEnMano || EstaArrastrando || _bloqueada || _achicadaPorOtra) return;
			_achicadaPorOtra = true;
			_tweenAnim?.Kill();
			_tweenAnim = CreateTween().SetParallel(true);
			_tweenAnim.TweenProperty(this, "scale", _escalaAlArrastrar, 0.12f);
		}
		else
		{
			if (!_achicadaPorOtra) return;
			_achicadaPorOtra = false;
			if (EstaArrastrando) return;
			_tweenAnim?.Kill();
			_tweenAnim = CreateTween().SetParallel(true);
			// Volver también a Position/Rotation (no solo Scale): mientras esta carta estaba achicada
			// por el hover de una hermana, ReubicarEnMano pudo haber reacomodado toda la mano (p. ej.
			// al jugarse otra carta) — actualiza el destino guardado pero, por estar achicada, SE
			// SALTABA la animación de posición (solo tocaba Scale acá). La carta quedaba con el tamaño
			// correcto pero en el lugar VIEJO: eso era el desorden al mirar la mano para elegir.
			_tweenAnim.TweenProperty(this, "scale", _escalaNormalMano, 0.12f);
			_tweenAnim.TweenProperty(this, "position", _posicionOriginal, 0.12f);
			_tweenAnim.TweenProperty(this, "rotation", _rotacionOriginal, 0.12f);
		}
	}
}
