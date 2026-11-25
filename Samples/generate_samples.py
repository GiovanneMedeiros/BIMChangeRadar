#!/usr/bin/env python3
"""Gera project-v1.json e project-v2.json (Edifício Alpha) de forma determinística.
As mudanças da v2 são aplicadas explicitamente abaixo; o motor de comparação do app
NÃO lê este script — ele compara os dois JSONs gerados."""
import copy, json, random
random.seed(42)

def r(x): return round(x, 2)

def wall(i, level, length, height, material, typ, fire, comments):
    w = 0.2
    e = dict(id=f"W-{i}", uniqueId=f"wall-{i}", name=f"Parede {chr(64+(i-100)%26 or 26)}{i}", category="Wall", type=typ,
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
lv = ["Pavimento 1", "Pavimento 2", "Pavimento 3"]
mats_w = ["Concreto", "Alvenaria", "Drywall"]
for n in range(1, 25):
    i = 100 + n
    l = lv[(n - 1) % 3]
    m = mats_w[n % 3]
    els.append(recompute(wall(i, l, r(2.4 + (n % 7) * 0.6), 2.8, m, "Parede Interna" if n % 4 else "Parede Externa",
                              ["30min", "60min", "90min"][n % 3], "Parede interna" if n % 4 else "Parede externa")))
# a parede do exemplo do enunciado
els[3].update(name="Parede A", level="Pavimento 1", material="Concreto", length=3.2, height=2.8)
els[3]["parameters"]["FireRating"] = "60min"; recompute(els[3])

for n in range(1, 13):
    i = 100 + n; w = 0.9 if n % 3 else 1.0
    els.append(recompute(dict(id=f"D-{i}", uniqueId=f"door-{i}", name=f"Porta {n}", category="Door",
        type="Folha Simples" if n % 2 else "Folha Dupla", level=lv[(n - 1) % 3], material="Madeira" if n % 3 else "Aço",
        width=w, height=2.1, mark=f"D-{i}", parameters={"Mark": f"D-{i}", "FireRating": "20min" if n % 2 else "45min", "Hardware": "Padrão"})))

for n in range(1, 11):
    i = 100 + n
    els.append(recompute(dict(id=f"WIN-{i}", uniqueId=f"window-{i}", name=f"Janela {n}", category="Window",
        type="Fixa" if n % 2 else "De Abrir", level=lv[(n - 1) % 3], material="Alumínio", width=1.2, height=1.5,
        mark=f"WIN-{i}", parameters={"Mark": f"WIN-{i}", "GlazingType": "Duplo", "Comments": "Janela padrão"})))

room_names = ["Hall", "Escritório", "Reunião", "Cozinha", "Banheiro", "Depósito", "Sala de Servidores", "Corredor", "Recepção", "Copa", "Arquivo", "Laboratório"]
for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"R-{i}", uniqueId=f"room-{i}", name=f"{room_names[n-1]}", category="Room",
        type="Ambiente", level=lv[(n - 1) % 3], length=r(3 + n % 5), width=r(4 + n % 4), mark=f"R-{i}",
        parameters={"Mark": f"R-{i}", "Occupancy": "Comercial", "Department": "Geral"})))

for n in range(1, 5):
    i = 100 + n
    els.append(recompute(dict(id=f"F-{i}", uniqueId=f"floor-{i}", name=f"Laje {n}", category="Floor",
        type="Laje de Concreto 20cm", level=lv[(n - 1) % 3], material="Concreto", length=12.0, width=10.0, height=0.2,
        mark=f"F-{i}", parameters={"Mark": f"F-{i}", "StructuralUsage": "Laje", "LoadBearing": "true"})))

for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"C-{i}", uniqueId=f"column-{i}", name=f"Pilar {n}", category="Column",
        type="Retangular 40x40", level=lv[(n - 1) % 3], material="Concreto", width=0.4, height=3.0, length=0.4,
        mark=f"C-{i}", parameters={"Mark": f"C-{i}", "StructuralUsage": "Pilar", "LoadBearing": "true"})))

for n in range(1, 11):
    i = 100 + n
    els.append(recompute(dict(id=f"B-{i}", uniqueId=f"beam-{i}", name=f"Viga {n}", category="Beam",
        type="Retangular 25x50", level=lv[(n - 1) % 3], material="Aço" if n % 2 else "Concreto", width=0.25, height=0.5,
        length=r(4 + (n % 4) * 0.5), mark=f"B-{i}", parameters={"Mark": f"B-{i}", "StructuralUsage": "Viga principal", "LoadBearing": "true"})))

for n in range(1, 13):
    i = 100 + n
    els.append(recompute(dict(id=f"P-{i}", uniqueId=f"pipe-{i}", name=f"Tubo {n}", category="Pipe",
        type="PVC 100mm" if n % 2 else "Cobre 50mm", level=lv[(n - 1) % 3], material="PVC" if n % 2 else "Cobre",
        width=0.1 if n % 2 else 0.05, length=r(3 + (n % 5)), mark=f"P-{i}",
        parameters={"Mark": f"P-{i}", "System": "Esgoto" if n % 2 else "Água fria", "FlowRate": "2.5 L/s"})))

furn = ["Mesa", "Cadeira", "Armário", "Sofá", "Mesa de Centro", "Estante", "Gaveteiro"]
for n in range(1, 15):
    i = 100 + n
    els.append(recompute(dict(id=f"FUR-{i}", uniqueId=f"furniture-{i}", name=f"{furn[n % 7]} {n}", category="Furniture",
        type=furn[n % 7], level=lv[(n - 1) % 3], material="Madeira", width=r(0.6 + (n % 3) * 0.3), length=r(1.0 + (n % 4) * 0.3), height=0.75,
        mark=f"FUR-{i}", parameters={"Mark": f"FUR-{i}", "Comments": "Móvel"})))

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
mod("W-104", length=4.8, material="Drywall", level="Pavimento 2")            # exemplo do enunciado (3 mudanças)
mod("W-107", name="Parede G (Corta-fogo)")                                  # mudança de nome (LOW)
mod("W-115", params={"FireRating": "120min"})                             # parâmetro crítico (HIGH)
mod("W-120", type="Parede Cortina")                                        # mudança de tipo (MEDIUM)
mod("W-122", height=3.2)                                                  # dimensão (HIGH)
mod("D-103", width=1.1)                                                   # dimensão porta
mod("D-105", material="Aço")                                            # material (MEDIUM)
mod("D-111", params={"Hardware": "Barra antipânico"})                            # param informativo (LOW)
mod("WIN-104", height=1.8, width=1.4)                                     # dimensões
mod("WIN-108", material="Madeira")                                           # material
mod("R-103", name="Sala de Reunião A", length=6.0)                           # nome + dimensão
mod("R-109", params={"Department": "Financeiro"})                            # param LOW
mod("F-102", length=14.0)                                                 # laje: dimensão estrutural (CRITICAL)
mod("C-105", height=3.6)                                                  # coluna mais alta (CRITICAL)
mod("C-108", material="Aço")                                            # material estrutural (HIGH)
mod("B-104", material="Concreto", params={"StructuralUsage": "Secundária"}) # HIGH
mod("B-107", length=6.5)                                                  # viga mais longa (CRITICAL)
mod("P-104", length=7.5, type="PVC 150mm", width=0.15)                    # tubulação
mod("P-111", level="Pavimento 3")                                             # mudou de nível
mod("FUR-103", level="Pavimento 3", name="Mesa Executiva")                    # nível + nome
mod("FUR-105", params={"Comments": "Fixo na parede"})                      # LOW

# Adicionados
def add(e): v2e.append(recompute(e))
for k in (201, 202, 203):
    add(wall(k, "Pavimento 2" if k != 203 else "Pavimento 3", r(3.0 + (k - 200) * 0.5), 2.8, "Drywall", "Parede Interna", "60min", "Nova divisória"))
for k in (201, 202):
    add(dict(id=f"D-{k}", uniqueId=f"door-{k}", name=f"Porta {k-188}", category="Door", type="Folha Simples", level="Pavimento 2",
             material="Madeira", width=0.9, height=2.1, mark=f"D-{k}", parameters={"Mark": f"D-{k}", "FireRating": "30min"}))
add(dict(id="WIN-201", uniqueId="window-201", name="Janela 11", category="Window", type="Fixa", level="Pavimento 2",
         material="Alumínio", width=1.5, height=1.5, mark="WIN-201", parameters={"Mark": "WIN-201", "GlazingType": "Triplo"}))
add(dict(id="R-201", uniqueId="room-201", name="Cabine Telefônica", category="Room", type="Ambiente", level="Pavimento 2",
         length=2.0, width=2.0, mark="R-201", parameters={"Mark": "R-201", "Occupancy": "Comercial"}))
for k in (201, 202, 203):
    add(dict(id=f"FUR-{k}", uniqueId=f"furniture-{k}", name=f"Mesa de Reunião {k-200}", category="Furniture", type="Table",
             level="Pavimento 2", material="Madeira", width=1.0, length=2.4, height=0.75, mark=f"FUR-{k}", parameters={"Mark": f"FUR-{k}"}))
add(dict(id="P-201", uniqueId="pipe-201", name="Tubo 13", category="Pipe", type="PVC 100mm", level="Pavimento 2",
         material="PVC", width=0.1, length=5.5, mark="P-201", parameters={"Mark": "P-201", "System": "Esgoto", "FlowRate": "2.5 L/s"}))
add(dict(id="B-201", uniqueId="beam-201", name="Viga 11", category="Beam", type="Retangular 25x50", level="Pavimento 2",
         material="Aço", width=0.25, height=0.5, length=5.0, mark="B-201", parameters={"Mark": "B-201", "StructuralUsage": "Viga principal", "LoadBearing": "true"}))

v2 = dict(projectName="Edifício Alpha", version="2.0", date="2026-11-23T14:30:00", source="json-sample", elements=v2e)

for name, data in (("project-v1.json", v1), ("project-v2.json", v2)):
    with open(name, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(name, len(data["elements"]), "elementos")
