extends SceneTree
func _initialize():
 for n in ["游標","捏臉手_右手"]:
  var t=load("res://Resources/Sprites/"+n+".png")
  assert(t!=null)
  var i=t.get_image()
  if i.is_compressed(): i.decompress()
  assert(i.get_pixel(0,i.get_height()-1).a<0.01)
  print("HAND_ALPHA_OK ",n)
 quit()
