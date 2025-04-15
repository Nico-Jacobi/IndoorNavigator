import json
import matplotlib
from door import Door
from graph import Graph
from room import Room
from stairs import Stair
from typing import List, Tuple
from polygon import Polygon2D

from wavefront import Wavefront

matplotlib.use('TkAgg')


def parse_obj_files(geojson_string) -> None:
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
                if len(feature.get("geometry", {}).get("coordinates", [])) <= 3:
                    continue
                rooms.append(Room(feature, graph))

        else:
            broken.append(feature)


    all_rooms = [room for room in rooms + stairs if room.level == 3]
    print("all rooms:", len(all_rooms))
    all_rooms = [room for room in all_rooms if len(room.coordinates) > 3]
    print("all rooms with valid geometry:", len(all_rooms))


    Stair.link_stairs(stairs)
    Room.setup_all_rooms(all_rooms, doors) # this needs to be done here, so the geometry is right, maybe just fix that
    # thats faster

    origin_lat, origin_lon = rooms[0].coordinates[0]

    walls:str = parse_walls_obj_from_rooms(all_rooms, origin_lat, origin_lon)
    ground:str = parse_ground_floor_obj_from_rooms(all_rooms, origin_lat, origin_lon)


    with open("resources/walls.obj", "w") as f:
        f.write(walls)

    with open("resources/ground.obj", "w") as f:
        f.write(ground)

    return





def parse_walls_obj_from_rooms(all_rooms: List['Room'], origin_lat: float, origin_lon: float) -> str:
    wavefront: Wavefront = Wavefront()

    for room in all_rooms:
        room.get_wavefront_walls(origin_lat, origin_lon, wavefront)

    return str(wavefront)



def parse_ground_floor_obj_from_rooms(rooms: List['Room'], origin_lat: float, origin_lon: float) -> str:

    wavefront = Wavefront()
    for room in rooms:

        geometry = room.get_meter_geometry(origin_lat, origin_lon)

        if not geometry.is_empty:
            polygon = Polygon2D.from_shapely(geometry)
            polygon.to_wavefront(wavefront)

    return str(wavefront)






if __name__ == "__main__":

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    parse_obj_files(geojson_data)





