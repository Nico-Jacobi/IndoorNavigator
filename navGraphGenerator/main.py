import json
import random
import matplotlib.pyplot as plt
import matplotlib
import coordinateUtilities
from door import Door
from graph import Graph
from parseObj import parse_obj_files
from room import Room
from stairs import Stair
import numpy as np

matplotlib.use('TkAgg')



def parse_geojson_to_graph(geojson_string, origin_lat=coordinateUtilities.origin_lat, origin_lon=coordinateUtilities.origin_lon) -> tuple[Graph, list, list, list]:
    """
    Parses a GeoJSON file and extracts rooms, doors, and stairs.
    Sets them up, and returns a graph representation of the building,
    along with the lists of rooms, stairs, and doors.
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
    Room.setup_all_rooms(rooms + stairs, doors)
    graph.keep_largest_component()
    graph.normalize_coordinates(origin_lat, origin_lon)

    return graph, rooms, stairs, doors





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
    building_name = "h4"
    geojson_data = json.loads(geojson_string)

    graph, rooms, stairs, doors = parse_geojson_to_graph(geojson_data)

    with open(f"resources/{building_name}_graph.json", "w") as f:
        f.write(graph.export_json())

    parse_obj_files(rooms, stairs, doors, building_name)
