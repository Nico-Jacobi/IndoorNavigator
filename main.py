import json
import random
from itertools import chain
from typing import List, Dict
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

    doors_dict = defaultdict(list)
    stairs_dict = defaultdict(list)
    rooms_dict = defaultdict(list)
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
                doors_dict[level].append(Door(feature, graph))
            elif is_stairs:
                stairs_dict[level].append(Stair(feature, graph))
            else:
                if len(feature.get("geometry", {}).get("coordinates", [])) <= 2:
                    continue
                rooms_dict[level].append(Room(feature, graph))

        else:
            broken.append(feature)


    all_stairs = [stair for stair_list in stairs_dict.values() for stair in stair_list]
    Stair.link_stairs(all_stairs)

    all_rooms = [room for room_list in rooms_dict.values() for room in room_list]
    Room.setup_all_rooms(all_rooms + all_stairs)
    link_rooms(rooms_dict, doors_dict, stairs_dict)


    setup_graphs_parallel(rooms_dict, stairs_dict)

    return [rooms_dict, doors_dict, stairs_dict]


def link_rooms(rooms: Dict[int, List[Room]], doors: Dict[int, List[Door]], stairs: Dict[int, List[Stair]]):
    """
       Links doors to rooms and stairs if they are on the outline of a structure.

       Args:
           rooms (Dict[int, List[Room]]): Dictionary of rooms indexed by level.
           doors (Dict[int, List[Door]]): Dictionary of doors indexed by level.
           stairs (Dict[int, List[Stair]]): Dictionary of stairs indexed by level.
    """

    for level in rooms.keys():
        for door in doors[level]:
            for room in chain(rooms[level], stairs[level]):

                if room.is_door_on_outline(door):
                    door.add_room(room)
                    room.doors.append(door)


def process_room(room: Room):
    room.setup_graph()  # Verändert das Objekt intern
    return room  # Gibt das veränderte Objekt zurück


def process_stair(stair: Stair):
    stair.setup_graph()
    return stair


#todo make simpler by using stais and rooms as list
def setup_graphs_parallel(rooms: Dict[int, List[Room]], stairs: Dict[int, List[Stair]], parallel: bool = False):
    """
    Initialisiert die Graphen für alle Räume und Treppen, mit Option für parallele oder sequentielle Verarbeitung.

    :param rooms: Ein Dictionary mit Leveln als Keys und Listen von Room-Objekten als Values.
    :param stairs: Ein Dictionary mit Leveln als Keys und Listen von Treppen-Objekten als Values.
    :param parallel: Boolean, der angibt, ob die Verarbeitung parallel (True) oder sequentiell (False) erfolgen soll.
    """
    print("Started processing graphs for individual rooms")

    if parallel:
        # Parallele Verarbeitung mit ProcessPoolExecutor
        with concurrent.futures.ProcessPoolExecutor() as executor:
            room_futures = {executor.submit(process_room, room): (level, i)
                            for level in rooms for i, room in enumerate(rooms[level])}

            stair_futures = {executor.submit(process_stair, stair): (level, i)
                             for level in stairs for i, stair in enumerate(stairs[level])}

            # Ergebnisse zurück in die Dictionaries schreiben
            for future in concurrent.futures.as_completed(room_futures):
                level, i = room_futures[future]
                rooms[level][i] = future.result()

            for future in concurrent.futures.as_completed(stair_futures):
                level, i = stair_futures[future]
                stairs[level][i] = future.result()
    else:
        # Sequentielle Verarbeitung für Debugging
        print("Running in sequential mode for debugging")
        for level in rooms:
            for i, room in enumerate(rooms[level]):
                rooms[level][i] = process_room(room)

        for level in stairs:
            for i, stair in enumerate(stairs[level]):
                stairs[level][i] = process_stair(stair)

    print("Graph processing finished")


def visualize_level(rooms, stairs, max_features):
    plt.figure(figsize=(12, 12))

    plotted_features = 0

    def random_color():
        return "#{:06x}".format(random.randint(0, 0xFFFFFF))

    for room in rooms:
        plotted_features += 1
        if plotted_features >= max_features:
            break
        room.plot(color=random_color())


    # Treppen kannst du ggf. ähnlich als Methode einer `Stair`-Klasse auslagern
    for stair in stairs:
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

    visualize_level(rooms[LEVEL_TO_DISPLAY], stairs[LEVEL_TO_DISPLAY], 1000)

    with open("resources/graph.json", "w") as f:
        f.write(rooms[LEVEL_TO_DISPLAY][0].graph.export_json())


