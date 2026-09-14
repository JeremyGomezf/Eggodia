extends Control

# ==============================================================================
# Terminal de Invocación de Cartas Físicas (NFC / RFID / PIN) - Eggodia
# ==============================================================================

const URL_PRODUCCION = "https://enyooichat.cloud/api/cartas/claim-physical-card"
const URL_LOCAL      = "http://localhost:5289/api/cartas/claim-physical-card"

@export var api_url: String = ""

# Catálogo local de las 7 tarjetas físicas reales (5 dígitos finales)
const CATALOGO_TARJETAS_FISICAS = {
	"09764": {
		"cardId": "tiburon",
		"name": "Tiburón",
		"serie": "PACÍFICO",
		"tipo": "Asesino",
		"hp": 260,
		"atk": 320,
		"def": 180,
		"habilidad": "Mordida feroz: siguiente ataque hace x2 daño",
		"dialogue": "¡Felicidades, me has encontrado! Mis mandíbulas están listas para destrozar a tus rivales."
	},
	"09750": {
		"cardId": "calamar_gigante",
		"name": "Calamar Gigante",
		"serie": "PACÍFICO",
		"tipo": "Coloso",
		"hp": 400,
		"atk": 370,
		"def": 380,
		"habilidad": "Depredador acuático",
		"dialogue": "Emerjo desde las profundidades abisales para unirme a tus tropas."
	},
	"09393": {
		"cardId": "soldado_cartoon",
		"name": "Soldado Cartoon",
		"serie": "TOON",
		"tipo": "Asesino",
		"hp": 230,
		"atk": 220,
		"def": 170,
		"habilidad": "Fuego automático: Ráfagas continuas de metralla",
		"dialogue": "¡Reportándome al deber! ¡Listos para abrir fuego constante!"
	},
	"27058": {
		"cardId": "campero",
		"name": "Campero",
		"serie": "TOON",
		"tipo": "Táctico",
		"hp": 160,
		"atk": 350,
		"def": 110,
		"habilidad": "Camuflaje: Oculto en arbustos hasta disparar su rifle",
		"dialogue": "Blanco fijado en la mira. Nadie nos verá venir."
	},
	"26920": {
		"cardId": "granadero",
		"name": "Granadero",
		"serie": "TOON",
		"tipo": "Asesino",
		"hp": 240,
		"atk": 300,
		"def": 190,
		"habilidad": "Lanzamiento Explosivo: Granadas de daño en área",
		"dialogue": "¡Fuego en el hoyo! Mis explosivos abrirán el camino hacia la victoria."
	},
	"09388": {
		"cardId": "tanque",
		"name": "Tanque",
		"serie": "TOON",
		"tipo": "Coloso",
		"hp": 550,
		"atk": 420,
		"def": 480,
		"habilidad": "Cañonazo Blindado: Disparo pesado que derriba defensas",
		"dialogue": "¡Blindaje pesado desplegado! Aplanaremos cualquier defensa enemiga."
	},
	"27052": {
		"cardId": "kabar",
		"name": "Kabar",
		"serie": "TOON",
		"tipo": "Asesino",
		"hp": 180,
		"atk": 270,
		"def": 130,
		"habilidad": "Invisibilidad Espectral: Atraviesa enemigos y ataca por la espalda",
		"dialogue": "¿Sentiste ese frío en tu espalda?... Ahora lucho para ti."
	}
}

# Mapeo de renders completos que emergen del portal
const SPRITES_PERSONAJE = {
	"tiburon": "res://imagenes/RendersTropa/Tiburon_Menu.png",
	"calamar_gigante": "res://imagenes/RendersTropa/Calamar_Menu.png",
	"soldado_cartoon": "res://imagenes/RendersTropa/SoldadoCartoon_Menu.png",
	"campero": "res://imagenes/RendersTropa/Campero_Menu.png",
	"granadero": "res://imagenes/RendersTropa/Granadero_Menu.png",
	"tanque": "res://imagenes/RendersTropa/Tanque_Menu.png",
	"kabar": "res://imagenes/RendersTropa/Kabar_Menu.png"
}

# Mapeo de íconos/avatares en el marco de diálogo
const ICONOS_PERSONAJE = {
	"tiburon": "res://imagenes/iconos/Tiburon_icon.png",
	"calamar_gigante": "res://imagenes/iconos/Calamar_icon.png",
	"soldado_cartoon": "res://imagenes/iconos/SoldadoCA_icon.png",
	"campero": "res://imagenes/iconos/CanperoCA_icon.png",
	"granadero": "res://imagenes/iconos/GranaderoCA_icon.png",
	"tanque": "res://imagenes/iconos/TanqueCA_icon.png",
	"kabar": "res://imagenes/iconos/KabarCA_icon.png"
}

# Mapeo de fondos temáticos dinámicos según el origen de la tropa
const FONDOS_TEMATICOS = {
	"tiburon": "res://imagenes/UI_Dialogo/FondoPortalOceano.jpg",
	"calamar_gigante": "res://imagenes/UI_Dialogo/FondoPortalOceano.jpg",
	"soldado_cartoon": "res://imagenes/UI_Dialogo/FondoPortalGuerra.jpg",
	"campero": "res://imagenes/UI_Dialogo/FondoPortalGuerra.jpg",
	"granadero": "res://imagenes/UI_Dialogo/FondoPortalGuerra.jpg",
	"tanque": "res://imagenes/UI_Dialogo/FondoPortalGuerra.jpg",
	"kabar": "res://imagenes/UI_Dialogo/FondoPortalGuerra.jpg"
}

# Referencias a nodos principales
@onready var fondo_tematico: TextureRect = $FondoTematico
@onready var panel_input: PanelContainer = $PanelInput
@onready var btn_escanear_nfc: Button = $PanelInput/VBox/BtnEscanearNFC
@onready var subtitulo: Label = $PanelInput/VBox/Subtitulo
@onready var txt_codigo: LineEdit = $PanelInput/VBox/HBoxInput/TxtCodigo
@onready var btn_sincronizar: Button = $PanelInput/VBox/HBoxInput/BtnSincronizar
@onready var lbl_estado: Label = $PanelInput/VBox/LblEstado
@onready var panel_nfc_prompt: PanelContainer = $PanelNfcPrompt

@onready var contenedor_anim: Node2D = $ContenedorAnimacion
@onready var sprite_portal: Sprite2D = $ContenedorAnimacion/Portal
@onready var sprite_personaje: Sprite2D = $ContenedorAnimacion/PersonajeSprite

# Referencias a la Caja de Diálogo
@onready var caja_dialogo: Control = $CajaDialogoDDLC
@onready var avatar_box: Control = $CajaDialogoDDLC/Margin/HBox/AvatarBox
@onready var avatar_personaje: TextureRect = $CajaDialogoDDLC/Margin/HBox/AvatarBox/AvatarTexture
@onready var lbl_nombre_personaje: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxTop/NamePanel/Center/LblNombre
@onready var badge_serie: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxTop/BadgeSerie/Margin/LblSerie
@onready var badge_tipo: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxTop/BadgeTipo/Margin/LblTipo
@onready var txt_dialogo: RichTextLabel = $CajaDialogoDDLC/Margin/HBox/VBox/TxtDialogo
@onready var lbl_stats: Label = $CajaDialogoDDLC/LblStats
@onready var lbl_hp: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxBottom/HBoxStats/BadgeHP/Margin/HBoxHP/LblHP
@onready var lbl_atk: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxBottom/HBoxStats/BadgeATK/Margin/HBoxATK/LblATK
@onready var lbl_def: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxBottom/HBoxStats/BadgeDEF/Margin/HBoxDEF/LblDEF
@onready var lbl_hab: Label = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxBottom/HBoxStats/BadgeHab/Margin/LblHab
@onready var btn_continuar: Button = $CajaDialogoDDLC/Margin/HBox/VBox/HBoxBottom/BtnContinuar

# Referencias al Modal "Ya en tu Ejército"
@onready var modal_ya_en_ejercito: Control = $ModalYaEnEjercito
@onready var modal_panel: Control = $ModalYaEnEjercito/PanelContenido
@onready var modal_avatar: TextureRect = $ModalYaEnEjercito/PanelContenido/Margin/VBox/CardPreviewBox/AvatarPreview
@onready var modal_lbl_nombre: Label = $ModalYaEnEjercito/PanelContenido/Margin/VBox/CardPreviewBox/VBoxInfo/LblNombreTropa
@onready var modal_lbl_hab: Label = $ModalYaEnEjercito/PanelContenido/Margin/VBox/CardPreviewBox/VBoxInfo/LblHabilidadTropa
@onready var btn_ir_al_mazo: Button = $ModalYaEnEjercito/PanelContenido/Margin/VBox/HBoxBotones/BtnIrAlMazo
@onready var btn_cerrar_modal: Button = $ModalYaEnEjercito/PanelContenido/Margin/VBox/HBoxBotones/BtnCerrarModal

@onready var http_request: HTTPRequest = $HTTPRequest
@onready var audio_beep: AudioStreamPlayer = $AudioDatafast
@onready var audio_portal: AudioStreamPlayer = $AudioPortal
@onready var audio_voice: AudioStreamPlayer = $AudioTypewriter
@onready var btn_volver: Button = $BtnVolver

# Estados
enum Estado { IDLE, VALIDANDO, INVOCANDO, DIALOGO, MODAL_YA_POSEIDA }
var estado_actual: Estado = Estado.IDLE
var carta_reclamada: Dictionary = {}
var texto_completo: String = ""
var typewriter_tween: Tween = null
var float_tween: Tween = null
var tarjeta_escaneada: bool = false

func _ready() -> void:
	if api_url.is_empty():
		if OS.has_feature("editor") or OS.has_feature("windows") or OS.has_feature("linux") or OS.has_feature("macos"):
			api_url = URL_LOCAL
		else:
			api_url = URL_PRODUCCION

	panel_nfc_prompt.visible = false
	caja_dialogo.visible = false
	modal_ya_en_ejercito.visible = false
	sprite_personaje.scale = Vector2.ZERO
	sprite_personaje.modulate.a = 0.0
	lbl_estado.text = ""

	btn_escanear_nfc.pressed.connect(_simular_escaneo_nfc)
	btn_sincronizar.pressed.connect(_on_sincronizar_pressed)
	btn_continuar.pressed.connect(_on_continuar_pressed)
	btn_volver.pressed.connect(_on_volver_pressed)
	btn_ir_al_mazo.pressed.connect(_on_ir_al_mazo_pressed)
	btn_cerrar_modal.pressed.connect(_on_cerrar_modal_pressed)
	http_request.request_completed.connect(_on_http_request_completed)
	txt_codigo.text_submitted.connect(func(_t): _on_sincronizar_pressed())

	_crear_audios_sinteticos()
	_iniciar_rotacion_portal()
	_iniciar_pulso_escaner()

func _iniciar_pulso_escaner() -> void:
	var tw = create_tween().set_loops()
	tw.tween_property(btn_escanear_nfc, "modulate", Color(1.2, 1.2, 1.4), 0.8).set_trans(Tween.TRANS_SINE)
	tw.tween_property(btn_escanear_nfc, "modulate", Color.WHITE, 0.8).set_trans(Tween.TRANS_SINE)

func _iniciar_rotacion_portal() -> void:
	var tw = create_tween().set_loops()
	tw.tween_property(sprite_portal, "rotation", TAU, 14.0).from(0.0)

# "Trampa NFC": emula la lectura física por RFID/NFC con sonido auténtico y vibración háptica
func _simular_escaneo_nfc() -> void:
	tarjeta_escaneada = true
	_reproducir_beep_nfc()
	
	btn_escanear_nfc.text = "✓ ¡TARJETA RFID/NFC DETECTADA!"
	btn_escanear_nfc.modulate = Color(0.2, 1.2, 0.7)
	
	lbl_estado.text = "Tarjeta física reconocida. Ingresa los 5 dígitos del reverso para sincronizar:"
	lbl_estado.modulate = Color(0.3, 1.0, 0.7)
	
	txt_codigo.grab_focus()

func _input(event: InputEvent) -> void:
	if estado_actual == Estado.IDLE:
		if event is InputEventScreenTouch and event.pressed:
			if not txt_codigo.has_focus():
				_simular_escaneo_nfc()

func _es_tropa_ya_desbloqueada(card_id: String) -> bool:
	if card_id.is_empty():
		return false
	var cfg = ConfigFile.new()
	if cfg.load("user://preferencias.cfg") == OK:
		var clave = "tropa_" + card_id.to_lower().replace("_", "").replace("-", "")
		return cfg.get_value("tienda_items", clave, false)
	return false

func _on_sincronizar_pressed() -> void:
	var codigo = txt_codigo.text.strip_edges()
	if codigo.is_empty():
		lbl_estado.text = "Ingresa los 5 dígitos impresos en tu tarjeta."
		lbl_estado.modulate = Color(1.0, 0.5, 0.5)
		return

	# Si ya la tiene desbloqueada en el ejército, mostrar la pantalla visual de tropa ya poseída
	if CATALOGO_TARJETAS_FISICAS.has(codigo):
		var info_local = CATALOGO_TARJETAS_FISICAS[codigo]
		if _es_tropa_ya_desbloqueada(info_local.get("cardId", "")):
			_mostrar_modal_ya_en_ejercito(info_local)
			return
		carta_reclamada = info_local

	estado_actual = Estado.VALIDANDO
	btn_sincronizar.disabled = true
	lbl_estado.text = "Sincronizando tarjeta con el portal..."
	lbl_estado.modulate = Color(0.9, 0.9, 1.0)

	var json_payload = JSON.stringify({
		"CardPin": codigo,
		"NfcUid": "NFC_" + codigo
	})

	var headers = [
		"Content-Type: application/json",
		"Accept: application/json"
	]

	var error = http_request.request(
		api_url,
		headers,
		HTTPClient.METHOD_POST,
		json_payload
	)

	if error != OK:
		_resolver_fallback_local(codigo)

func _on_http_request_completed(result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	btn_sincronizar.disabled = false
	var codigo = txt_codigo.text.strip_edges()

	# Caso 1: Servidor respondió 200 OK
	if result == HTTPRequest.RESULT_SUCCESS and response_code == 200:
		var json = JSON.new()
		var parse_err = json.parse(body.get_string_from_utf8())
		if parse_err == OK and json.data is Dictionary:
			carta_reclamada = json.data
			_finalizar_canje_exitoso()
			return

	# Caso 2: 409 Tarjeta ya registrada -> Mostrar interfaz visual hermosa
	if response_code == 409:
		var info = carta_reclamada
		if info.is_empty() and CATALOGO_TARJETAS_FISICAS.has(codigo):
			info = CATALOGO_TARJETAS_FISICAS[codigo]
		_mostrar_modal_ya_en_ejercito(info)
		return

	# Caso 3: Fallback inteligente si la API remota dio 405 / 404 o conexión caída
	_resolver_fallback_local(codigo)

func _resolver_fallback_local(codigo: String) -> void:
	if CATALOGO_TARJETAS_FISICAS.has(codigo):
		var info = CATALOGO_TARJETAS_FISICAS[codigo]
		if _es_tropa_ya_desbloqueada(info.get("cardId", "")):
			_mostrar_modal_ya_en_ejercito(info)
			return
		carta_reclamada = info
		_finalizar_canje_exitoso()
	else:
		btn_sincronizar.disabled = false
		lbl_estado.text = "Código de tarjeta no válido. Revisa los 5 dígitos."
		lbl_estado.modulate = Color(1.0, 0.35, 0.35)
		estado_actual = Estado.IDLE

func _finalizar_canje_exitoso() -> void:
	lbl_estado.text = "¡Tarjeta vinculada con éxito!"
	lbl_estado.modulate = Color(0.2, 1.0, 0.5)

	# Registrar desbloqueo en preferencias locales
	var card_id = carta_reclamada.get("cardId", "")
	if card_id != "":
		var cfg = ConfigFile.new()
		cfg.load("user://preferencias.cfg")
		var clave = "tropa_" + card_id.to_lower().replace("_", "").replace("-", "")
		cfg.set_value("tienda_items", clave, true)
		cfg.save("user://preferencias.cfg")

	# Iniciar invocación masiva del portal
	_activar_invocacion_portal()

func _activar_invocacion_portal() -> void:
	estado_actual = Estado.INVOCANDO
	panel_input.visible = false

	# Textura del personaje según CardId
	var card_id = carta_reclamada.get("cardId", "")
	var ruta_sprite = SPRITES_PERSONAJE.get(card_id, "res://imagenes/MenuNuevo/BotonCartas.png")
	if ResourceLoader.exists(ruta_sprite):
		sprite_personaje.texture = load(ruta_sprite)

	sprite_personaje.position = Vector2(0, -30)
	sprite_personaje.scale = Vector2.ZERO
	sprite_personaje.modulate.a = 0.0

	# Fondo temático según el origen del personaje (Océano para Tiburón/Calamar, Guerra para tropas Toon)
	var ruta_fondo = FONDOS_TEMATICOS.get(card_id, "")
	if ruta_fondo != "" and ResourceLoader.exists(ruta_fondo):
		fondo_tematico.texture = load(ruta_fondo)
		fondo_tematico.modulate.a = 0.0

	# Portal gigante y expansión masiva del personaje (ocupa toda la pantalla como en el selector)
	var tw = create_tween()
	tw.set_parallel(true)
	if ruta_fondo != "":
		tw.tween_property(fondo_tematico, "modulate:a", 0.9, 0.75).set_trans(Tween.TRANS_SINE)
	tw.tween_property(sprite_portal, "scale", Vector2(2.15, 2.15), 0.4).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tw.tween_property(sprite_portal, "modulate", Color(1.8, 1.8, 2.4), 0.4)

	# Escala masiva (1.35x) para llenar la pantalla con gran presencia épica
	tw.tween_property(sprite_personaje, "scale", Vector2(1.35, 1.35), 0.7).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tw.tween_property(sprite_personaje, "modulate:a", 1.0, 0.45)

	tw.chain().tween_property(sprite_portal, "scale", Vector2(1.85, 1.85), 0.4)
	tw.tween_property(sprite_portal, "modulate", Color.WHITE, 0.4)

	tw.chain().tween_callback(Callable(self, "_iniciar_dialogo_ddlc"))

	# Levitación constante sobre el portal
	if float_tween:
		float_tween.kill()
	float_tween = create_tween().set_loops()
	float_tween.tween_property(sprite_personaje, "position:y", -22.0, 1.8).as_relative().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	float_tween.tween_property(sprite_personaje, "position:y", 22.0, 1.8).as_relative().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)

func _iniciar_dialogo_ddlc() -> void:
	estado_actual = Estado.DIALOGO
	caja_dialogo.visible = true
	caja_dialogo.modulate.a = 0.0

	var card_id = carta_reclamada.get("cardId", "")
	var nombre = carta_reclamada.get("name", "Tropa Exclusiva")
	var serie = carta_reclamada.get("serie", "PACÍFICO")
	var tipo = carta_reclamada.get("tipo", "Guerrero")
	var hp = carta_reclamada.get("hp", 0)
	var atk = carta_reclamada.get("atk", 0)
	var def = carta_reclamada.get("def", 0)
	var hab = carta_reclamada.get("habilidad", "")
	texto_completo = carta_reclamada.get("dialogue", "¡Estoy listo para combatir a tu lado!")

	# Cargar avatar en el marco de diálogo
	var ruta_avatar = ICONOS_PERSONAJE.get(card_id, "")
	if ruta_avatar != "" and ResourceLoader.exists(ruta_avatar):
		avatar_personaje.texture = load(ruta_avatar)

	# Animación de rebote del avatar al aparecer
	avatar_box.scale = Vector2(0.75, 0.75)
	avatar_box.pivot_offset = Vector2(100, 100)
	create_tween().tween_property(avatar_box, "scale", Vector2(1.0, 1.0), 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

	# Nombre y Badges
	lbl_nombre_personaje.text = nombre.to_upper()
	if serie.to_upper() == "PACÍFICO":
		badge_serie.text = "🌊 PACÍFICO"
	elif serie.to_upper() == "TOON":
		badge_serie.text = "💥 TOON"
	else:
		badge_serie.text = "⭐ " + serie

	badge_tipo.text = "🗡️ " + tipo if tipo == "Asesino" else ("🛡️ " + tipo if tipo == "Coloso" else "🎯 " + tipo)

	# Estadísticas RPG con números limpios e íconos gráficos dedicados
	lbl_hp.text = str(hp)
	lbl_atk.text = str(atk)
	lbl_def.text = str(def)
	lbl_hab.text = "⚡ " + hab

	if lbl_stats:
		lbl_stats.text = "SERIE: %s | HP: %d | ATK: %d | DEF: %d" % [serie, hp, atk, def]

	# Texto con formato BBCode elegante
	txt_dialogo.text = "[color=#FFEAA7]“[/color]%s[color=#FFEAA7]”[/color]" % texto_completo
	txt_dialogo.visible_characters = 0

	create_tween().tween_property(caja_dialogo, "modulate:a", 1.0, 0.3)

	if typewriter_tween:
		typewriter_tween.kill()

	typewriter_tween = create_tween()
	var total_chars = texto_completo.length()
	var tiempo_total = max(1.2, total_chars * 0.035)

	typewriter_tween.tween_method(
		func(val: int):
			if val != txt_dialogo.visible_characters:
				txt_dialogo.visible_characters = val
				if val < total_chars and (val % 2 == 0):
					_reproducir_voz_typewriter()
					if randf() > 0.65:
						avatar_box.scale = Vector2(1.03, 1.03)
					else:
						avatar_box.scale = Vector2(1.0, 1.0),
		0,
		total_chars,
		tiempo_total
	)
	typewriter_tween.chain().tween_callback(func(): avatar_box.scale = Vector2(1.0, 1.0))

# Interfaz visual cuando la tropa ya forma parte del ejército
func _mostrar_modal_ya_en_ejercito(datos: Dictionary) -> void:
	estado_actual = Estado.MODAL_YA_POSEIDA
	modal_ya_en_ejercito.visible = true
	modal_ya_en_ejercito.modulate.a = 0.0

	var card_id = datos.get("cardId", "tiburon")
	var nombre = datos.get("name", "Tropa").to_upper()
	var hab = datos.get("habilidad", "")

	modal_lbl_nombre.text = nombre
	modal_lbl_hab.text = "Habilidad: " + hab

	var ruta_avatar = ICONOS_PERSONAJE.get(card_id, "")
	if ruta_avatar != "" and ResourceLoader.exists(ruta_avatar):
		modal_avatar.texture = load(ruta_avatar)

	var ruta_fondo = FONDOS_TEMATICOS.get(card_id, "")
	if ruta_fondo != "" and ResourceLoader.exists(ruta_fondo):
		fondo_tematico.texture = load(ruta_fondo)
		fondo_tematico.modulate.a = 0.0

	modal_panel.scale = Vector2(0.75, 0.75)
	var tw = create_tween()
	tw.set_parallel(true)
	if ruta_fondo != "":
		tw.tween_property(fondo_tematico, "modulate:a", 0.85, 0.35).set_trans(Tween.TRANS_SINE)
	tw.tween_property(modal_ya_en_ejercito, "modulate:a", 1.0, 0.25)
	tw.tween_property(modal_panel, "scale", Vector2(1.0, 1.0), 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)

	_reproducir_beep_nfc()

func _on_ir_al_mazo_pressed() -> void:
	get_tree().change_scene_to_file("res://escenas/menu/MenuConstructor.tscn")

func _on_cerrar_modal_pressed() -> void:
	var tw = create_tween()
	tw.set_parallel(true)
	tw.tween_property(fondo_tematico, "modulate:a", 0.0, 0.2)
	tw.tween_property(modal_ya_en_ejercito, "modulate:a", 0.0, 0.2)
	tw.tween_property(modal_panel, "scale", Vector2(0.8, 0.8), 0.2)
	tw.chain().tween_callback(func():
		modal_ya_en_ejercito.visible = false
		estado_actual = Estado.IDLE
		btn_sincronizar.disabled = false
		lbl_estado.text = ""
	)

func _reproducir_beep_nfc() -> void:
	audio_beep.stream = _generar_beep_rfid()
	audio_beep.play()

	if OS.has_feature("mobile") or OS.has_feature("android"):
		var input_singleton = Engine.get_singleton("Input")
		if input_singleton and input_singleton.has_method("vibrate_handshake"):
			input_singleton.vibrate_handshake(180)

func _reproducir_voz_typewriter() -> void:
	if audio_voice.stream != null:
		audio_voice.pitch_scale = randf_range(0.92, 1.15)
		audio_voice.play()

func _crear_audios_sinteticos() -> void:
	if audio_beep.stream == null:
		audio_beep.stream = _generar_beep_rfid()
	if audio_voice.stream == null:
		audio_voice.stream = _generar_tono(440.0, 0.04, 0.15)

func _generar_beep_rfid() -> AudioStreamWAV:
	var sample_rate = 22050
	var d1 = 0.06
	var p = 0.03
	var d2 = 0.08
	var total_samples = int(sample_rate * (d1 + p + d2))
	var pcm = PackedByteArray()
	pcm.resize(total_samples)

	var s1 = int(sample_rate * d1)
	var sp = int(sample_rate * (d1 + p))

	for i in range(total_samples):
		var val = 0.0
		if i < s1:
			var t = float(i) / float(sample_rate)
			val = sin(t * 1050.0 * TAU) * 0.35
		elif i >= sp:
			var t = float(i - sp) / float(sample_rate)
			val = sin(t * 1500.0 * TAU) * 0.4
		var byte_val = int(clamp((val + 1.0) * 0.5 * 255.0, 0, 255))
		pcm.set(i, byte_val)

	var wav = AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_8_BITS
	wav.mix_rate = sample_rate
	wav.data = pcm
	return wav

func _generar_tono(frecuencia: float, duracion: float, volumen: float) -> AudioStreamWAV:
	var sample_rate = 22050
	var total_samples = int(sample_rate * duracion)
	var pcm = PackedByteArray()
	pcm.resize(total_samples)

	for i in range(total_samples):
		var t = float(i) / float(sample_rate)
		var val = sin(t * frecuencia * TAU) * volumen
		var byte_val = int(clamp((val + 1.0) * 0.5 * 255.0, 0, 255))
		pcm.set(i, byte_val)

	var wav = AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_8_BITS
	wav.mix_rate = sample_rate
	wav.data = pcm
	return wav

func _on_continuar_pressed() -> void:
	get_tree().change_scene_to_file("res://escenas/menu/MenuConstructor.tscn")

func _on_volver_pressed() -> void:
	get_tree().change_scene_to_file("res://escenas/menu/MenuConstructor.tscn")
