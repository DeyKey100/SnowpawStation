"""Печатает таблицу: материал -> текстура (или цвет), для всех моделей NpcCars."""
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fbx_textures import read_fbx


def prop_map(node):
    values = {}
    for child in node.children:
        if child.name != "Properties70":
            continue
        for prop in child.children:
            key = prop.props[0].decode("ascii", "replace").split("\x00")[0]
            rest = []
            for value in prop.props[4:]:
                if isinstance(value, bytes):
                    rest.append(value.decode("ascii", "replace").split("\x00")[0])
                else:
                    rest.append(value)
            values[key] = rest
    return values


def main(path):
    _version, roots = read_fbx(path)
    objects, connections = [], []
    for root in roots:
        if root.name == "Objects":
            objects = root.children
        elif root.name == "Connections":
            connections = root.children

    by_id = {o.props[0]: o for o in objects if len(o.props) >= 2 and isinstance(o.props[0], int)}

    tex_for_material = {}
    for conn in connections:
        if len(conn.props) < 4:
            continue
        src, dst = by_id.get(conn.props[1]), by_id.get(conn.props[2])
        if src is None or dst is None:
            continue
        if src.name == "Texture" and dst.name == "Material":
            rel = ""
            for child in src.children:
                if child.name == "RelativeFilename" and child.props:
                    rel = child.props[0].decode("ascii", "replace").split("\x00")[0]
            tex_for_material[dst.props[1].decode("ascii", "replace").split("\x00")[0]] = os.path.basename(rel.replace("\\", "/"))

    model = os.path.splitext(os.path.basename(path))[0]
    print("### " + model)
    for obj in objects:
        if obj.name != "Material":
            continue
        name = obj.props[1].decode("ascii", "replace").split("\x00")[0]
        props = prop_map(obj)
        color = props.get("DiffuseColor", [0.8, 0.8, 0.8])
        tex = tex_for_material.get(name, "")
        print("%s|%s|%.4f,%.4f,%.4f" % (name, tex, color[0], color[1], color[2]))


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        main(arg)
