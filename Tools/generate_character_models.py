from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
MODEL_DIR = ROOT / "Assets" / "_Project" / "Art" / "Models" / "Generated"
PREVIEW_DIR = ROOT / "Assets" / "_Project" / "Art" / "Concept" / "ModelPreviews"


@dataclass(frozen=True)
class Material:
    name: str
    color: tuple[float, float, float]
    metallic: float = 0.0
    roughness: float = 0.55
    emission: tuple[float, float, float] | None = None


@dataclass
class Part:
    name: str
    material: str
    vertices: np.ndarray
    faces: np.ndarray


class Model:
    def __init__(self, name: str, materials: dict[str, Material]) -> None:
        self.name = name
        self.materials = materials
        self.parts: list[Part] = []

    def add_part(self, name: str, material: str, vertices: list, faces: list) -> None:
        self.parts.append(
            Part(name, material, np.asarray(vertices, dtype=np.float64), np.asarray(faces, dtype=np.int32))
        )

    def sphere(self, name: str, center, scale, material: str, rings=12, segments=20, exponent=2.0) -> None:
        vertices = []
        faces = []

        def spow(value: float, power: float) -> float:
            return math.copysign(abs(value) ** power, value)

        power = 2.0 / exponent
        for ring in range(rings + 1):
            latitude = -math.pi / 2 + math.pi * ring / rings
            cl = math.cos(latitude)
            sl = math.sin(latitude)
            for segment in range(segments):
                longitude = 2 * math.pi * segment / segments
                x = spow(cl, power) * spow(math.cos(longitude), power)
                y = spow(sl, power)
                z = spow(cl, power) * spow(math.sin(longitude), power)
                vertices.append((center[0] + scale[0] * x, center[1] + scale[1] * y, center[2] + scale[2] * z))
        for ring in range(rings):
            for segment in range(segments):
                nxt = (segment + 1) % segments
                a = ring * segments + segment
                b = ring * segments + nxt
                c = (ring + 1) * segments + nxt
                d = (ring + 1) * segments + segment
                faces.extend(((a, b, c), (a, c, d)))
        self.add_part(name, material, vertices, faces)

    def cylinder(self, name: str, start, end, radius_a: float, radius_b: float, material: str, segments=16) -> None:
        start = np.asarray(start, dtype=float)
        end = np.asarray(end, dtype=float)
        axis = end - start
        axis /= np.linalg.norm(axis)
        helper = np.array((0.0, 1.0, 0.0)) if abs(axis[1]) < 0.9 else np.array((1.0, 0.0, 0.0))
        u = np.cross(axis, helper)
        u /= np.linalg.norm(u)
        v = np.cross(axis, u)
        vertices = [tuple(start), tuple(end)]
        for point, radius in ((start, radius_a), (end, radius_b)):
            for index in range(segments):
                angle = 2 * math.pi * index / segments
                vertices.append(tuple(point + radius * (u * math.cos(angle) + v * math.sin(angle))))
        faces = []
        for index in range(segments):
            nxt = (index + 1) % segments
            a, b = 2 + index, 2 + nxt
            c, d = 2 + segments + nxt, 2 + segments + index
            faces.extend(((a, b, c), (a, c, d), (0, b, a), (1, d, c)))
        self.add_part(name, material, vertices, faces)

    def cone(self, name: str, base, tip, radius: float, material: str, segments=16) -> None:
        self.cylinder(name, base, tip, radius, 0.015, material, segments)

    def torus(self, name: str, center, radii, tube: float, material: str, major_segments=32, tube_segments=8) -> None:
        vertices = []
        faces = []
        cx, cy, cz = center
        rx, ry = radii
        for i in range(major_segments):
            angle = 2 * math.pi * i / major_segments
            normal = np.array((math.cos(angle), math.sin(angle), 0.0))
            path = np.array((cx + rx * math.cos(angle), cy + ry * math.sin(angle), cz))
            for j in range(tube_segments):
                tube_angle = 2 * math.pi * j / tube_segments
                offset = normal * (tube * math.cos(tube_angle)) + np.array((0.0, 0.0, tube * math.sin(tube_angle)))
                vertices.append(tuple(path + offset))
        for i in range(major_segments):
            ni = (i + 1) % major_segments
            for j in range(tube_segments):
                nj = (j + 1) % tube_segments
                a = i * tube_segments + j
                b = ni * tube_segments + j
                c = ni * tube_segments + nj
                d = i * tube_segments + nj
                faces.extend(((a, b, c), (a, c, d)))
        self.add_part(name, material, vertices, faces)

    def export_obj(self, directory: Path) -> Path:
        directory.mkdir(parents=True, exist_ok=True)
        obj_path = directory / f"{self.name}.obj"
        mtl_path = directory / f"{self.name}.mtl"
        lines = [f"mtllib {mtl_path.name}", "s 1"]
        offset = 1
        for part in self.parts:
            lines.extend((f"o {part.name}", f"g {part.name}", f"usemtl {part.material}"))
            lines.extend(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}" for v in part.vertices)
            lines.extend(
                f"f {face[0] + offset} {face[1] + offset} {face[2] + offset}" for face in part.faces
            )
            offset += len(part.vertices)
        obj_path.write_text("\n".join(lines) + "\n", encoding="utf-8")

        mtl_lines = []
        for material in self.materials.values():
            mtl_lines.extend(
                (
                    f"newmtl {material.name}",
                    f"Kd {material.color[0]:.5f} {material.color[1]:.5f} {material.color[2]:.5f}",
                    f"Ks {material.metallic:.5f} {material.metallic:.5f} {material.metallic:.5f}",
                    f"Ns {(1.0 - material.roughness) * 900 + 10:.2f}",
                    "d 1.0",
                    "illum 2",
                )
            )
            if material.emission:
                mtl_lines.append(f"Ke {material.emission[0]:.5f} {material.emission[1]:.5f} {material.emission[2]:.5f}")
            mtl_lines.append("")
        mtl_path.write_text("\n".join(mtl_lines), encoding="utf-8")
        return obj_path


CYAN = Material("Lux_Cyan", (0.03, 0.72, 0.88), 0.05, 0.4)
WHITE = Material("Sport_White", (0.84, 0.87, 0.86), 0.0, 0.65)
PINK = Material("Hot_Pink", (0.95, 0.03, 0.32), 0.05, 0.35)
NAVY = Material("Deep_Navy", (0.015, 0.045, 0.10), 0.15, 0.38)
BLACK = Material("Grip_Black", (0.018, 0.022, 0.028), 0.05, 0.75)
EYE = Material("Eye_Cyan", (0.02, 0.5, 0.8), 0.15, 0.2, (0.0, 0.18, 0.35))
YELLOW = Material("Bastion_Yellow", (0.95, 0.55, 0.015), 0.45, 0.3)
GUNMETAL = Material("Gunmetal", (0.16, 0.19, 0.22), 0.7, 0.28)
SILVER = Material("Armor_Silver", (0.55, 0.58, 0.6), 0.65, 0.24)
EMISSIVE = Material("Core_Cyan", (0.01, 0.35, 0.65), 0.25, 0.18, (0.0, 0.65, 1.0))


def add_racket(model: Model, center, frame_material: str, accent_material: str) -> None:
    cx, cy, cz = center
    model.torus("Racket_Frame", (cx, cy + 0.34, cz), (0.24, 0.34), 0.035, frame_material)
    model.cylinder("Racket_Handle", (cx, cy - 0.55, cz), (cx, cy + 0.02, cz), 0.038, 0.048, BLACK.name, 12)
    model.cylinder("Racket_Throat_Left", (cx, cy + 0.02, cz), (cx - 0.15, cy + 0.13, cz), 0.027, 0.025, accent_material, 10)
    model.cylinder("Racket_Throat_Right", (cx, cy + 0.02, cz), (cx + 0.15, cy + 0.13, cz), 0.027, 0.025, accent_material, 10)
    for i in range(-4, 5):
        x = cx + i * 0.045
        half = 0.31 * math.sqrt(max(0.0, 1 - ((x - cx) / 0.24) ** 2))
        model.cylinder(f"Racket_String_V_{i+4}", (x, cy + 0.34 - half, cz), (x, cy + 0.34 + half, cz), 0.004, 0.004, NAVY.name, 6)
    for i in range(-6, 7):
        y = cy + 0.34 + i * 0.045
        half = 0.22 * math.sqrt(max(0.0, 1 - ((y - cy - 0.34) / 0.34) ** 2))
        model.cylinder(f"Racket_String_H_{i+6}", (cx - half, y, cz), (cx + half, y, cz), 0.004, 0.004, NAVY.name, 6)


def build_lux() -> Model:
    materials = {m.name: m for m in (CYAN, WHITE, PINK, NAVY, BLACK, EYE)}
    model = Model("Lux_Fox_Model_v1", materials)

    for side, x in (("L", -0.26), ("R", 0.26)):
        model.sphere(f"{side}_Shoe", (x, 0.13, 0.09), (0.23, 0.13, 0.35), WHITE.name, exponent=3.5)
        model.sphere(f"{side}_Shoe_Accent", (x, 0.10, 0.28), (0.18, 0.07, 0.13), PINK.name, exponent=3.0)
        model.cylinder(f"{side}_Shin", (x, 0.24, 0.02), (x, 0.76, 0.0), 0.125, 0.145, CYAN.name)
        model.sphere(f"{side}_Knee_Pad", (x, 0.83, 0.07), (0.17, 0.17, 0.13), NAVY.name, exponent=3.0)
        model.cylinder(f"{side}_Thigh", (x, 0.9, 0.0), (x * 0.78, 1.34, 0.0), 0.17, 0.205, CYAN.name)

    model.sphere("Shorts", (0.0, 1.35, 0.0), (0.43, 0.3, 0.27), NAVY.name, exponent=4.0)
    model.sphere("Torso", (0.0, 1.72, 0.0), (0.43, 0.5, 0.25), WHITE.name, exponent=2.7)
    model.sphere("Waist_Dark", (0.0, 1.39, -0.005), (0.39, 0.10, 0.255), NAVY.name, exponent=3.5)
    model.cylinder("Collar_Cyan", (-0.2, 2.03, 0.16), (0.2, 2.03, 0.16), 0.035, 0.035, CYAN.name, 12)
    model.cylinder("Chest_Accent", (-0.26, 1.90, 0.235), (0.0, 1.72, 0.255), 0.018, 0.018, PINK.name, 8)
    model.cylinder("Chest_Accent_R", (0.0, 1.72, 0.255), (0.26, 1.90, 0.235), 0.018, 0.018, PINK.name, 8)

    for side, sign in (("L", -1), ("R", 1)):
        shoulder = (0.45 * sign, 1.92, 0.0)
        elbow = (0.68 * sign, 1.48, 0.0)
        wrist = (0.77 * sign, 1.08, 0.03)
        model.sphere(f"{side}_Shoulder", shoulder, (0.18, 0.19, 0.18), CYAN.name)
        model.cylinder(f"{side}_Upper_Arm", shoulder, elbow, 0.145, 0.12, CYAN.name)
        model.cylinder(f"{side}_Forearm", elbow, wrist, 0.125, 0.095, CYAN.name)
        model.cylinder(f"{side}_Wrist_Band", (wrist[0], wrist[1] + 0.07, wrist[2]), wrist, 0.12, 0.11, PINK.name)
        model.sphere(f"{side}_Glove", (wrist[0], wrist[1] - 0.09, wrist[2] + 0.01), (0.12, 0.16, 0.105), BLACK.name, exponent=3.2)

    model.sphere("Head", (0.0, 2.30, 0.0), (0.37, 0.38, 0.34), CYAN.name)
    model.sphere("Muzzle", (0.0, 2.22, 0.31), (0.25, 0.17, 0.24), WHITE.name)
    model.sphere("Nose", (0.0, 2.23, 0.52), (0.075, 0.055, 0.06), BLACK.name)
    model.cone("Left_Ear", (-0.19, 2.50, 0.0), (-0.25, 2.92, 0.0), 0.18, CYAN.name)
    model.cone("Right_Ear", (0.19, 2.50, 0.0), (0.25, 2.92, 0.0), 0.18, CYAN.name)
    model.cone("Left_Ear_Inner", (-0.20, 2.55, 0.08), (-0.25, 2.82, 0.05), 0.09, WHITE.name)
    model.cone("Right_Ear_Inner", (0.20, 2.55, 0.08), (0.25, 2.82, 0.05), 0.09, WHITE.name)
    model.sphere("Left_Eye_Liner", (-0.13, 2.35, 0.315), (0.105, 0.07, 0.042), BLACK.name, exponent=3.5)
    model.sphere("Right_Eye_Liner", (0.13, 2.35, 0.315), (0.105, 0.07, 0.042), BLACK.name, exponent=3.5)
    model.sphere("Left_Eye", (-0.13, 2.35, 0.35), (0.057, 0.052, 0.025), EYE.name, exponent=3.0)
    model.sphere("Right_Eye", (0.13, 2.35, 0.35), (0.057, 0.052, 0.025), EYE.name, exponent=3.0)
    model.cone("Left_Cheek_Tuft", (-0.27, 2.23, 0.08), (-0.50, 2.19, 0.06), 0.13, WHITE.name, 12)
    model.cone("Right_Cheek_Tuft", (0.27, 2.23, 0.08), (0.50, 2.19, 0.06), 0.13, WHITE.name, 12)
    for index, x in enumerate((-0.21, -0.11, 0.0, 0.11, 0.21)):
        model.cone(f"Hair_Tuft_{index}", (x, 2.55, 0.0), (x * 1.18, 2.74 + 0.06 * (1 - abs(x) / 0.21), 0.05), 0.09, CYAN.name, 10)

    tail_points = ((0.05, 1.30, -0.18), (0.32, 1.10, -0.48), (0.55, 0.92, -0.62), (0.64, 0.70, -0.50))
    for index in range(len(tail_points) - 1):
        material = CYAN.name if index < 2 else WHITE.name
        radius = 0.23 - index * 0.035
        model.cylinder(f"Tail_{index}", tail_points[index], tail_points[index + 1], radius, radius * 0.92, material, 18)
        model.sphere(f"Tail_Joint_{index}", tail_points[index + 1], (radius, radius, radius), material)

    add_racket(model, (1.25, 1.03, 0.0), CYAN.name, PINK.name)
    return model


def build_bastion() -> Model:
    materials = {m.name: m for m in (YELLOW, NAVY, GUNMETAL, SILVER, BLACK, EMISSIVE)}
    model = Model("Bastion_Robot_Model_v1", materials)

    for side, x in (("L", -0.34), ("R", 0.34)):
        model.sphere(f"{side}_Foot", (x, 0.16, 0.08), (0.34, 0.18, 0.42), NAVY.name, exponent=5.0)
        model.sphere(f"{side}_Toe_Armor", (x, 0.19, 0.29), (0.30, 0.14, 0.19), YELLOW.name, exponent=5.0)
        model.cylinder(f"{side}_Ankle", (x, 0.30, 0.0), (x, 0.48, 0.0), 0.16, 0.16, GUNMETAL.name)
        model.sphere(f"{side}_Shin_Armor", (x, 0.68, 0.05), (0.27, 0.35, 0.26), SILVER.name, exponent=4.5)
        model.sphere(f"{side}_Knee", (x, 0.98, 0.13), (0.22, 0.22, 0.19), NAVY.name, exponent=3.5)
        model.sphere(f"{side}_Knee_Cap", (x, 1.00, 0.29), (0.16, 0.16, 0.07), YELLOW.name, exponent=4.0)
        model.cylinder(f"{side}_Thigh", (x, 1.08, 0.0), (x * 0.82, 1.43, 0.0), 0.23, 0.27, GUNMETAL.name)
        model.sphere(f"{side}_Thigh_Armor", (x * 0.9, 1.27, 0.10), (0.28, 0.27, 0.23), SILVER.name, exponent=4.0)

    model.sphere("Pelvis", (0.0, 1.46, 0.0), (0.48, 0.32, 0.36), NAVY.name, exponent=5.0)
    model.sphere("Torso_Core", (0.0, 1.91, 0.0), (0.61, 0.55, 0.42), GUNMETAL.name, exponent=4.5)
    model.sphere("Chest_Armor", (0.0, 1.98, 0.31), (0.43, 0.39, 0.16), SILVER.name, exponent=5.0)
    model.sphere("Upper_Chest", (0.0, 2.27, 0.12), (0.52, 0.30, 0.37), YELLOW.name, exponent=5.0)
    model.sphere("Backpack", (0.0, 2.02, -0.36), (0.45, 0.49, 0.20), NAVY.name, exponent=5.0)
    model.sphere("Chest_Core", (0.0, 1.88, 0.47), (0.12, 0.11, 0.055), EMISSIVE.name, exponent=4.0)

    for side, sign in (("L", -1), ("R", 1)):
        shoulder = (0.67 * sign, 2.24, 0.0)
        elbow = (0.84 * sign, 1.65, 0.0)
        wrist = (0.88 * sign, 1.18, 0.05)
        model.sphere(f"{side}_Shoulder_Joint", shoulder, (0.25, 0.25, 0.25), GUNMETAL.name)
        model.sphere(f"{side}_Shoulder_Armor", (0.78 * sign, 2.28, 0.0), (0.43, 0.40, 0.43), YELLOW.name, exponent=5.0)
        model.sphere(f"{side}_Shoulder_Navy", (0.84 * sign, 2.29, -0.02), (0.32, 0.31, 0.35), NAVY.name, exponent=5.0)
        model.cylinder(f"{side}_Upper_Arm", shoulder, elbow, 0.23, 0.20, GUNMETAL.name)
        model.sphere(f"{side}_Elbow", elbow, (0.22, 0.21, 0.22), NAVY.name)
        model.sphere(f"{side}_Forearm_Armor", ((elbow[0] + wrist[0]) / 2, 1.42, 0.05), (0.26, 0.32, 0.29), YELLOW.name, exponent=5.0)
        model.sphere(f"{side}_Forearm_Panel", ((elbow[0] + wrist[0]) / 2, 1.42, 0.29), (0.12, 0.20, 0.055), EMISSIVE.name, exponent=4.0)
        model.sphere(f"{side}_Fist", (wrist[0], 1.03, 0.07), (0.23, 0.24, 0.22), GUNMETAL.name, exponent=5.0)

    model.sphere("Head", (0.0, 2.43, 0.10), (0.32, 0.27, 0.29), GUNMETAL.name, exponent=5.0)
    model.sphere("Helmet", (0.0, 2.52, 0.07), (0.34, 0.22, 0.30), YELLOW.name, exponent=5.0)
    model.sphere("Visor", (0.0, 2.45, 0.36), (0.23, 0.075, 0.045), EMISSIVE.name, exponent=5.0)
    model.sphere("Jaw", (0.0, 2.31, 0.28), (0.22, 0.10, 0.10), NAVY.name, exponent=5.0)
    for index, x in enumerate((-0.24, 0.0, 0.24)):
        model.sphere(f"Back_Spine_{index}", (x, 1.95 + 0.16 * index, -0.53), (0.13, 0.17, 0.07), GUNMETAL.name, exponent=4.0)

    add_racket(model, (1.48, 1.08, 0.0), YELLOW.name, NAVY.name)
    return model


def render_preview(model: Model, output: Path) -> None:
    views = (
        ("FRONT", np.array((0.0, 0.0, 1.0)), np.array((1.0, 0.0, 0.0))),
        ("LEFT", np.array((1.0, 0.0, 0.0)), np.array((0.0, 0.0, -1.0))),
        ("BACK", np.array((0.0, 0.0, -1.0)), np.array((-1.0, 0.0, 0.0))),
        ("3/4", np.array((0.72, 0.18, 0.67)), np.array((0.68, 0.0, -0.73))),
    )
    panel_width, height = 420, 620
    canvas = Image.new("RGB", (panel_width * len(views), height), (232, 230, 226))
    light = np.array((-0.3, 0.75, 0.58))
    light /= np.linalg.norm(light)

    for panel, (label, camera, right) in enumerate(views):
        camera = camera / np.linalg.norm(camera)
        right = right / np.linalg.norm(right)
        up = np.cross(camera, right)
        triangles = []
        for part in model.parts:
            color = np.asarray(model.materials[part.material].color) * 255
            for face in part.faces:
                tri = part.vertices[face]
                normal = np.cross(tri[1] - tri[0], tri[2] - tri[0])
                length = np.linalg.norm(normal)
                if length < 1e-8:
                    continue
                normal /= length
                intensity = 0.42 + 0.58 * abs(float(np.dot(normal, light)))
                shaded = tuple(int(np.clip(value * intensity, 0, 255)) for value in color)
                projected = [(float(np.dot(v, right)), float(np.dot(v, up))) for v in tri]
                depth = float(np.mean(tri @ camera))
                triangles.append((depth, projected, shaded))
        triangles.sort(key=lambda value: value[0])
        draw = ImageDraw.Draw(canvas)
        scale = 165
        ox = panel * panel_width + panel_width // 2
        oy = height - 58
        for _, points, color in triangles:
            polygon = [(ox + x * scale, oy - y * scale) for x, y in points]
            draw.polygon(polygon, fill=color)
        draw.line((panel * panel_width, 0, panel * panel_width, height), fill=(190, 188, 184), width=2)
        draw.text((panel * panel_width + 18, 18), label, fill=(35, 42, 52))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)


def main() -> None:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    for model in (build_lux(), build_bastion()):
        obj = model.export_obj(MODEL_DIR)
        preview = PREVIEW_DIR / f"{model.name}_Preview.png"
        render_preview(model, preview)
        vertex_count = sum(len(part.vertices) for part in model.parts)
        triangle_count = sum(len(part.faces) for part in model.parts)
        print(f"{obj.name}: {len(model.parts)} parts, {vertex_count} vertices, {triangle_count} triangles")
        print(f"preview: {preview}")


if __name__ == "__main__":
    main()
