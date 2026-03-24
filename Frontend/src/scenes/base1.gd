extends Node3D

# =========================
# CONFIG
# =========================

const ANCHO = 9
const ALTO = 10

@export var escena_rey: PackedScene
@export var escena_prueba: PackedScene

@onready var camera = $Camera3D
@onready var game_manager = get_tree().get_root().get_node("main/GameManager")

# =========================
# INICIO
# =========================

func _ready():
	print("Campo iniciado")
	crear_reyes()

# =========================
# REYES
# =========================

func crear_reyes():
	if escena_rey == null:
		print("No asignaste Rey")
		return

	var rey_jugador = escena_rey.instantiate()
	rey_jugador.global_transform.origin = $PosReyJugador.global_transform.origin
	rey_jugador.name = "ReyJugador"
	add_child(rey_jugador)

	var rey_enemigo = escena_rey.instantiate()
	rey_enemigo.global_transform.origin = $PosReyEnemigo.global_transform.origin
	rey_enemigo.name = "ReyEnemigo"
	add_child(rey_enemigo)

	print("Reyes creados")

# =========================
# INPUT
# =========================

func _input(event):
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT:
			detectar_click()

# =========================
# RAYCAST
# =========================

func detectar_click():
	var mouse_pos = get_viewport().get_mouse_position()
	
	var origen = camera.project_ray_origin(mouse_pos)
	var direccion = camera.project_ray_normal(mouse_pos)
	
	var espacio = get_world_3d().direct_space_state
	var query = PhysicsRayQueryParameters3D.create(origen, origen + direccion * 1000)
	var resultado = espacio.intersect_ray(query)
	
	if resultado.size() > 0:
		var pos = resultado.position
		procesar_celda(pos)
	else:
		print("No golpeó nada")

# =========================
# CELDAS
# =========================

func procesar_celda(pos):
	var x = round(pos.x)
	var y = round(pos.z)
	
	if x >= 0 and x < ANCHO and y >= 0 and y < ALTO:
		
		print("Click en:", x, y)
		
		if game_manager.PuedeColocar(x, y):
			colocar_objeto(x, y)
		else:
			print("No permitido")

# =========================
# COLOCAR OBJETO
# =========================

func colocar_objeto(x, y):
	if escena_prueba == null:
		print("No asignaste objeto")
		return
	
	var obj = escena_prueba.instantiate()
	obj.global_transform.origin = Vector3(x, 1, y)
	add_child(obj)
	
	game_manager.ColocarEnMatriz(x, y, obj)
