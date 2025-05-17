import json
import matplotlib
from matplotlib import pyplot as plt
from shapely.geometry.polygon import Polygon
from shapely.ops import unary_union
import coordinateUtilities
from door import Door
from room import Room
from stairs import Stair
from typing import List, Tuple
from polygon import Polygon2D
from wavefront import Wavefront

matplotlib.use('TkAgg')

import os

def parse_obj_files(rooms: List[Door], stairs: List[Stair], doors: List[Door], source_filename, origin_lat, origin_lon) -> None:
    """
    generates .obj files per floor, and a config .json with output paths.
    Expects the rooms to be already parsed and setup. (the graph doesn't need to be setup)
    """
    building_name = os.path.splitext(os.path.basename(source_filename))[0]

    levels = sorted(set(room.level for room in rooms + stairs))
    output_config = {
        "building": building_name,
        "graph": f"{source_filename}_graph.json",
        "floors": []
    }

    for level in levels:
        print(f"Processing level {level}")
        floor_rooms = [r for r in rooms + stairs if r.level == level and len(r.coordinates) > 3]
        if not floor_rooms:
            continue

        floor_doors = [d for d in doors if d.level == level]
        walls = parse_walls_obj_from_rooms(floor_rooms, origin_lat, origin_lon)
        ground = parse_ground_floor_obj_from_rooms(floor_rooms, origin_lat, origin_lon)
        doors_obj = parse_door_obj(floor_doors, origin_lat, origin_lon)

        walls_file = f"resources/{building_name}_floor{level}_walls.obj"
        ground_file = f"resources/{building_name}_floor{level}_ground.obj"
        doors_file = f"resources/{building_name}_floor{level}_doors.obj"


        with open(walls_file, "w") as f:
            f.write(str(walls))
        with open(ground_file, "w") as f:
            f.write(str(ground))
        with open(doors_file, "w") as f:
            f.write(str(doors_obj))

        output_config["floors"].append({
            "level": level,
            "walls": os.path.basename(walls_file),
            "ground": os.path.basename(ground_file),
            "doors": os.path.basename(doors_file)
        })


    config_file = f"resources/{building_name}_config.json"
    with open(config_file, "w") as f:
        json.dump(output_config, f, indent=4)

    write_default_mtl(f"resources/buildings_material.mtl")

    print("Done writing config:", config_file)


def write_default_mtl(filename: str):
    """
    Writes a basic MTL file with 'WhiteMaterial' and 'DarkMaterial'.
    """
    with open(filename, "w") as f:
        f.write("""# Default materials

            newmtl WhiteMaterial
            Ka 1.000 1.000 1.000
            Kd 1.000 1.000 1.000
            Ks 0.000 0.000 0.000
            d 1.0
            illum 1
            
            newmtl DarkMaterial
            Ka 0.1 0.1 0.1
            Kd 0.1 0.1 0.1
            Ks 0.000 0.000 0.000
            d 1.0
            illum 1
            """)


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



def parse_door_obj(all_doors: List['Door'], origin_lat: float,origin_lon: float) -> Wavefront:

    wavefront = Wavefront()
    for door in all_doors:
        door.get_wavefront_walls(origin_lat, origin_lon, wavefront)

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


