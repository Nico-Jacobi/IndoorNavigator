import json
import matplotlib
from matplotlib import pyplot as plt
from shapely.geometry.polygon import Polygon
from shapely.ops import unary_union

import coordinateUtilities
from door import Door
from graph import Graph
from room import Room
from stairs import Stair
from typing import List, Tuple
from polygon import Polygon2D
from wavefront import Wavefront

matplotlib.use('TkAgg')


def parse_obj_files(geojson_string, origin_lat=coordinateUtilities.origin_lat, origin_lon=coordinateUtilities.origin_lon) -> None:
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

    #origin_lat, origin_lon = rooms[0].coordinates[0]

    walls:Wavefront = parse_walls_obj_from_rooms(all_rooms, origin_lat, origin_lon)
    ground:Wavefront = parse_ground_floor_obj_from_rooms(all_rooms, origin_lat, origin_lon)



    with open("resources/walls.obj", "w") as f:
        f.write(str(walls))

    with open("resources/ground.obj", "w") as f:
        f.write(str(ground))

    # Export the .mtl file (if it's not already created)
    with open("resources/WhiteMaterial.mtl", "w") as f:
        f.write("""
        newmtl WhiteMaterial
        Ka 1.000 1.000 1.000  # Ambient color (white)
        Kd 1.000 1.000 1.000  # Diffuse color (white)
        Ks 0.500 0.500 0.500  # Specular color (gray)
        d 1.0                 # Transparency
        """)

    return


def parse_walls_obj_from_rooms(all_rooms: List['Room'], origin_lat: float, origin_lon: float) -> Wavefront:
    wavefront: Wavefront = Wavefront()

    for room in all_rooms:
        room.get_wavefront_walls(origin_lat, origin_lon, wavefront)

    merged_geometry_outside = unary_union([room.polygon for room in all_rooms if room.polygon.geom_type == "Polygon"])
    merged_geometry_outside = merged_geometry_outside.buffer(Room.wall_thickness[0], join_style="mitre").buffer(-Room.wall_thickness[0], join_style="mitre")

    #plot_geometry_collection(merged_geometry_outside)
    print(merged_geometry_outside.geom_type)

    outside_holes = []
    outside = []

    # Handle both MultiPolygon and Polygon cases
    if merged_geometry_outside.geom_type == "MultiPolygon":
        # Create a Room for each polygon in the collection
        for room_polygon in merged_geometry_outside.geoms:
            # Handle exterior boundary
            outside.append(Room.from_data(-1, "outside",Room.simplify_geometry(list(room_polygon.buffer(Room.wall_thickness[0]/2,join_style="mitre").exterior.coords),loop=True),all_rooms[0].graph))  # graph not actually used here

            # Handle holes (interior rings) for each polygon
            for interior in room_polygon.interiors:
                coords = Room.simplify_geometry(list(Polygon(interior).buffer(-Room.wall_thickness[0] / 2, join_style="mitre").exterior.coords))

                if len(coords) > 3:
                    outside_holes.append(Room.from_data(-1, "inside_hole", coords, all_rooms[0].graph))

    elif merged_geometry_outside.geom_type == "Polygon":
        # Handle single polygon case
        outside.append(Room.from_data(-1, "outside",Room.simplify_geometry(list(merged_geometry_outside.buffer(Room.wall_thickness[0]/2,join_style="mitre").exterior.coords),loop=True), all_rooms[0].graph))

        # Handle holes for the single polygon
        for interior in merged_geometry_outside.interiors:
            coords = Room.simplify_geometry(list(Polygon(interior).buffer(-Room.wall_thickness[0] / 2, join_style="mitre").exterior.coords))

            if len(coords) > 3:
                outside_holes.append(Room.from_data(-1, "inside_hole", coords, all_rooms[0].graph))

    # Process all collected rooms
    for room in outside_holes:
        room.get_wavefront_walls(origin_lat, origin_lon, wavefront, outside=False, inside=True, top=True)

    for outside_room in outside:
        outside_room.get_wavefront_walls(origin_lat, origin_lon, wavefront, outside=True, inside=False, top=True)

    return wavefront



def parse_ground_floor_obj_from_rooms(rooms: List['Room'], origin_lat: float, origin_lon: float) -> Wavefront:

    wavefront = Wavefront()
    for room in rooms:

        geometry = room.get_meter_geometry(origin_lat, origin_lon)

        if not geometry.is_empty:
            polygon = Polygon2D.from_shapely(geometry)
            polygon.to_wavefront_triangulation(wavefront)

    return wavefront


def plot_geometry_collection(geom_collection, ax=None):
    if ax is None:
        fig, ax = plt.subplots(figsize=(12, 6))

    # Define colors for different geometry types
    colors = {
        'Point': 'red',
        'LineString': 'blue',
        'Polygon': 'green'
    }

    # Plot each geometry in the collection
    for geom in geom_collection.geoms:
        geom_type = geom.geom_type

        if geom_type == 'Point':
            ax.plot(geom.x, geom.y, 'o', color=colors['Point'], markersize=8)

        elif geom_type == 'LineString':
            x, y = geom.xy
            ax.plot(x, y, color=colors['LineString'], linewidth=2)

        elif geom_type == 'Polygon':
            x, y = geom.exterior.xy
            ax.fill(x, y, alpha=0.5, color=colors['Polygon'])

            # Plot any holes in the polygon
            for interior in geom.interiors:
                x, y = interior.xy
                ax.fill(x, y, alpha=1, color='red', edgecolor='black', linewidth=1)

    # Add a legend
    from matplotlib.lines import Line2D
    legend_elements = [
        Line2D([0], [0], marker='o', color='w', markerfacecolor=colors['Point'], markersize=8, label='Point'),
        Line2D([0], [0], color=colors['LineString'], linewidth=2, label='LineString'),
        Line2D([0], [0], marker='s', color='w', markerfacecolor=colors['Polygon'], alpha=0.5, markersize=10,
               label='Polygon')
    ]
    ax.legend(handles=legend_elements)

    ax.set_title('Shapely GeometryCollection Visualization')
    ax.set_xlabel('X Coordinate')
    ax.set_ylabel('Y Coordinate')
    ax.axis('equal')
    ax.grid(True)
    plt.tight_layout()
    plt.show()

    return ax






if __name__ == "__main__":

    geojson_string = open("resources/h4.geojson", encoding="utf-8").read()
    geojson_data = json.loads(geojson_string)
    parse_obj_files(geojson_data)





