"""Распаковывает текстуры, вшитые в FBX, рядом с моделью.

Unity при импорте модели ищет текстуры по имени файла из RelativeFilename внутри
папки `<имя модели>.fbm`, лежащей рядом с .fbx. В этих моделях пути абсолютные и
указывают на чужой диск (G:\\Мой диск\\Igra-PD\\...), поэтому Unity ничего не
находит и материалы остаются однотонными.

Скрипт вытаскивает PNG-байты из узлов Video и кладёт их в `<имя>.fbm` под теми
именами, которые записаны в FBX (Image_0, Image_1, ...). После этого импорт
разрешает текстуры штатным путём, без ремапов и сгенерированных материалов.

Использование:
    python Tools/fbx_textures.py Assets/NpcCars/*/[!.]*.fbx
"""
import os
import struct
import sys

FBX_MAGIC = b"Kaydara FBX Binary  \x00"


class Node:
    __slots__ = ("name", "props", "children")

    def __init__(self, name, props, children):
        self.name = name
        self.props = props
        self.children = children


def _read_props(data, offset, count):
    props = []
    for _ in range(count):
        code = data[offset:offset + 1]
        offset += 1
        if code == b"Y":
            props.append(struct.unpack_from("<h", data, offset)[0])
            offset += 2
        elif code == b"C":
            props.append(struct.unpack_from("<B", data, offset)[0] != 0)
            offset += 1
        elif code == b"I":
            props.append(struct.unpack_from("<i", data, offset)[0])
            offset += 4
        elif code == b"F":
            props.append(struct.unpack_from("<f", data, offset)[0])
            offset += 4
        elif code == b"D":
            props.append(struct.unpack_from("<d", data, offset)[0])
            offset += 8
        elif code == b"L":
            props.append(struct.unpack_from("<q", data, offset)[0])
            offset += 8
        elif code in (b"f", b"d", b"l", b"i", b"b"):
            # Для массивов: длина в элементах, признак сжатия, длина в байтах.
            count, encoding, byte_length = struct.unpack_from("<III", data, offset)
            offset += 12
            payload = data[offset:offset + byte_length]
            offset += byte_length
            if encoding:
                import zlib
                payload = zlib.decompress(payload)
            formats = {b"f": "f", b"d": "d", b"l": "q", b"i": "i", b"b": "b"}
            fmt = formats[code]
            props.append(list(struct.unpack_from("<" + fmt * count, payload)))
        elif code in (b"S", b"R"):
            length = struct.unpack_from("<I", data, offset)[0]
            offset += 4
            raw = data[offset:offset + length]
            offset += length
            props.append(raw if code == b"R" else raw.decode("utf-8", "replace"))
        else:
            raise ValueError("unknown property code %r at %d" % (code, offset - 1))
    return props, offset


def _read_node(data, offset):
    if offset + 13 > len(data):
        return None, offset
    end_offset, num_props, _prop_len, name_len = struct.unpack_from("<IIIB", data, offset)
    if end_offset == 0:
        return None, offset + 13
    name = data[offset + 13:offset + 13 + name_len].decode("ascii", "replace")
    props, cursor = _read_props(data, offset + 13 + name_len, num_props)
    children = []
    while cursor < end_offset - 13:
        child, cursor = _read_node(data, cursor)
        if child is None:
            break
        children.append(child)
    return Node(name, props, children), end_offset


def read_fbx(path):
    with open(path, "rb") as handle:
        data = handle.read()
    if not data.startswith(FBX_MAGIC):
        raise ValueError("not a binary FBX: " + path)
    offset = 27
    roots = []
    while True:
        node, offset = _read_node(data, offset)
        if node is None:
            break
        roots.append(node)
    return roots


def _text(value):
    if isinstance(value, bytes):
        return value.decode("ascii", "replace").split("\x00")[0]
    return str(value).split("\x00")[0]


def _referenced_name(texture):
    """Имя файла, которое FBX ожидает увидеть в папке .fbm."""
    for child in texture.children:
        if child.name in ("RelativeFilename", "FileName") and child.props:
            raw = child.props[0]
            name = os.path.basename(_text(raw).replace("\\", "/"))
            if name:
                return name
    return None


def _content_bytes(node):
    for child in node.children:
        if child.name == "Content" and child.props:
            return child.props[0]
    return None


def _extension(blob):
    if blob[:4] == b"\x89PNG":
        return ".png"
    if blob[:3] == b"\xff\xd8\xff":
        return ".jpg"
    return ".bin"


def collect(fbx_path):
    """Возвращает список (имя материала, имя файла из FBX, байты картинки)."""
    roots = read_fbx(fbx_path)
    objects, connections = [], []
    for root in roots:
        if root.name == "Objects":
            objects = root.children
        elif root.name == "Connections":
            connections = root.children

    by_id = {o.props[0]: o for o in objects if len(o.props) >= 2 and isinstance(o.props[0], int)}
    video_for_texture = {}
    for conn in connections:
        if len(conn.props) < 3:
            continue
        video, texture = by_id.get(conn.props[1]), by_id.get(conn.props[2])
        if video is not None and texture is not None and video.name == "Video" and texture.name == "Texture":
            video_for_texture[conn.props[2]] = video

    results = []
    for conn in connections:
        if len(conn.props) < 3:
            continue
        texture, material = by_id.get(conn.props[1]), by_id.get(conn.props[2])
        if texture is None or material is None:
            continue
        # Текстура подключена к материалу как DiffuseColor — это и есть base color.
        if texture.name != "Texture" or material.name != "Material":
            continue
        blob = _content_bytes(texture)
        if blob is None:
            video = video_for_texture.get(conn.props[1])
            if video is not None:
                blob = _content_bytes(video)
        if not blob:
            continue
        results.append((_text(material.props[1]), _referenced_name(texture), blob))
    return results


def main(fbx_path):
    folder = os.path.dirname(os.path.abspath(fbx_path))
    stem = os.path.splitext(os.path.basename(fbx_path))[0]
    target = os.path.join(folder, stem + ".fbm")
    os.makedirs(target, exist_ok=True)

    entries = collect(fbx_path)
    written = set()
    for material_name, fbx_name, blob in entries:
        # Имя файла — по материалу модели. Имя из FBX (Image_N) не помогает:
        # импортёр ищет по абсолютному пути внутри FBX и всё равно ничего не
        # находит, поэтому связку "материал → картинка" делает
        # NpcCarTextureBinder, а дублировать картинки вторым комплектом
        # незачем — это лишние 350 КБ в репозитории.
        extension = _extension(blob)
        name = material_name + extension
        if name in written:
            continue
        written.add(name)
        with open(os.path.join(target, name), "wb") as handle:
            handle.write(blob)

    print("%s: %d texture(s) -> %s" % (os.path.basename(fbx_path), len(entries), os.path.basename(target)))
    return 0


if __name__ == "__main__":
    status = 0
    for argument in sys.argv[1:]:
        status |= main(argument)
    sys.exit(status)
