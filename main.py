import json
import matplotlib.pyplot as plt
from collections import defaultdict
import matplotlib
import numpy as np
from door import Door
from room import Room
from stairs import Stair

matplotlib.use('TkAgg')  # Oder 'Agg', falls TkAgg nicht funktioniert


def parse_geojson(geojson_data):
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


def visualize_level(parsed_data, level):
    rooms, doors, stairs = parsed_data
    plt.figure(figsize=(10, 10))

    def plot_linestring(features, color) -> None:
        for room in features:
            x, y = zip(*room.coordinates) if isinstance(room.coordinates[0], tuple) else ([], [])
            plt.plot(x, y, color=color, alpha=0.5)

    def plot_points(features, color) -> None:
        for value in features:
            x, y = value.coordinates
            plt.plot(x, y, color=color, alpha=0.5)


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
