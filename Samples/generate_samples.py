#!/usr/bin/env python3
"""Gera project-v1.json e project-v2.json (Edifício Alpha) de forma determinística.
As mudanças da v2 são aplicadas explicitamente abaixo; o motor de comparação do app
NÃO lê este script — ele compara os dois JSONs gerados."""
import copy, json, random
random.seed(42)

def r(x): return round(x, 2)

def wall(i, level, length, height, material, typ, fire, comments):
    w = 0.2
    e = dict(id=f"W-{i}", uniqueId=f"wall-{i}", name=f"Wall {chr(64+(i-100)%26 or 26)}{i}", category="Wall", type=typ,
             level=level, material=material, length=length, height=height, width=w, mark=f"W-{i}",
             parameters={"Mark": f"W-{i}", "FireRating": fire, "Comments": comments})
    return e

def recompute(e):
    c = e["category"]
    if c == "Wall":
        e["area"] = r(e["length"] * e["height"]); e["volume"] = r(e["area"] * e["width"])
    elif c in ("Door", "Window"):
        e["area"] = r(e["width"] * e["height"])
    elif c in ("Room", "Floor"):
        e["area"] = r(e["length"] * e["width"])
        if c == "Floor": e["volume"] = r(e["area"] * e["height"])
    elif c == "Column":
        e["volume"] = r(e["width"] * e["width"] * e["height"])
    elif c == "Beam":
        e["volume"] = r(e["width"] * e["height"] * e["length"])
    elif c == "Pipe":
        e["volume"] = r(3.14159 * (e["width"] / 2) ** 2 * e["length"])
    return e

els = []
lv = ["Floor 1", "Floor 2", "Floor 3"]
mats_w = ["Concrete", "Brick", "Drywall"]
for n in range(1, 25):
    i = 100 + n
    l = lv[(n - 1) % 3]
    m = mats_w[n % 3]
    els.append(recompute(wall(i, l, r(2.4 + (n % 7) * 0.6), 2.8, m, "Interior Wall" if n % 4 else "Exterior Wall",
                              ["30min", "60min", "90min"][n % 3], "Interior wall" if n % 4 else "Exterior wall")))
# a parede do exemplo do enunciado
els[3].update(name="Wall A", level="Floor 1", material="Concrete", length=3.2, height=2.8)
els[3]["parameters"]["FireRating"] = "60min"; recompute(els[3])

for n in range(1, 13):
    i = 100 + n; w = 0.9 if n % 3 else 1.0
    els.append(recompute(dict(id=f"D-{i}", uniqueId=f"door-{i}", name=f"Door {n}", category="Door",
        type="Single Flush" if n % 2 else "Double Flush", level=lv[(n - 1) % 3], material="Wood" if n % 3 else "Steel",
        width=w, height=2.1, mark=f"D-{i}", parameters={"Mark": f"D-{i}", "FireRating": "20min" if n % 2 else "45min", "Hardware": "Standard"})))

for n in range(1, 11):
    i = 100 + n
    els.append(recompute(dict(id=f"WIN-{i}", uniqueId=f"window-{i}", name=f"Window {n}", category="Window",
        type="Fixed" if n % 2 else "Casement", level=lv[(n - 1) % 3], material="Aluminum", width=1.2, height=1.5,
        mark=f"WIN-{i}", parameters={"Mark": f"WIN-{i}", "GlazingType": "Double", "Comments": "Standard window"})))

room_names = ["Lobby", "Office", "Meeting", "Kitchen", "Restroom", "Storage", "Server Room", "Corridor", "Reception", "Break Room", "Archive", "Lab"]
for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"R-{i}", uniqueId=f"room-{i}", name=f"{room_names[n-1]}", category="Room",
        type="Room", level=lv[(n - 1) % 3], length=r(3 + n % 5), width=r(4 + n % 4), mark=f"R-{i}",
        parameters={"Mark": f"R-{i}", "Occupancy": "Business", "Department": "General"})))

for n in range(1, 5):
    i = 100 + n
    els.append(recompute(dict(id=f"F-{i}", uniqueId=f"floor-{i}", name=f"Floor Slab {n}", category="Floor",
        type="Concrete Slab 20cm", level=lv[(n - 1) % 3], material="Concrete", length=12.0, width=10.0, height=0.2,
        mark=f"F-{i}", parameters={"Mark": f"F-{i}", "StructuralUsage": "Slab", "LoadBearing": "true"})))

for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"C-{i}", uniqueId=f"column-{i}", name=f"Column {n}", category="Column",
        type="Rectangular 40x40", level=lv[(n - 1) % 3], material="Concrete", width=0.4, height=3.0, length=0.4,
        mark=f"C-{i}", parameters={"Mark": f"C-{i}", "StructuralUsage": "Column", "LoadBearing": "true"})))

for n in range(1, 11):
    i = 100 + n
    els.append(recompute(dict(id=f"B-{i}", uniqueId=f"beam-{i}", name=f"Beam {n}", category="Beam",
        type="Rectangular 25x50", level=lv[(n - 1) % 3], material="Steel" if n % 2 else "Concrete", width=0.25, height=0.5,
        length=r(4 + (n % 4) * 0.5), mark=f"B-{i}", parameters={"Mark": f"B-{i}", "StructuralUsage": "Girder", "LoadBearing": "true"})))

for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"P-{i}", uniqueId=f"pipe-{i}", name=f"Pipe {n}", category="Pipe",
        type="PVC 100mm" if n % 2 else "Copper 50mm", level=lv[(n - 1) % 3], material="PVC" if n % 2 else "Copper",
        width=0.1 if n % 2 else 0.05, length=r(3 + (n % 5)), mark=f"P-{i}",
        parameters={"Mark": f"P-{i}", "System": "Sanitary" if n % 2 else "Domestic Water", "FlowRate": "2.5 L/s"})))

furn = ["Desk", "Chair", "Cabinet", "Sofa", "Table", "Shelf", "Locker"]
for n in range(1, 15):
    i = 100 + n
    els.append(recompute(dict(id=f"FUR-{i}", uniqueId=f"furniture-{i}", name=f"{furn[n % 7]} {n}", category="Furniture",
        type=furn[n % 7], level=lv[(n - 1) % 3], material="Wood", width=r(0.6 + (n % 3) * 0.3), length=r(1.0 + (n % 4) * 0.3), height=0.75,
        mark=f"FUR-{i}", parameters={"Mark": f"FUR-{i}", "Comments": "Movable"})))

v1 = dict(projectName="Edifício Alpha", version="1.0", date="2026-11-20T09:00:00", source="json-sample", elements=els)

# ---------------- v2: aplicar mudanças explícitas ----------------
v2e = copy.deepcopy(els)
byid = {e["id"]: e for e in v2e}

def mod(i, **kw):
    e = byid[i]
    for k, v in kw.items():
        if k == "params": e["parameters"].update(v)
        else: e[k] = v
    recompute(e)

# Removidos
removed = ["W-110", "W-118", "D-108", "FUR-111", "FUR-112", "P-109", "C-112", "B-110"]
v2e = [e for e in v2e if e["id"] not in removed]
byid = {e["id"]: e for e in v2e}

# Modificados
mod("W-104", length=4.8, material="Drywall", level="Floor 2")            # exemplo do enunciado (3 mudanças)
mod("W-107", name="Wall G (Fire Rated)")                                  # mudança de nome (LOW)
mod("W-115", params={"FireRating": "120min"})                             # parâmetro crítico (HIGH)
mod("W-120", type="Curtain Wall")                                        # mudança de tipo (MEDIUM)
mod("W-122", height=3.2)                                                  # dimensão (HIGH)
mod("D-103", width=1.1)                                                   # dimensão porta
mod("D-105", material="Steel")                                            # material (MEDIUM)
mod("D-111", params={"Hardware": "Panic Bar"})                            # param informativo (LOW)
mod("WIN-104", height=1.8, width=1.4)                                     # dimensões
mod("WIN-108", material="Wood")                                           # material
mod("R-103", name="Meeting Room A", length=6.0)                           # nome + dimensão
mod("R-109", params={"Department": "Finance"})                            # param LOW
mod("F-102", length=14.0)                                                 # laje: dimensão estrutural (CRITICAL)
mod("C-105", height=3.6)                                                  # coluna mais alta (CRITICAL)
mod("C-108", material="Steel")                                            # material estrutural (HIGH)
mod("B-104", material="Concrete", params={"StructuralUsage": "Secondary"}) # HIGH
mod("B-107", length=6.5)                                                  # viga mais longa (CRITICAL)
mod("P-104", length=7.5, type="PVC 150mm", width=0.15)                    # tubulação
mod("P-111", level="Floor 3")                                             # mudou de nível
mod("FUR-103", level="Floor 3", name="Executive Desk")                    # nível + nome
mod("FUR-105", params={"Comments": "Fixed to wall"})                      # LOW

# Adicionados
def add(e): v2e.append(recompute(e))
for k in (201, 202, 203):
    add(wall(k, "Floor 2" if k != 203 else "Floor 3", r(3.0 + (k - 200) * 0.5), 2.8, "Drywall", "Interior Wall", "60min", "New partition"))
for k in (201, 202):
    add(dict(id=f"D-{k}", uniqueId=f"door-{k}", name=f"Door {k-188}", category="Door", type="Single Flush", level="Floor 2",
             material="Wood", width=0.9, height=2.1, mark=f"D-{k}", parameters={"Mark": f"D-{k}", "FireRating": "30min"}))
add(dict(id="WIN-201", uniqueId="window-201", name="Window 11", category="Window", type="Fixed", level="Floor 2",
         material="Aluminum", width=1.5, height=1.5, mark="WIN-201", parameters={"Mark": "WIN-201", "GlazingType": "Triple"}))
add(dict(id="R-201", uniqueId="room-201", name="Phone Booth", category="Room", type="Room", level="Floor 2",
         length=2.0, width=2.0, mark="R-201", parameters={"Mark": "R-201", "Occupancy": "Business"}))
for k in (201, 202, 203):
    add(dict(id=f"FUR-{k}", uniqueId=f"furniture-{k}", name=f"Conference Table {k-200}", category="Furniture", type="Table",
             level="Floor 2", material="Wood", width=1.0, length=2.4, height=0.75, mark=f"FUR-{k}", parameters={"Mark": f"FUR-{k}"}))
add(dict(id="P-201", uniqueId="pipe-201", name="Pipe 13", category="Pipe", type="PVC 100mm", level="Floor 2",
         material="PVC", width=0.1, length=5.5, mark="P-201", parameters={"Mark": "P-201", "System": "Sanitary", "FlowRate": "2.5 L/s"}))
add(dict(id="B-201", uniqueId="beam-201", name="Beam 11", category="Beam", type="Rectangular 25x50", level="Floor 2",
         material="Steel", width=0.25, height=0.5, length=5.0, mark="B-201", parameters={"Mark": "B-201", "StructuralUsage": "Girder", "LoadBearing": "true"}))

v2 = dict(projectName="Edifício Alpha", version="2.0", date="2026-11-23T14:30:00", source="json-sample", elements=v2e)

for name, data in (("project-v1.json", v1), ("project-v2.json", v2)):
    with open(name, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(name, len(data["elements"]), "elementos")
