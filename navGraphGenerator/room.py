import math
import random
from scipy.spatial import distance_matrix
from typing import Tuple, List, Any, Dict, Optional
import numpy as np
from matplotlib import pyplot as plt
from shapely.geometry.point import Point
from coordinateUtilities import meters_to_latlon, normalize_lat_lon_to_meter
from dataClasses import PathVertex, BoundingBox, NavigationPath
from door import Door
import heapq
from shapely.geometry import Polygon
from graph import Graph




class Room:
    # the grid size for pathfinding the visual paths (lower = more accurate, but slower)
    grid_size_x: float = 0.00001
    grid_size_y: float = 0.00001

    # used to remove extremely thin parts of the geometry and for linking doors with walls (in gps)
    # has 2 values, as gps coordinates are not uniform (1 in lat != 1 in lon)
    wall_thickness: (float, float) = meters_to_latlon(0.3, 0.3, 50.8, 8.8)

    def __init__(self, json: Dict[str, Any], graph: Graph):
        """
        :param json: JSON object containing the room data.
        :param graph: reference to the graph object to add this room to
        """

        properties = json.get("properties", {})
        self.level: int = int(properties.get("level"))
        self.name: str = properties.get("name", "")

        geometry = json.get("geometry", {})
        #self.geometry_type: str = geometry.get("type", "")
        # Flip x and y when loading coordinates (for having it in lat lon order)
        self.coordinates: List[Tuple[float, float]] = [
            (float(coord[1]), float(coord[0])) for coord in geometry.get("coordinates", [])
        ]

        self.coordinates = Room.simplify_geometry(self.coordinates, loop=True)

        # represents holes in the geometry (used later)
        self.holes: List[List[Tuple[float, float]]]= []
        self.polygon = Polygon(self.coordinates, self.holes)

        # references to the doors in this room
        self.doors: List[Door] = []
        self.graph: Graph = graph

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        self.bounding_box: BoundingBox = self._compute_bounding_box()

        # grid for finding the visual paths
        self.grid: List[List[Optional[PathVertex]]] = []


    @classmethod
    def from_data(cls, level: int, name: str, coordinates: List[Tuple[float, float]], graph: Graph, holes: Optional[List[List[Tuple[float, float]]]] = None):
        """
        Alternative constructor to create a Room without JSON input. (usefull for speaciall rooms, not in the geojson)
        """
        obj = cls.__new__(cls)
        obj.level = level
        obj.name = name
        obj.coordinates = Room.simplify_geometry(coordinates, loop=True)
        obj.holes = holes if holes else []
        obj.polygon = Polygon(obj.coordinates, obj.holes)
        obj.doors = []
        obj.graph = graph
        obj.bounding_box = obj._compute_bounding_box()
        obj.grid = []
        return obj

    def __repr__(self):
        return f"Room (name={self.name}, level={self.level})"


    def _compute_bounding_box(self) -> BoundingBox:
        """
        calculates the bounding box of the room based on its coordinates.
        """
        if not self.coordinates:
            return BoundingBox(0, 0, 0, 0)

        min_x = min(coord[0] for coord in self.coordinates)
        max_x = max(coord[0] for coord in self.coordinates)
        min_y = min(coord[1] for coord in self.coordinates)
        max_y = max(coord[1] for coord in self.coordinates)

        return BoundingBox(min_x, min_y, max_x, max_y)


    def get_meter_geometry(self, origin_lat: float, origin_lon: float) -> Polygon:
        """
        Returns the geometry of the room as a shapely Polygon object.
        Subtracts holes (if they were set up)
        """
        coordinates: List[Tuple] = [normalize_lat_lon_to_meter(lat, lon, origin_lat, origin_lon) for lat, lon in self.coordinates]
        holes = []
        for hole in self.holes:
            holes.append([normalize_lat_lon_to_meter(lat, lon, origin_lat, origin_lon) for lat, lon in hole])

        return Polygon(coordinates, holes=holes)


    def get_wavefront_walls(self, origin_lat: float, origin_lon: float, add_to_wavefront: "Wavefront", height: float = 2.0, outside=False, inside=True, top=True) -> None:
        """
        Creates an OBJ file representation of walls from room polygons.

        """

        def align_polygon_coords(inner_coords, outer_coords):
            """alisn 2 lists, so the indices are the closest poins, will delete points, if lenth doesent match"""

            inner_coords_np = np.array(inner_coords)
            outer_coords_np = np.array(outer_coords)

            # If different lengths, downsample the longer one by matching closest vertices
            if len(inner_coords_np) != len(outer_coords_np):
                if len(inner_coords_np) < len(outer_coords_np):
                    shorter = inner_coords_np
                    longer = outer_coords_np
                else:
                    shorter = outer_coords_np
                    longer = inner_coords_np

                # Compute distance matrix and pick closest matches
                dists = distance_matrix(shorter, longer)
                used_indices = set()
                matches = []

                for i in range(len(shorter)):
                    idx = np.argmin(dists[i])
                    while idx in used_indices:  # avoid duplicates
                        dists[i][idx] = np.inf
                        idx = np.argmin(dists[i])
                    used_indices.add(idx)
                    matches.append(idx)

                matched_longer = longer[list(matches)]

                if len(inner_coords_np) < len(outer_coords_np):
                    outer_coords_np = matched_longer
                else:
                    inner_coords_np = matched_longer

            # Find best rotation (minimizing total distance)
            min_total_dist = float('inf')
            best_rotation = 0
            best_reversed = False

            for reversed_flag in [False, True]:
                outer_to_test = outer_coords_np[::-1] if reversed_flag else outer_coords_np
                for shift in range(len(outer_to_test)):
                    rotated = np.roll(outer_to_test, -shift, axis=0)
                    total_dist = np.linalg.norm(inner_coords_np - rotated, axis=1).sum()
                    if total_dist < min_total_dist:
                        min_total_dist = total_dist
                        best_rotation = shift
                        best_reversed = reversed_flag

            aligned_outer = outer_coords_np[::-1] if best_reversed else outer_coords_np
            aligned_outer = np.roll(aligned_outer, -best_rotation, axis=0)

            return inner_coords_np, aligned_outer

        def is_clockwise(ring: List[Tuple[float, float]]) -> bool:
            area = 0.0
            for i in range(len(ring)):
                x1, y1 = ring[i]
                x2, y2 = ring[(i + 1) % len(ring)]
                area += (x2 - x1) * (y2 + y1)
            return area > 0  # Positive → clockwise in this convention

            # Ensure outer ring is clockwise

        def _generate_wall_faces(inner, outer, height, inside, outside):
            for i in range(len(inner)):
                next_i = (i + 1) % len(inner)
                dx, dz = inner[next_i][0] - inner[i][0], inner[next_i][1] - inner[i][1]
                length = math.hypot(dx, dz)
                normal_in = (-dz / length, 0, dx / length) if length else (0, 0, 0)

                if top:
                    # top
                    normal_top = (0, 1, 0)
                    add_to_wavefront.add_face([
                        (inner[next_i][0], height, inner[next_i][1]),
                        (outer[next_i][0], height, outer[next_i][1]),
                        (outer[i][0], height, outer[i][1]),
                        (inner[i][0], height, inner[i][1])
                    ], [normal_top] * 4)

                if inside:
                    #inner wall
                    add_to_wavefront.add_face([
                        (inner[i][0], 0.0, inner[i][1]),
                        (inner[next_i][0], 0.0, inner[next_i][1]),
                        (inner[next_i][0], height, inner[next_i][1]),
                        (inner[i][0], height, inner[i][1])
                    ], [normal_in] * 4)

                if outside:
                    # outer wall
                    normal_out = tuple(-n for n in normal_in)
                    add_to_wavefront.add_face([
                        (outer[i][0], height, outer[i][1]),
                        (outer[next_i][0], height, outer[next_i][1]),
                        (outer[next_i][0], 0.0, outer[next_i][1]),
                        (outer[i][0], 0.0, outer[i][1])
                    ], [normal_out] * 4)

        polygon: Polygon = self.get_meter_geometry(origin_lat, origin_lon)
        wall_thickness = 0.2

        inner_geom = polygon.buffer(-wall_thickness / 2, join_style="mitre")
        outer_geom = polygon.buffer(wall_thickness / 2, join_style="mitre")

        if inner_geom.geom_type == "MultiPolygon":
            inner_geom = max(inner_geom.geoms, key=lambda p: p.area)
            outer_geom = inner_geom.buffer(wall_thickness, join_style="mitre")

        inner_polygon = list(inner_geom.exterior.coords[:-1])
        outer_polygon = list(outer_geom.exterior.coords[:-1])

        inner_aligned, outer_aligned = align_polygon_coords(inner_polygon, outer_polygon)

        if is_clockwise(inner_aligned):
            inner_aligned = list(reversed(inner_aligned))
            outer_aligned = list(reversed(outer_aligned))

        _generate_wall_faces(inner_aligned, outer_aligned, height, inside, outside)

        # For handling holes in the room polygon
        for hole_ring in polygon.interiors:

            hole_polygon = Polygon(hole_ring)
            inner_geom = hole_polygon.buffer(wall_thickness*0.6, join_style="mitre")

            # Handle potential geometry issues
            if inner_geom.geom_type == "MultiPolygon":
                inner_geom = max(inner_geom.geoms, key=lambda p: p.area)

            # Extract coordinates (dropping the closing point)
            inner =  Room.simplify_geometry(list(inner_geom.exterior.coords[:-1]))

            for i in range(len(inner)):
                next_i = (i + 1) % len(inner)
                dx, dz = inner[next_i][0] - inner[i][0], inner[next_i][1] - inner[i][1]
                length = math.hypot(dx, dz)
                normal_in = (-dz / length, 0, dx / length) if length else (0, 0, 0)

                # inner wall
                add_to_wavefront.add_face([
                    (inner[i][0], 0.0, inner[i][1]),
                    (inner[next_i][0], 0.0, inner[next_i][1]),
                    (inner[next_i][0], height, inner[next_i][1]),
                    (inner[i][0], height, inner[i][1])
                ], [normal_in] * 4)





    @staticmethod
    def setup_all_rooms(rooms: List["Room"], doors: List[Door]) -> None:
        """
        - fixes overlapping geometry of the rooms
        - generates the grid in each room
        - links doors to rooms and vice versa
        - sets up the visual paths

        :param rooms: List of Room objects, including the stairs.
        :param doors: List of all Door object.

        The inputs should contain all rooms and doors that can be linked horizontally.
        (don't call twice with rooms on the same floor, otherwise the graph whill not be fully connected)
        """

        # fixing the geometry of the rooms, sometimes overlapping in the geojson
        for i in range(len(rooms)):
            print("checking geometry of",  rooms[i].name)
            for j in range(i + 1, len(rooms)):
                rooms[i]._fix_intersections(rooms[j])


        # generating the grid in each room, used for computing the visual paths later
        # (precompute and cache distance_to_wall() and is_walkable() for later pathfinding)
        for room in rooms:
            print("setting up grid for room", room.name)
            room._generate_grid()

        # linking doors to rooms and vice versa
        for room in rooms:
            print("linking doors for room", room.name)
            for door in doors:
                if room.is_door_on_outline(door):
                    door.add_room(room)
                    room.doors.append(door)

        # setting up the visual paths later shown in the app
        for room in rooms:
            room.coordinates = Room.simplify_geometry(room.coordinates, loop=True)
            room._setup_paths()



    def _fix_intersections(self, other: "Room") -> bool:
        """
        Will remove overlapping areas from the bigger room.
        Will also handle holes in the room.

        :param other: The overlapping room (will do nothing id they don't overlap)
        :return: True if the geometry was changed, False otherwise
        """


        if self.level != other.level:
            return False

        if len(self.coordinates) < 3 or len(other.coordinates) < 3:
            return False

        # Check if the bounding boxes intersect
        # (as this is much faster than checking the polygon, assuming most rooms are rectangular / don't overlap)
        bounding_box_intersect = not (
            self.bounding_box.max_x < other.bounding_box.min_x or
            self.bounding_box.min_x > other.bounding_box.max_x or
            self.bounding_box.max_y < other.bounding_box.min_y or
            self.bounding_box.min_y > other.bounding_box.max_y
        )
        if not bounding_box_intersect:
            return False

        # if the bounding boxes intersect, check if the polygons actually do (done for performance)
        room_polygon = self.polygon
        other_polygon = other.polygon
        if not room_polygon.intersects(other_polygon):
            return False

        if other_polygon.area > room_polygon.area:
            # if this room is contained in the other one, remove parts from the other
            return other._fix_intersections(self)

        # Perform the subtraction (shapely library)
        result_polygon = (room_polygon - other_polygon)
        for hole in self.holes:
            result_polygon = result_polygon - Polygon(hole)

        # buffer in and then out, to keep the geometry but remove extremely thin parts, which happen due to the '-'
        # the "mitre" is to not causes rounded geometry, wich is not directly a problem, but leads to longer calculations
        result_polygon = result_polygon.buffer(-Room.wall_thickness[0]).buffer(Room.wall_thickness[0], join_style="mitre")

        # Convert the result back to a list of coordinates, and save
        if result_polygon.geom_type == 'Polygon':
            self.coordinates = list(result_polygon.exterior.coords)[:-1]  # Remove the last point as it's the same as the first
            self.holes = [list(interior.coords)[:-1] for interior in result_polygon.interiors]  # Hole coordinates
            self.polygon = result_polygon
            return True


        # if the rooms was split into multiple polygons (shouldn't happen, but just in case)
        elif result_polygon.geom_type == 'MultiPolygon':
            # "result_polygon.geoms" this is not a problem, as the room is not a simple polygon in this case
            largest_polygon = max(result_polygon.geoms, key=lambda p: p.area)
            self.coordinates = list(largest_polygon.exterior.coords)[:-1]
            self.holes = [list(interior.coords)[:-1] for interior in largest_polygon.interiors]  # Hole coordinates
            self.polygon = largest_polygon
            return True

        else:
            # If the result is not a polygon (shouldn't happen), return the original
            return False



    def _generate_grid(self) -> None:
        """
        Generates a grid of PathVertex objects within the room's bounding box.
        This will be used to find the visual paths between doors.

        (could be done without precalculating this, but saved a lot of time, as otherwise
        distance_to_wall() and is_walkable() would be called very often on the same point)
        """

        self.grid: List[List[Optional[PathVertex]]] = []

        has_vertex = False
        y = self.bounding_box.min_y
        y_index = 0

        # going over x and y, to create a grid of PathVertex objects, going at the grid size (class variable)
        while y <= self.bounding_box.max_y:
            row = []
            x = self.bounding_box.min_x
            x_index = 0

            while x <= self.bounding_box.max_x:

                # if the point is walkable, create a PathVertex object
                if self.is_walkable((x, y)):
                    vertex = PathVertex(x=x, y=y, x_index=x_index, y_index=y_index, distance_to_wall=self.distance_to_wall((x, y)))
                    row.append(vertex)
                    has_vertex = True

                # if the point is not walkable, append None, to keep the indices correct
                else:
                    row.append(None)

                x += Room.grid_size_x
                x_index += 1

            self.grid.append(row)
            y += Room.grid_size_y
            y_index += 1


        # if no vertices were added, create a single vertex in the center of the room (for very small rooms / grid size to big)
        if not has_vertex:
            center: Tuple[float, float] = self.bounding_box.get_center()
            center_vertex = PathVertex(x=center[0], y=center[1], x_index=0, y_index=0, distance_to_wall=self.distance_to_wall((center[0], center[1])))
            self.grid = [[center_vertex]]

    def is_walkable(self, point_gps_pos: Tuple[float, float]) -> bool:
        """
        Determines whether a position is walkable by checking if it is within the room's polygon
        but outside any holes.
        uses shapely (which casts a ray from the point, if intersections are odd, it is inside)

        :param point_gps_pos: Tuple of (x, y) coordinates of the point.
        :return: True if the position is within the room polygon and not in a hole, otherwise False.
        """
        if not self.bounding_box.is_inside(point_gps_pos):
            return False

        # Create a Point object for the given coordinates
        point = Point(point_gps_pos)

        if len(self.coordinates) < 3:
            return False

        # Check if the point is inside the main room polygon
        room_polygon = Polygon(self.coordinates)
        if not room_polygon.contains(point):
            return False

        # Check if the point is inside any of the holes (interior polygons)
        for hole in self.holes:
            hole_polygon = Polygon(hole)
            if hole_polygon.contains(point):
                return False  # The point is inside a hole, so it's not walkable

        # If the point is inside the room and not in any holes, it's walkable
        return True

    def distance_to_wall(self, point: Tuple[float, float]) -> float:
        """
        Calculates the minimum distance from a point to any wall (edge) of the room, considering holes.

        :param point: The point (x, y) to check distance from
        :return: The minimum distance to any wall
        """
        if not self.coordinates or len(self.coordinates) < 2:
            return 0.0  # No walls to measure distance from

        min_distance = float('inf')


        # Check distance to each wall segment of the room
        for i in range(len(self.coordinates)):
            j = (i + 1) % len(self.coordinates)  # Next point, wrapping around to first point

            # Calculate distance to this wall segment
            distance = distance_to_segment(point, self.coordinates[i], self.coordinates[j])

            # Update minimum distance if this is smaller
            min_distance = min(min_distance, distance)

        # the segment looping the thing around
        min_distance = min(min_distance, distance_to_segment(point, self.coordinates[0], self.coordinates[-1]))

        # Now check the holes (interior polygons)
        for hole in self.holes:
            # The hole is a polygon, so we check the distance to its walls (edges)
            for i in range(len(hole)):
                j = (i + 1) % len(hole)  # Next point, wrapping around to first point

                # Calculate distance to this wall segment of the hole
                distance = distance_to_segment(point, hole[i], hole[j])

                # Update minimum distance if this is smaller
                min_distance = min(min_distance, distance)

        return min_distance

    def is_door_on_outline(self,door: Door, tolerance: float = wall_thickness[0]) -> bool:
        """
        Checks if a door is on the outline of the room and returns a segment centered at the door,
        aligned with the closest wall segment and of fixed length.

        :param door: The door to check.
        :param tolerance: Distance tolerance for matching a wall segment.
        :return: Tuple (is_on_outline, edge_segment_centered_at_door) — edge_segment is None if not on outline.
        """

        def make_centered_rectangle(center: tuple[float, float], direction: tuple[float, float],
                                    door_length_scale: float = 4, door_depth_scale: float = 1.5) -> list[tuple[float, float]]:
            """
            Returns 4 corner points of a rectangle centered at 'center', oriented along 'direction'.
            Length and depth are scaled based on wall_thickness due to stretched coordinate system.
            """
            dx, dy = direction
            norm = math.hypot(dx, dy)
            if norm == 0:
                return [center] * 4  # degenerate case

            # Normalize direction
            dx, dy = dx / norm, dy / norm
            perp_dx, perp_dy = -dy, dx  # perpendicular

            # Choose scale based on major direction (x or y dominates)
            use_x = abs(dx) > abs(dy)
            scale_length = Room.wall_thickness[0] if use_x else Room.wall_thickness[1]
            scale_depth = Room.wall_thickness[1] if use_x else Room.wall_thickness[0]

            # Set defaults if not provided
            door_length = scale_length * door_length_scale
            door_depth = scale_depth * door_depth_scale

            # Half sizes
            half_len = door_length / 2
            half_dep = door_depth / 2

            # Corners
            p1 = (center[0] - dx * half_len - perp_dx * half_dep,
                  center[1] - dy * half_len - perp_dy * half_dep)
            p2 = (center[0] + dx * half_len - perp_dx * half_dep,
                  center[1] + dy * half_len - perp_dy * half_dep)
            p3 = (center[0] + dx * half_len + perp_dx * half_dep,
                  center[1] + dy * half_len + perp_dy * half_dep)
            p4 = (center[0] - dx * half_len + perp_dx * half_dep,
                  center[1] - dy * half_len + perp_dy * half_dep)

            return [p1, p2, p3, p4]

        if len(self.coordinates) < 2 or self.level != door.level:
            return False

        segments = []

        # main outline
        coords = self.coordinates
        segments += [(coords[i], coords[i + 1]) for i in range(len(coords) - 1)]
        segments.append((coords[-1], coords[0]))  # wrap

        # holes
        for hole in self.holes:
            segments += [(hole[i], hole[i + 1]) for i in range(len(hole) - 1)]
            segments.append((hole[-1], hole[0]))  # wrap

        # check all segments
        for a, b in segments:
            if distance_to_segment(door.coordinates, a, b) <= tolerance:
                direction = (b[0] - a[0], b[1] - a[1])
                centered = make_centered_rectangle(door.coordinates, direction)
                door.geometry = centered
                return True

        return False



    def _setup_paths(self):
        """
        Links the doors vertices using edges.
        For that the path over the grid is calculated using A* and saved in the edge for later visualization.
        """

        # If there's only one door, nothing to do
        if len(self.doors) == 1:
            return

        # each combination of doors can be navigated
        for i, start_door in enumerate(self.doors):
            for j, goal_door in enumerate(self.doors):

                # only compute each pair once
                if i >= j:
                    continue


                # Start from the closest grid position to the doors
                start_point = self.get_closest_grid_position(start_door.coordinates)
                goal_point = self.get_closest_grid_position(goal_door.coordinates)

                # Find path between points
                path = self._a_star_pathfinding(start_point, goal_point)

                if path:
                    # Calculate total path length
                    path_length = 0
                    for k in range(len(path) - 1):
                        path_length += math.sqrt(
                            (path[k][0] - path[k + 1][0]) ** 2 +
                            (path[k][1] - path[k + 1][1]) ** 2
                        )

                    # Add distance from doors to the closest grid points
                    start_dist = math.sqrt(
                        (start_door.coordinates[0] - start_point.x) ** 2 +
                        (start_door.coordinates[1] - start_point.y) ** 2
                    )
                    goal_dist = math.sqrt(
                        (goal_door.coordinates[0] - goal_point.x) ** 2 +
                        (goal_door.coordinates[1] - goal_point.y) ** 2
                    )

                    total_length = path_length + start_dist + goal_dist

                    path = Room.simplify_geometry(path[1:-1], loop=False) # Remove start and end points + cleanup
                    path = self.smooth_path(path)
                    # Create direct edge between door vertices with weight and path
                    self.graph.add_edge_bidirectional(start_door.vertex, goal_door.vertex, NavigationPath(weight=total_length, points=path))


        print("Setup graph for room", self.name)

    def get_closest_grid_position(self, position: Tuple[float, float]) -> PathVertex:
        """
        Returns the closest position on the grid that contains a valid point.
        """
        x, y = position
        closest_point = None
        min_distance = float("inf")

        # loop over the grid to find the closest point
        for row in self.grid:
            for point in row:
                if point:
                    px, py = point.get_coordinates()
                    dist = (px - x) ** 2 + (py - y) ** 2
                    if dist < min_distance:
                        min_distance = dist
                        closest_point = point

        # if the no point was found the grid was empty, meaning _generateGrid() was not called or is faulty
        if closest_point is None:
            raise ValueError(f"No point found in grid for the given position. Grid {self}")

        return closest_point

    def _a_star_pathfinding(self, start_vertex: PathVertex, goal_vertex: PathVertex) -> List[Tuple[float, float]]:
        """
        A* pathfinding from a start point to the goal point, over the grid points in the room.
        Returns a list of coordinate tuples representing the path.
        """

        # Helper function to get neighbors of a PathVertex
        def get_neighbors(vertex: PathVertex) -> List[PathVertex]:
            neighbors = []
            y_index, x_index = vertex.get_indices()  # Row, Column format

            # Check all 8 adjacent cells (can be none or not existent)
            neighbor_indices = [
                (y_index - 1, x_index),  # above
                (y_index + 1, x_index),  # below
                (y_index, x_index - 1),  # left
                (y_index, x_index + 1),  # right
                (y_index - 1, x_index - 1),  # top-left
                (y_index - 1, x_index + 1),  # top-right
                (y_index + 1, x_index - 1),  # bottom-left
                (y_index + 1, x_index + 1)  # bottom-right
            ]

            # check if they are actually valid
            for ni, nj in neighbor_indices:
                if 0 <= ni < len(self.grid) and 0 <= nj < len(self.grid[0]) and self.grid[ni][nj] is not None:
                    neighbors.append(self.grid[ni][nj])

            return neighbors


        if start_vertex is None or goal_vertex is None:
            return []  # No valid vertices found

        # Use a counter to break ties
        counter = 0
        open_set = []
        heapq.heappush(open_set, (0, counter, start_vertex))
        counter += 1

        # For O(1) lookups - using id() since PathVertex objects need to be compared by identity
        open_set_hash = {id(start_vertex)}

        # Tracking best paths
        came_from = {}
        g_score = {id(start_vertex): 0}
        f_score = {id(start_vertex): start_vertex.distance(goal_vertex)}

        while open_set:
            # heapq for efficient priority queue (should be a fibonacci heap?)
            _, _, current = heapq.heappop(open_set)
            open_set_hash.remove(id(current))

            # If current position is the goal, reconstruct path
            if current == goal_vertex:
                path = []
                while id(current) in came_from:
                    path.append(current.get_coordinates())
                    current = came_from[id(current)]
                path.append(start_vertex.get_coordinates())
                path.reverse()
                return path

            # Check all neighbors for next and update distances
            for neighbor in get_neighbors(current):
                # Calculate movement cost
                move_cost = current.distance(neighbor)

                # Add wall proximity penalty (for more natural path not snaking along walls)
                # can experiment with this and look how the paths change
                distance_to_wall = neighbor.distance_to_wall
                penalty = 2.0 - min(1.0, distance_to_wall * 100000)
                move_cost *= penalty

                neighbor_id = id(neighbor)

                # tentative_g_score = shortest path to current, which is known at this time
                tentative_g_score = g_score[id(current)] + move_cost

                # update distance if current + move is shorter
                if neighbor_id not in g_score or tentative_g_score < g_score[neighbor_id]:
                    came_from[neighbor_id] = current
                    g_score[neighbor_id] = tentative_g_score

                    # estimated distance to goal from this neighbour
                    # euclidean_distance to goal_vertex used as heuristic for how far the goal is
                    f_value = tentative_g_score + neighbor.distance(goal_vertex)
                    f_score[neighbor_id] = f_value

                    # if this neighbour was never seen add it to the know points
                    if neighbor_id not in open_set_hash:
                        heapq.heappush(open_set, (f_value, counter, neighbor))
                        counter += 1
                        open_set_hash.add(neighbor_id)

        return []  # No path found




    def plot(self, color=None):
        """plot the room for debugging purposes"""

        # Generate a random color if none provided
        if color is None:
            color = (random.random(), random.random(), random.random())

        # Plot room outline
        if len(self.coordinates) > 0 and isinstance(self.coordinates[0], tuple):
            coords = self.coordinates + [self.coordinates[0]]  # close the loop
            x, y = zip(*coords)
        else:
            x, y = [], []

        plt.plot(x, y, color=color, alpha=0.5)

        # Plot the doors, colors for how many rooms are linked to them
        for door in self.doors:
            x, y = door.coordinates
            if len(door.rooms) == 2:
                plt.scatter(x, y, color="green", alpha=0.8, s=20)
            else:
                plt.scatter(x, y, color="red", alpha=0.8, s=20) # (1 on outside walls is normal)


    @classmethod
    def simplify_geometry(cls, coordinates: List[Tuple[float, float]], loop: bool = True, tolerance:float = None ) -> List[Tuple[float, float]]:
        if not tolerance:
            tolerance = Room.wall_thickness[0]

        def distance_from_point_to_line(p1, p2, center_point):
            x1, y1 = p1
            x2, y2 = p2
            x3, y3 = center_point

            numerator = abs((y2 - y1) * x3 - (x2 - x1) * y3 + x2 * y1 - y2 * x1)
            denominator = math.sqrt((y2 - y1) ** 2 + (x2 - x1) ** 2)

            # Check if denominator is zero (points are the same)
            if denominator == 0:
                return 0

            return numerator / denominator

        def distance_between_points(p1, p2):
            x1, y1 = p1
            x2, y2 = p2
            return math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)

        def remove_close_points(coords, threshold):
            """Remove consecutive points that are too close to each other."""
            temp_coords = []
            for index in range(len(coords)):
                p1 = coords[index]
                if len(temp_coords) > 0 and distance_between_points(p1, temp_coords[-1]) < threshold:
                    # Skip adding p1 if it's too close to the previous point
                    continue
                temp_coords.append(p1)
            return temp_coords

        if len(coordinates) < 3:
            return coordinates  # could also return [] prob

        if coordinates[0] == coordinates[-1]:
            coordinates = coordinates[:-1]

        coordinates = remove_close_points(coordinates, tolerance)
        #visualize_shapely_polygon(Polygon(coordinates))

        cleaned_something = True
        before_count = len(coordinates)

        while cleaned_something:

            cleaned_something = False
            if len(coordinates) < 4:
                return coordinates


            span = range(len(coordinates)) if loop else range(1,len(coordinates)-1)
            for index in span:
                p1 = coordinates[(index -1) % len(coordinates)] # Wrap around
                p2 = coordinates[index]
                p3 = coordinates[(index + 1) % len(coordinates)]

                # Check the distance of point p2 from the line defined by p1 and p3
                dist = distance_from_point_to_line(p1, p3, p2)

                if dist < tolerance/4: # most "unnecessary" vertices are directly on the line, so this doesn't need to be bigger
                    cleaned_something = True
                    # i don't know why this has to be here, but otherwise it doesn't work correctly
                    coordinates.remove(p2)
                    break


        if loop:
            print("simplified geometry from", before_count, "to", len(coordinates), "vertecies")
        else:
            print("simplified path from", before_count, "to", len(coordinates), "vertecies")

        #visualize_shapely_polygon(Polygon(coordinates), title="after")

        return coordinates


    def smooth_path(self, path: List[Tuple[float, float]]) -> List[Tuple[float, float]]:
        """
        1. First removes collinear vertices (points that lie on straight lines)
        2. Then removes edge vertices while ensuring the path remains inside the room
           and maintains a safe distance from walls

        this is quite slow

        :param path: List of coordinate tuples representing the path
        :return: A smoothed path with unnecessary vertices removed
        """
        if len(path) <= 2:
            return path

        # Phase 1: Remove collinear points first
        i = 1
        while i < len(path) - 1:
            prev_point = path[i - 1]
            current_point = path[i]
            next_point = path[i + 1]

            # Check if current point is collinear with its neighbors
            if self._is_collinear(prev_point, current_point, next_point):
                path.pop(i)
                # Don't increment i, as we want to check the new sequence
            else:
                i += 1

        # Phase 2: Remove edge vertices that can be skipped while maintaining safe distance from walls
        cleaned_something = True
        while cleaned_something and len(path) > 2:
            cleaned_something = False

            i = 1 + random.randint(0, 1)
            while i < len(path) - 1:
                prev_point = path[i - 1]
                current_point = path[i]
                next_point = path[i + 1]

                # Only remove the vertex if:
                # 1. The edge from prev to next would be fully inside the room
                # 2. The current point is not too close to a wall
                # 3. The new edge would not pass too close to a wall
                if (self._is_edge_walkable(prev_point, next_point) and
                        not self._is_close_to_wall(current_point) and
                        not self._edge_too_close_to_wall(prev_point, next_point)):
                    path.pop(i)
                    cleaned_something = True
                    # Sometimes skip to next vertex to avoid over-smoothing
                    i += random.randint(0, 1)
                else:
                    i += 1

        return path

    def _is_collinear(self, p1: Tuple[float, float], p2: Tuple[float, float],
                      p3: Tuple[float, float], tolerance: float = 0.00000000001) -> bool:
        """
        Determines if three points are approximately collinear (on the same straight line).

        Uses the area of the triangle formed by the three points. If area is near zero,
        the points are collinear.

        :param p1, p2, p3: The three points to check
        :param tolerance: Threshold for determining collinearity
        :return: True if points are collinear, False otherwise
        """
        # Calculate the area of the triangle formed by the three points
        area = abs(p1[0] * (p2[1] - p3[1]) + p2[0] * (p3[1] - p1[1]) + p3[0] * (p1[1] - p2[1])) / 2.0

        # print(area)
        # Points are collinear if the area is approximately zero
        return area < tolerance

    #change the num_checks for faster performance, this takes ages
    def _is_edge_walkable(self, point1: Tuple[float, float], point2: Tuple[float, float], num_checks: int = 30) -> bool:
        """
        Checks if the straight line between two points is entirely walkable within the room.

        :param point1: Starting point as (x, y) tuple
        :param point2: Ending point as (x, y) tuple
        :param num_checks: Number of points to check along the line
        :return: True if the entire edge is walkable, False otherwise
        """
        # Check if endpoints are walkable
        if not self.is_walkable(point1) or not self.is_walkable(point2):
            return False

        # Check multiple points along the line
        for i in range(1, num_checks):
            t = i / num_checks
            check_point = (
                point1[0] + t * (point2[0] - point1[0]),
                point1[1] + t * (point2[1] - point1[1])
            )

            if not self.is_walkable(check_point):
                return False

        return True

    def _is_close_to_wall(self, point: Tuple[float, float], safety_distance: float = 0.00003) -> bool:
        """
        Determines if a point is too close to any wall.

        :param point: The point to check as (x, y) tuple
        :param safety_distance: Minimum safe distance from walls
        :return: True if the point is too close to a wall, False otherwise
        """
        # Use the existing distance_to_wall method
        distance = self.distance_to_wall(point)
        return distance < safety_distance

    def _edge_too_close_to_wall(self, point1: Tuple[float, float], point2: Tuple[float, float],
                                num_checks: int = 10, safety_distance: float = 0.00003) -> bool:
        """
        Checks if any point along the edge between two points is too close to a wall.

        :param point1: Starting point as (x, y) tuple
        :param point2: Ending point as (x, y) tuple
        :param num_checks: Number of points to check along the line
        :param safety_distance: Minimum safe distance from walls
        :return: True if the edge passes too close to a wall, False otherwise
        """
        # Check multiple points along the line
        for i in range(1, num_checks):
            t = i / num_checks
            check_point = (
                point1[0] + t * (point2[0] - point1[0]),
                point1[1] + t * (point2[1] - point1[1])
            )

            # Check if this point is too close to a wall
            if self._is_close_to_wall(check_point, safety_distance):
                return True

        return False


def distance_to_segment(point, a, b):
    """ Helper to calculate distance from point to line segment (a, b) """
    ax, ay = a
    bx, by = b
    point_x, point_y = point

    # Vector a -> b
    abx, aby = bx - ax, by - ay
    ab_length_sq = abx ** 2 + aby ** 2

    if ab_length_sq == 0:
        # a == b
        return ((point_x - ax) ** 2 + (point_y - ay) ** 2) ** 0.5

    # projection from point onto line
    t = max(0, min(1, ((point_x - ax) * abx + (point_y - ay) * aby) / ab_length_sq))
    closest_x = ax + t * abx
    closest_y = ay + t * aby

    # distance from point to the closest point on segment
    return ((point_x - closest_x) ** 2 + (point_y - closest_y) ** 2) ** 0.5


