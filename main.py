import json
import matplotlib.pyplot as plt
from collections import defaultdict
import matplotlib
import numpy as np

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
                doors[level].append(feature)
            elif is_stairs:
                stairs[level].append(feature)
            else:
                rooms[level].append(feature)
        else:
            broken.append(feature)

    return [doors, stairs, rooms]


def visualize_level(parsed_data, level):
    doors, stairs, rooms = parsed_data
    plt.figure(figsize=(10, 10))

    def plot_linestring(features, color):
        for feature in features:
            geometry = feature.get("geometry", {})
            if geometry.get("type") == "LineString":
                coordinates = geometry.get("coordinates", [])
                x, y = zip(*coordinates)
                plt.plot(x, y, color=color, alpha=0.5)

    def plot_points(features, color):
        for feature in features:
            geometry = feature.get("geometry", {})
            if geometry.get("type") == "Point":
                coordinates = geometry.get("coordinates", [])
                if len(coordinates) == 2:  # Sicherstellen, dass es genau ein (x, y)-Paar ist
                    x, y = coordinates
                    plt.scatter(x, y, color=color, alpha=0.5)


    plot_linestring(rooms[level], "blue")
    plot_points(doors[level], "red")
    plot_linestring(stairs[level], "green")

    plt.gca().set_aspect(1 / np.cos(np.deg2rad(50.8)))
    plt.xlabel("X Coordinate")
    plt.ylabel("Y Coordinate")
    plt.title(f"Visualization of Level {level}")
    plt.grid(True)
    plt.show()


def count_door_occurrences(parsed_data):
    doors, _, rooms = parsed_data

    # Räume in ein Set von Koordinaten umwandeln für schnelles Nachschlagen
    room_coordinates = set()
    for features in rooms.values():
        for feature in features:
            geometry = feature.get("geometry", {})
            if geometry.get("type") == "LineString":
                room_coordinates.update(map(tuple, geometry.get("coordinates", [])))

    # Jede Tür überprüfen
    for level, door_features in doors.items():
        for door in door_features:
            geometry = door.get("geometry", {})
            if geometry.get("type") == "Point":
                coordinates = tuple(geometry.get("coordinates", []))
                occurrences = sum(1 for coord in room_coordinates if coord == coordinates)
                print(f"Door at {coordinates} occurs {occurrences} times in rooms.")


if __name__ == "__main__":
    LEVEL_TO_DISPLAY = "3"  # Change this to the level you want to visualize

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    parsed = parse_geojson(geojson_data)

    count_door_occurrences(parsed)


    visualize_level(parsed, LEVEL_TO_DISPLAY)
