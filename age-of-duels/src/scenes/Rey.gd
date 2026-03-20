extends Node3D

var vida = 200
var es_enemigo = false

func recibir_danio(cantidad):
	vida -= cantidad
	print(name, "vida:", vida)

	if vida <= 0:
		morir()

func morir():
	print(name, "has perdido")
	queue_free()
