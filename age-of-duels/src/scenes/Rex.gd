extends CharacterBody3D

@export var velocidad = 5.0
@export var danio = 20
@export var vida = 120
@export var tiempo_ataque = 1.0

var objetivo = null
var puede_atacar = true

var camino = []
var punto_actual = 0

func _ready():
	if name == "RexAliado":
		add_to_group("Aliado")
		objetivo = get_tree().get_root().find_child("ReyEnemigo", true, false)
	else:
		add_to_group("Enemigo")
		objetivo = get_tree().get_root().find_child("ReyJugador", true, false)

# -------------------------
func _physics_process(_delta):
	var target = null

	# 1️⃣ BUSCAR ENEMIGO
	var enemigo = buscar_enemigo_cercano()

	if enemigo:
		var dist = global_transform.origin.distance_to(enemigo.global_transform.origin)

		# SOLO si está relativamente cerca pelea
		if dist < 4:
			target = enemigo

	# 2️⃣ SI NO HAY ENEMIGO CERCANO → seguir camino o rey
	if target == null:
		if punto_actual < camino.size():
			target = camino[punto_actual]

			if global_transform.origin.distance_to(target.global_transform.origin) < 1:
				punto_actual += 1
		else:
			target = objetivo

	# 3️⃣ SIEMPRE moverse
	if target:
		mover_y_atacar(target)

# -------------------------
func buscar_enemigo_cercano():
	var grupo_objetivo = "Enemigo" if is_in_group("Aliado") else "Aliado"
	var enemigos = get_tree().get_nodes_in_group(grupo_objetivo)

	var mas_cercano = null
	var menor_distancia = INF

	for enemigo in enemigos:
		if enemigo == self:
			continue

		var distancia = global_transform.origin.distance_to(enemigo.global_transform.origin)

		if distancia < menor_distancia:
			menor_distancia = distancia
			mas_cercano = enemigo

	return mas_cercano

# -------------------------
func mover_y_atacar(target):
	var direccion = (target.global_transform.origin - global_transform.origin).normalized()
	direccion.y = 0

	velocity = direccion * velocidad
	move_and_slide()

	var distancia = global_transform.origin.distance_to(target.global_transform.origin)

	# 🔥 IMPORTANTE: rango de ataque correcto
	if distancia < 2:
		atacar(target)

# -------------------------
func atacar(target):
	if target and puede_atacar:
		puede_atacar = false

		if target.has_method("recibir_danio"):
			target.recibir_danio(danio)

		await get_tree().create_timer(tiempo_ataque).timeout
		puede_atacar = true

# -------------------------
func recibir_danio(cantidad):
	vida -= cantidad

	if vida <= 0:
		queue_free()
