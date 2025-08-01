import json
import os
import random
import matplotlib.pyplot as plt
import matplotlib
import coordinateUtilities
import room
from door import Door
from graph import Graph
from parseObj import parse_obj_files
from room import Room
from stairs import Stair
import numpy as np

matplotlib.use('TkAgg')

# used to normalize the coordinates for unity
#origin_lat=50.80977
#origin_lon=8.81048
origin_lat = -1
origin_lon = -1


def parse_geojson_to_graph(geojson_string) -> tuple[Graph, list, list, list]:
    """
    Parses a GeoJSON file and extracts rooms, doors, and stairs.
    Sets them up, and returns a graph representation of the building,
    along with the lists of rooms, stairs, and doors.
    """
    global origin_lat
    global origin_lon

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

                if origin_lat == -1 and origin_lon == -1:
                    origin_lat = doors[-1].coordinates[0]
                    origin_lon = doors[-1].coordinates[1]

            elif is_stairs:
                try:
                    stairs.append(Stair(feature, graph))
                except ValueError as e:
                    print(f"Skipping invalid stair: {e}")
                    broken.append(feature)

            else:
                if len(feature.get("geometry", {}).get("coordinates", [])) <= 2:
                    continue
                try:

                    room = Room(feature, graph)
                    rooms.append(room)
                    #room.plot()
                    #plt.show()

                except ValueError as e:
                    print(f"Skipping invalid room: {e}")
                    broken.append(feature)
        else:
            broken.append(feature)


    coordinateUtilities.origin_lat = rooms[0].coordinates[0][0]
    coordinateUtilities.origin_lon = rooms[0].coordinates[0][1]


    Stair.link_stairs(stairs)
    Room.setup_all_rooms(rooms + stairs, doors)
    graph.keep_largest_component()
    graph.normalize_coordinates()

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


# will read all geojson from resources and make 3d models + graph + config file from them
# for each geojson a folder will be created
if __name__ == "__main__":

    geojson_folder = "resources"

    geojson_files = [f for f in os.listdir(geojson_folder) if f.endswith('.geojson')]

    for geojson_file in geojson_files:

        # Extract building name from filename
        building_name = os.path.splitext(geojson_file)[0]

        print(f"Processing building: {building_name}")

        # Read and parse the GeoJSON file
        geojson_path = os.path.join(geojson_folder, geojson_file)
        with open(geojson_path, encoding="utf-8") as f:
            geojson_string = f.read()
        geojson_data = json.loads(geojson_string)

        # Parse the GeoJSON to graph
        graph, rooms, stairs, doors = parse_geojson_to_graph(geojson_data)

        # Save the graph JSON in the resources folder
        graph_path = os.path.join(geojson_folder, f"{building_name}_graph.json")
        with open(graph_path, "w") as f:
            f.write(graph.export_json())

        # Create building-specific folder for OBJ files
        building_obj_folder = os.path.join(geojson_folder, building_name)
        os.makedirs(building_obj_folder, exist_ok=True)

        # Parse OBJ files and save them in the building-specific folder
        parse_obj_files(rooms, stairs, doors, building_name, building_obj_folder)

        # Visualize the level
        #visualize_level(rooms, stairs)

        print(f"Completed processing for {building_name}")
        print("origin lat: ", origin_lat)
        print("origin lon: ", origin_lon)
        print("-" * 50)  # Separator between buildings

        print("saved point in geometry: ", room.saved_points_geometry)
        print("saved point in path: ", room.saved_points_path)