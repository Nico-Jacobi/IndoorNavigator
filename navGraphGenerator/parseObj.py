import json
import math
import matplotlib
from shapely import Polygon as shapelyPolygon
from door import Door
from graph import Graph
from room import Room
from stairs import Stair
from typing import List, Tuple
from polygon import Polygon2D

from wavefront import Wavefront

matplotlib.use('TkAgg')


def parse_obj_files(geojson_string) -> str:
    """
    Parses a GeoJSON file and extracts rooms, doors, and stairs.
    Sets them up, and returns a graph representation of the building.
    """
    graph = Graph()

    doors = []
    stairs = []
    rooms = []
    broken = []

    for feature in geojson_string.get("features", []):
        properties = feature.get("properties")
        if not isinstance(properties, dict):
            broken.append(feature)
            continue

        level = properties.get("level")
        is_door = properties.get("door") == "yes"
        is_stairs = properties.get("stairs") == "yes"

        if level is not None:
            if is_door:
                doors.append(Door(feature, graph))
            elif is_stairs:
                stairs.append(Stair(feature, graph))
            else:
                if len(feature.get("geometry", {}).get("coordinates", [])) <= 2:
                    continue
                rooms.append(Room(feature, graph))

        else:
            broken.append(feature)

    all_rooms = [room for room in rooms + stairs if room.level == 3][10:100]

    Stair.link_stairs(stairs)
    Room.setup_all_rooms(all_rooms, doors) # this needs to be done here, so the geometry is right, maybe just fix that
    # thats faster

    origin_lat, origin_lon = rooms[0].coordinates[0]

    return parse_ground_floor_obj_from_rooms(all_rooms, origin_lat, origin_lon)





def parse_walls_obj_from_rooms(all_rooms: List['Room'], origin_lat: float, origin_lon: float) -> str:
    if not all_rooms:
        return ""

    all_vertices = []
    all_faces = []
    vertex_offset = 0

    for room in all_rooms:
        #todo
        wavefront = room.get_2d_outline_wavefront(origin_lat, origin_lon)

        if not wavefront:
            continue

        lines = wavefront.splitlines()
        for line in lines:
            if line.startswith('v '):
                all_vertices.append(line)
            elif line.startswith('f '):
                # face line -> shift indices
                parts = line.split()
                shifted_face = ['f']
                for part in parts[1:]:
                    # Support 'f v' or 'f v/vt' or 'f v/vt/vn' style
                    sub_parts = part.split('/')
                    sub_parts[0] = str(int(sub_parts[0]) + vertex_offset)
                    shifted_face.append('/'.join(sub_parts))
                all_faces.append(' '.join(shifted_face))

        # Count new vertices added
        new_vertices = sum(1 for l in lines if l.startswith('v '))
        vertex_offset += new_vertices

    obj_str = "\n".join(all_vertices + all_faces)

    obj_str = optimize_obj(obj_str)

    return extrude_obj(obj_str)

def extrude_obj(obj_string: str, height: float = 2.0) -> str:
    """
    Takes an OBJ string representing a 2D shape and extrudes it in the y direction.

    Args:
        obj_string: String containing the contents of a Wavefront OBJ file
        height: The height to extrude the shape (default: 2.0)

    Returns:
        String containing the extruded 3D OBJ model with all necessary faces
    """
    lines = obj_string.strip().split('\n')
    vertices = []
    faces = []
    other_lines = []

    # First pass: collect vertices, faces and other lines
    for line in lines:
        line = line.strip()
        if line.startswith('v '):
            vertices.append(line)
        elif line.startswith('f '):
            faces.append(line)
        else:
            other_lines.append(line)

    # Extract vertex coordinates
    parsed_vertices = []
    for v in vertices:
        parts = v.split()
        # Handle both "v x y z" and "v x y" formats
        if len(parts) >= 4:  # 3D vertex
            x, y, z = float(parts[1]), float(parts[2]), float(parts[3])
        else:  # 2D vertex (assume z=0)
            x, y = float(parts[1]), float(parts[2])
            z = 0.0
        parsed_vertices.append((x, y, z))

    # Create duplicated vertices with extruded height
    new_vertices = []
    for i, (x, y, z) in enumerate(parsed_vertices):
        # Original vertex (bottom)
        new_vertices.append(f"v {x} {y} {z}")
        # Extruded vertex (top)
        new_vertices.append(f"v {x} {y + height} {z}")

    # Parse faces to get vertex indices
    parsed_faces = []
    for face in faces:
        parts = face.split()
        face_indices = []
        for part in parts[1:]:  # Skip the 'f' prefix
            # Handle different face formats (v, v/vt, v/vt/vn, v//vn)
            vertex_idx = int(part.split('/')[0])
            face_indices.append(vertex_idx)
        parsed_faces.append(face_indices)

    # Create new faces
    new_faces = []

    # 1. Keep original faces (bottom)
    for face_indices in parsed_faces:
        bottom_face = ["f"]
        for idx in face_indices:
            # The original vertices are now at positions 2*idx-1
            bottom_face.append(str(2 * idx - 1))
        new_faces.append(" ".join(bottom_face))

    # 2. Create top faces (extruded)
    for face_indices in parsed_faces:
        top_face = ["f"]
        # Reverse the order for proper face orientation
        for idx in reversed(face_indices):
            # The extruded vertices are at positions 2*idx
            top_face.append(str(2 * idx))
        new_faces.append(" ".join(top_face))

    # 3. Create side faces (quads connecting top and bottom)
    for face_indices in parsed_faces:
        for i in range(len(face_indices)):
            v1_idx = face_indices[i]
            v2_idx = face_indices[(i + 1) % len(face_indices)]

            # Create a quad face for each edge
            side_face = [
                "f",
                str(2 * v1_idx - 1),  # Bottom vertex 1
                str(2 * v2_idx - 1),  # Bottom vertex 2
                str(2 * v2_idx),  # Top vertex 2
                str(2 * v1_idx)  # Top vertex 1
            ]
            new_faces.append(" ".join(side_face))

    # Combine everything back
    result = []
    for line in other_lines:
        if not any(line.startswith(prefix) for prefix in ['v ', 'f ']):
            result.append(line)

    result.extend(new_vertices)
    result.extend(new_faces)

    return '\n'.join(result)

def optimize_obj(obj_str: str) -> str:
    merged = merge_vertices_in_obj(obj_str)
    removed = remove_redundant_faces(merged)
    filtered = remove_redundant_vertices(removed)

    return filtered
def merge_vertices_in_obj(obj_str: str, tolerance: float = 0.01) -> str:
    """
    Merge vertices that are within a certain tolerance in an OBJ string.

    :param obj_str: The OBJ string.
    :param tolerance: The maximum distance between vertices to consider them close.
    :return: The updated OBJ string with merged vertices.
    """

    # Parse the OBJ string to get the vertices and faces
    vertices = []
    faces = []

    # Read the vertices and faces from the OBJ string
    for line in obj_str.splitlines():
        if line.startswith('v '):
            # Extract vertex (x, y, z) coordinates
            _, x, y, z = line.split()
            vertices.append((float(x), float(y), float(z)))
        elif line.startswith('f '):
            # Store the face index lines
            faces.append(line)

    # Function to calculate the Euclidean distance between two vertices
    def distance(v1, v2):
        return math.sqrt((v1[0] - v2[0]) ** 2 + (v1[1] - v2[1]) ** 2 + (v1[2] - v2[2]) ** 2)

    # Merging vertices
    merged_vertices = []
    vertex_map = {}  # A map from the original vertex index to the new merged vertex index
    for i, vertex in enumerate(vertices):
        found = False
        for j, merged_vertex in enumerate(merged_vertices):
            if distance(vertex, merged_vertex) <= tolerance:
                # If vertex is close to an already merged vertex, use the index of the merged vertex
                vertex_map[i] = j
                found = True
                break
        if not found:
            # If no close vertex found, add it as a new vertex
            vertex_map[i] = len(merged_vertices)
            merged_vertices.append(vertex)

    # Update faces to use the new merged vertex indices
    updated_faces = []
    for face in faces:
        updated_face = []
        for index in face.split()[1:]:
            # Adjust for 1-based indexing of OBJ (so subtract 1 and then find the new index)
            original_index = int(index) - 1
            updated_face.append(str(vertex_map[original_index] + 1))  # Convert back to 1-based index
        updated_faces.append('f ' + ' '.join(updated_face))


    # Create the updated OBJ string with the 'v' prefix for vertices
    updated_obj_str = "\n".join([f"v {v[0]} {v[1]} {v[2]}" for v in merged_vertices]) + '\n' + '\n'.join(updated_faces)

    return updated_obj_str
def remove_redundant_faces(obj_string: str) -> str:
    """
    Removes Faces covered by other faces
    Assumes there is no y direction in the vertecies
    """

    vertices: List[Tuple[float, float]] = []
    faces: List[Tuple[shapelyPolygon, str]] = []


    for value in obj_string.splitlines():
        if value.startswith('v '):
            # assumes all faces are planar in y (y == 0)
            _, x, y, z = value.split()
            vertices.append((float(x), float(z)))


    for value in obj_string.splitlines():
        if value.startswith('f '):
            # assumes all faces are planar in y (y == 0)
            _, i1, i2, i3, i4 = value.split()
            i1 = int(i1)
            i2 = int(i2)
            i3 = int(i3)
            i4 = int(i4)

            faces.append((shapelyPolygon([vertices[i1-1],
                                 vertices[i2-1],
                                 vertices[i3-1],
                                 vertices[i4-1],
                                  ]),  value))


    to_remove = set()
    to_keep = set()     # as some faces are duplicates or mirrors they would otherwise be completely removed
    for face1 in faces:
        for face2 in faces:
            if face1 is face2:
                continue  # don't compare a face with itself

            if face1[0].covers(face2[0]):
                to_remove.add(face2)

                if face2[0].covers(face1[0]):
                    to_keep.add(face1)

    faces = list((set(faces) - to_remove).union(to_keep))


    return "\n".join([f"v {v[0]} 0 {v[1]}" for v in vertices]) + '\n' + '\n'.join([face[1] for face in faces])
def remove_redundant_vertices(obj_string: str) -> str:
    """removes unused vertices from the obj string"""

    lines = obj_string.strip().split('\n')
    vertices = []
    faces = []

    # Split lines into vertices and faces
    for line in lines:
        if line.startswith('v '):
            vertices.append(line)
        elif line.startswith('f '):
            faces.append(line)

    # Collect used vertex indices
    used_indices = set()
    for face in faces:
        for part in face.split()[1:]:
            used_indices.add(int(part))

    # Rebuild vertex list and index mapping
    new_vertices = []
    index_map = {}
    new_index = 1
    for i, vertex in enumerate(vertices, start=1):
        if i in used_indices:
            index_map[i] = new_index
            new_vertices.append(vertex)
            new_index += 1

    # Update face indices
    new_faces = []
    for face in faces:
        parts = face.split()
        updated_face = 'f ' + ' '.join(str(index_map[int(p)]) for p in parts[1:])
        new_faces.append(updated_face)

    return '\n'.join(new_vertices + new_faces)


def parse_ground_floor_obj_from_rooms(rooms: List['Room'], origin_lat: float, origin_lon: float) -> str:

    wavefront = Wavefront()
    for room in rooms:

        geometry = room.get_meter_geometry(origin_lat, origin_lon)

        if not geometry.is_empty:
            polygon = Polygon2D.from_shapely(geometry)
            polygon.to_wavefront(wavefront)

    wavefront.merge_vertices()
    return str(wavefront)






if __name__ == "__main__":

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    obj_string: str = parse_obj_files(geojson_data)


    with open("resources/test.obj", "w") as f:
        f.write(obj_string)




