"""Печатает связи Texture -> Material во FBX, чтобы понять, какие карты куда идут."""
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fbx_textures import read_fbx, Node  # noqa: E402


def walk(node, out):
    out.append(node)
    for child in node.children:
        walk(child, out)


def main(path):
    version, roots = read_fbx(path)
    objects = []
    connections = []
    for root in roots:
        if root.name == "Objects":
            for child in root.children:
                objects.append(child)
        if root.name == "Connections":
            for child in root.children:
                connections.append(child)

    by_id = {}
    for obj in objects:
        if len(obj.props) >= 2 and isinstance(obj.props[0], int):
            by_id[obj.props[0]] = obj

    print("version=%d objects=%d connections=%d" % (version, len(objects), len(connections)))

    # Материалы и их свойства
    for obj in objects:
        if obj.name != "Material":
            continue
        name = obj.props[1].decode("ascii", "replace").split("\x00")[0]
        print("MATERIAL %s id=%d" % (name, obj.props[0]))
        for child in obj.children:
            if child.name == "Properties70":
                for prop in child.children:
                    key = prop.props[0].decode("ascii", "replace")
                    if key.startswith("3ds") or "Map" in key or "Texture" in key or key in (
                            "DiffuseColor", "Diffuse", "Maya|baseColor", "Maya|base_color_texture"):
                        printed = []
                        for value in prop.props[4:]:
                            if isinstance(value, bytes):
                                printed.append(value.decode("ascii", "replace").split("\x00")[0])
                            else:
                                printed.append(value)
                        print("    %s = %s" % (key, printed))

    # Связи текстура -> материал
    for conn in connections:
        if len(conn.props) < 3:
            continue
        kind = conn.props[0].decode("ascii", "replace").split("\x00")[0]
        src = conn.props[1]
        dst = conn.props[2]
        src_obj = by_id.get(src)
        dst_obj = by_id.get(dst)
        if src_obj is None or dst_obj is None:
            continue
        if src_obj.name in ("Texture", "Video") and dst_obj.name == "Material":
            rel = None
            for child in src_obj.children:
                if child.name == "RelativeFilename" and child.props:
                    rel = child.props[0].decode("ascii", "replace").split("\x00")[0]
            prop_name = conn.props[3].decode("ascii", "replace").split("\x00")[0] if len(conn.props) > 3 and isinstance(conn.props[3], bytes) else "?"
            print("LINK %s -> %s.%s  (%s)" % (
                src_obj.props[1].decode("ascii", "replace").split("\x00")[0],
                dst_obj.props[1].decode("ascii", "replace").split("\x00")[0],
                prop_name, rel))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
