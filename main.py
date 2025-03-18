import json
from itertools import chain
from typing import List, Dict

import matplotlib.pyplot as plt
from collections import defaultdict
import matplotlib
import numpy as np
from door import Door
from room import Room
from stairs import Stair

matplotlib.use('TkAgg')  # Oder 'Agg', falls TkAgg nicht funktioniert


def parse_geojson(geojson_data):
    """
    Parses a GeoJSON file and extracts rooms, doors, and stairs, categorizing them by level.

    Args:
        geojson_data (dict): The parsed GeoJSON data.

    Returns:
        List[Dict[int, List[Room]], Dict[int, List[Door]], Dict[int, List[Stair]]]:
        A list containing dictionaries for rooms, doors, and stairs, indexed by level.
    """

    doors = defaultdict(list)
    stairs = defaultdict(list)
    rooms = defaultdict(list)
    broken = []

    for feature in geojson_data.get("features", []):
        properties = feature.get("properties")
        if not isinstance(properties, dict):
            broken.append(feature)
            continue

        level = properties.get("level")
        is_door = properties.get("door") == "yes"
        is_stairs = properties.get("stairs") == "yes"

        if level is not None:
            if is_door:
                doors[level].append(Door(feature))
            elif is_stairs:
                stairs[level].append(Stair(feature))
            else:
                if len(feature.get("geometry", {}).get("coordinates", [])) <= 2:
                    continue
                rooms[level].append(Room(feature))

        else:
            broken.append(feature)

    return [rooms, doors, stairs]


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

                if room.is_point_on_outline(door.coordinates):
                    door.rooms.append(room)
                    room.doors.append(door)







def visualize_level(parsed_data, level):#
    """
        Visualizes a specific level by plotting rooms, doors, and stairs.

        Args:
            parsed_data (List[Dict[int, List[Room]], Dict[int, List[Door]], Dict[int, List[Stair]]]):
                Parsed data containing rooms, doors, and stairs.
            level (int): The level to visualize.
    """

    rooms, doors, stairs = parsed_data
    link_rooms(rooms, doors, stairs)
    plt.figure(figsize=(10, 10))

    def plot_linestring(features, color) -> None:
        for room in features:
            x, y = zip(*room.coordinates) if isinstance(room.coordinates[0], tuple) else ([], [])
            plt.plot(x, y, color=color, alpha=0.5)

    def plot_points(features, color) -> None:
        for value in features:
            x, y = value.coordinates
            plt.scatter(x, y, color=color, alpha=0.8)  # Use scatter for points

    plot_linestring(rooms[level], "blue")
    plot_points(doors[level], "red")
    plot_linestring(stairs[level], "green")

    plt.gca().set_aspect(1 / np.cos(np.deg2rad(50.8)))
    plt.xlabel("X Coordinate")
    plt.ylabel("Y Coordinate")
    plt.title(f"Visualization of Level {level}")
    plt.grid(True)
    plt.show()



if __name__ == "__main__":
    LEVEL_TO_DISPLAY = "3"  # Change this to the level you want to visualize

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    parsed = parse_geojson(geojson_data)



    visualize_level(parsed, LEVEL_TO_DISPLAY)
