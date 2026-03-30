extends ColorRect # Esto es importante: le decimos que somos un ColorRect

var esta_invocada = false

# --- EL JUGADOR AGARRA LA CARTA ---
func _get_drag_data(at_position: Vector2) -> Variant:
	if esta_invocada:
		return null # Si ya está en el tablero, no la agarramos
	
	print("¡Agarraste una carta!")
	
	# 1. Crear el fantasma visual
	var fantasma = duplicate()
	fantasma.modulate.a = 0.5 # Hacerlo medio transparente
	
	# Esto es vital para que el ratón lo agarre del centro y no de la esquina
	var control_fantasma = Control.new()
	control_fantasma.add_child(fantasma)
	fantasma.position = -size / 2 
	
	set_drag_preview(control_fantasma)
	
	# 2. Enviar la carta real al tablero
	return { "carta_real": self }
