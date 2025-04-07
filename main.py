import json
import random
from itertools import chain
from typing import List, Dict, Union
import concurrent.futures
import matplotlib.pyplot as plt
from collections import defaultdict
import matplotlib
import numpy as np
from door import Door
from graph import Graph
from room import Room
from stairs import Stair

matplotlib.use('TkAgg')  # Oder 'Agg', falls TkAgg nicht funktioniert


def parse_geojson(geojson_string):
    """
    Parses a GeoJSON file and extracts rooms, doors, and stairs, categorizing them by level.

    Args:
        geojson_string (dict): The parsed GeoJSON data.

    Returns:
        List[Dict[int, List[Room]], Dict[int, List[Door]], Dict[int, List[Stair]]]:
        A list containing dictionaries for rooms, doors, and stairs, indexed by level.
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

    Stair.link_stairs(stairs)

    Room.setup_all_rooms(rooms + stairs)

    link_rooms(rooms, doors, stairs)

    setup_graphs_parallel(rooms, stairs)

    return [rooms, doors, stairs]


def link_rooms(rooms: List[Room], doors: List[Door], stairs: List[Stair]):
    """
       Links doors to rooms and stairs if they are on the outline of a structure.
    """
    for room in chain(rooms, stairs):
        print("linking doors for room", room.name)
        for door in doors:

            if room.is_door_on_outline(door):
                door.add_room(room)
                room.doors.append(door)




def setup_graphs_parallel(rooms: List[Room], stairs: List[Stair]):
    """
    Initialisiert die Graphen für alle Räume und Treppen, mit Option für parallele oder sequentielle Verarbeitung.

    :param rooms: Liste von Room-Objekten.
    :param stairs: Liste von Stair-Objekten.
    """
    print("Started processing graphs for individual rooms")

    rooms_and_stairs = rooms + stairs

    for item in rooms_and_stairs:
        item.setup_paths()

    print("Graph processing finished")



def visualize_level(rooms, stairs, level, max_features):
    plt.figure(figsize=(12, 12))

    plotted_features = 0

    def random_color():
        return "#{:06x}".format(random.randint(0, 0xFFFFFF))

    for room in rooms:
        if room.level != level:
            continue

        plotted_features += 1
        if plotted_features >= max_features:
            break
        room.plot(color=random_color())


    # Treppen kannst du ggf. ähnlich als Methode einer `Stair`-Klasse auslagern
    for stair in stairs:
        if stairs != level:
            continue

        plotted_features +=1
        if plotted_features >= max_features:
            break
        if len(stair.coordinates) > 0:
            x, y = zip(*stair.coordinates) if isinstance(stair.coordinates[0], tuple) else ([], [])
            plt.plot(x, y, color="green", alpha=0.5)

    plt.gca().set_aspect(1 / np.cos(np.deg2rad(50.8)))
    plt.xlabel("X Coordinate")
    plt.ylabel("Y Coordinate")
    plt.grid(True)

    plt.show()


if __name__ == "__main__":
    LEVEL_TO_DISPLAY = "3"  # Change this to the level you want to visualize

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    rooms, doors, stairs = parse_geojson(geojson_data)

    #visualize_level(rooms, stairs, 7, 1000)

    with open("resources/graph.json", "w") as f:
        f.write(rooms[LEVEL_TO_DISPLAY][0].graph.export_json())

    with open("C:\\Users\\nico\\Desktop\\Alles\\Projekte\\geoJsonParser\\navigator\\lib\\resources\\graph.json", "w") as f:
        f.write(rooms[LEVEL_TO_DISPLAY][0].graph.export_json())


