extends Node3D

@export var escena_rey: PackedScene
@export var escena_dino: PackedScene

func _ready():
	print("Iniciando mapa...")
	crear_reyes()
	spawn_dinosaurios()

# -------------------------
# CREAR REYES
# -------------------------
func crear_reyes():
	if escena_rey == null:
		print("ERROR: No asignaste Rey.tscn")
		return

	var pos_jugador = $PosReyJugador.global_transform.origin
	var pos_enemigo = $PosReyEnemigo.global_transform.origin

	var rey_jugador = escena_rey.instantiate()
	rey_jugador.global_transform.origin = pos_jugador
	rey_jugador.name = "ReyJugador"
	add_child(rey_jugador)

	var rey_enemigo = escena_rey.instantiate()
	rey_enemigo.global_transform.origin = pos_enemigo
	rey_enemigo.name = "ReyEnemigo"
	add_child(rey_enemigo)

	print("Reyes creados")

# -------------------------
# SPAWN AUTOMÁTICO
# -------------------------
func spawn_dinosaurios():
	while true:
		await get_tree().create_timer(10).timeout
		spawn_dos_dinos()

# -------------------------
# CREAR DINOS
# -------------------------
func spawn_dos_dinos():
	if escena_dino == null:
		print("ERROR: No asignaste Dinosaurio.tscn")
		return

	var pos_jugador = $CuevaJugador.global_transform.origin
	var pos_enemigo = $CuevaEnemigo.global_transform.origin

	# Elegir caminos
	var camino_aliado = elegir_camino()
	var camino_enemigo = elegir_camino()

	# Dino aliado
	var dino_aliado = escena_dino.instantiate()
	dino_aliado.global_transform.origin = pos_jugador
	dino_aliado.name = "RexAliado"
	dino_aliado.camino = camino_aliado
	add_child(dino_aliado)

	# Dino enemigo
	var dino_enemigo = escena_dino.instantiate()
	dino_enemigo.global_transform.origin = pos_enemigo
	dino_enemigo.name = "RexEnemigo"
	dino_enemigo.camino = camino_enemigo
	add_child(dino_enemigo)

	print("Dinos generados")

# -------------------------
# ELEGIR PUENTE (ALEATORIO)
# -------------------------
func elegir_camino():
	if randi() % 2 == 0:
		return [
			$PuenteIzq_1,
			$PuenteIzq_2
		]
	else:
		return [
			$PuenteDer_1,
			$PuenteDer_2
		]
