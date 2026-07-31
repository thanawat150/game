from pathlib import Path

path = Path("GrowWise/scripts/main.gd")
text = path.read_text(encoding="utf-8")
text = text.replace("func translate(", "func tx(")
text = text.replace("translate(", "tx(")
path.write_text(text, encoding="utf-8")
