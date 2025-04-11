import json
import math
import random
import matplotlib.pyplot as plt
import matplotlib
from door import Door
from graph import Graph
from room import Room
from stairs import Stair
import numpy as np
import trimesh
from typing import List

matplotlib.use('TkAgg')


def parse_geojson_to_graph(geojson_string) -> Graph:
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



    with open("resources/test.obj", "w") as f:
        f.write(parse_obj_from_rooms(rooms, doors, height=2.0))
    # todo remove
    return


    Stair.link_stairs(stairs)
    Room.setup_all_rooms(rooms + stairs, doors)

    visualize_level(rooms, stairs)

    # cutting off the broken parts of the graph (like rooms without doors / rooms only accessible from the outside)
    graph.keep_largest_component()
    return graph


def parse_obj_from_rooms(all_rooms: List['Room'], all_doors: List['Door'], height: float = 2.0) -> str:
    all_rooms = [room for room in all_rooms if room.level == 3]
    if not all_rooms:
        return ""

    origin_lat, origin_lon = all_rooms[0].coordinates[0]

    all_vertices = []
    all_faces = []
    vertex_offset = 0

    for room in all_rooms:
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

    return merge_vertices_in_obj(obj_str)



# extrusion upwards (currently it has to do double the work)
def merge_vertices_in_obj(obj_str: str, tolerance: float = 0.01) -> str:
    """
    Merge vertices that are within a certain tolerance in an OBJ string.

    :param obj_str: The OBJ string.
    :param tolerance: The maximum distance between vertices to consider them close.
    :return: The updated OBJ string with merged vertices.
    """

    # todo also optimize the faces at the end of this method (overlapping faces)

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


def visualize_level(rooms, stairs, level=3, max_features=10000):
    """Function to visualize the rooms and stairs of a given level. (Debug feature)"""

    plt.figure(figsize=(12, 12))
    plotted_features = 0

    def random_color():
        return "#{:06x}".format(random.randint(0, 0xFFFFFF))

    # plot rooms in random colors
    for room in rooms + stairs:
        if room.level != level:
            continue

        plotted_features += 1
        if plotted_features >= max_features:
            break

        room.plot(color=random_color())


    plt.gca().set_aspect(1 / np.cos(np.deg2rad(50.8)))
    plt.xlabel("lat Coordinate")
    plt.ylabel("lon Coordinate")
    plt.grid(True)

    plt.show()




if __name__ == "__main__":

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    graph: Graph = parse_geojson_to_graph(geojson_data)


    with open("resources/graph.json", "w") as f:
        f.write(graph.export_json())

    with open("C:\\Users\\nico\\Desktop\\Alles\\Projekte\\geoJsonParser\\navigatorApp\\lib\\resources\\graph.json", "w") as f:
        f.write(graph.export_json())

