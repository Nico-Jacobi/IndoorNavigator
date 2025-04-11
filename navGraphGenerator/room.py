import math
import random
import sys
from typing import Tuple, List, Any, Dict, Optional
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
    wall_thickness: (float, float) = meters_to_latlon(0.1, 0.1, 50.8, 8.8)

    def __init__(self, json: Dict[str, Any], graph: Graph):
        """
        :param json: JSON object containing the room data.
        :param graph: reference to the graph object to add this room to
        """

        properties = json.get("properties", {})
        self.level: int = int(properties.get("level"))
        self.name: str = properties.get("name", "")

        geometry = json.get("geometry", {})
        self.geometry_type: str = geometry.get("type", "")

        self.coordinates: List[Tuple[float, float]] = [
            (float(coord[0]), float(coord[1])) for coord in geometry.get("coordinates", [])
        ]
        self.coordinates = Room.cleanup_path(self.coordinates, is_loop=True)

        # represents holes in the geometry
        self.holes: List[List[Tuple[float, float]]]= []

        # references to the doors in this room
        self.doors: List[Door] = []
        self.graph: Graph = graph

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        self.bounding_box: BoundingBox = self._compute_bounding_box()

        # grid for finding the visual paths
        self.grid: List[List[Optional[PathVertex]]] = []


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


    def get_2d_outline_wavefront(self, origin_lat: float, origin_lon: float) -> str:
        """
        Creates an OBJ file representation of walls from room polygons.

        :param all_rooms: List of rooms.
        :param all_doors: List of doors (unused here, but can be used for door openings).
        :param height: Height of the walls (unused in this flat version).
        :return: The .obj string representation of the 3D model.
        """

        vertecies = []
        faces = []

        polygon = self.get_meter_geometry(origin_lat, origin_lon)

        # Define wall thickness (in meters)
        wall_thickness = 0.2

        # the outer one is buffered in and out, so both have the same number of vertices
        inner_polygon = polygon.buffer(-wall_thickness / 2, join_style=2).exterior.coords
        outer_polygon = polygon.buffer(-wall_thickness / 2, join_style=2).buffer(wall_thickness, join_style=2).exterior.coords

        vertecies.append(f"v {inner_polygon[0][0]} 0.0 {inner_polygon[0][1]}")
        vertecies.append(f"v {outer_polygon[0][0]} 0.0 {outer_polygon[0][1]}")
        vertex_count = 2  # OBJ indices start at 1, not 0

        for i in range(1, len(inner_polygon)):
            vertecies.append(f"v {inner_polygon[i][0]} 0.0 {inner_polygon[i][1]}")
            vertecies.append(f"v {outer_polygon[i][0]} 0.0 {outer_polygon[i][1]}")
            vertex_count += 2

            faces.append(f"f {vertex_count} {vertex_count - 1} {vertex_count - 3} {vertex_count - 2}")

        return "\n".join(vertecies + faces)



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

        # linking doors to rooms anmd vice versa
        for room in rooms:
            print("linking doors for room", room.name)
            for door in doors:
                if room.is_door_on_outline(door):
                    door.add_room(room)
                    room.doors.append(door)

        # setting up the visual paths later shown in the app
        for room in rooms:
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
        room_polygon = Polygon(self.coordinates)
        other_polygon = Polygon(other.coordinates)
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
            return True


        # if the rooms was split into multiple polygons (shouldn't happen, but just in case)
        elif result_polygon.geom_type == 'MultiPolygon':
            # "result_polygon.geoms" this is not a problem, as the room is not a simple polygon in this case
            largest_polygon = max(result_polygon.geoms, key=lambda p: p.area)
            self.coordinates = list(largest_polygon.exterior.coords)[:-1]
            self.holes = [list(interior.coords)[:-1] for interior in largest_polygon.interiors]  # Hole coordinates
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



    def is_door_on_outline(self, door: Door, tolerance: float = wall_thickness[0]) -> bool:
        """
        Checks if a door is on the outline of the room.
        (will not link those)

        :param door: The door to check.
        :param tolerance: The distance tolerance for checking proximity to the outline in gps fractions.
        """
        if len(self.coordinates) < 2 or self.level != door.level:
            return False


        # looping over all segments of the room, and check the distance to the door
        for i in range(len(self.coordinates) - 1):
            if distance_to_segment(door.coordinates, self.coordinates[i], self.coordinates[i + 1]) <= tolerance:
                return True

        # check the distance to the last segment, which is the one looping around
        if distance_to_segment(door.coordinates, self.coordinates[-1], self.coordinates[0]) <= tolerance:
            return True

        # also check the holes
        for hole in self.holes:
            for i in range(len(hole) - 1):
                if distance_to_segment(door.coordinates, hole[i], hole[i + 1]) <= tolerance:
                    return True

            # last (looping) segment
            if distance_to_segment(door.coordinates, hole[-1], hole[0]) <= tolerance:
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

                    path = Room.cleanup_path(path[1:-1]) # Remove start and end points + cleanup
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


    @staticmethod
    def cleanup_path(path: List[Tuple[float, float]], is_loop: bool = False, tolerance: float = wall_thickness[0]) -> \
    List[Tuple[float, float]]:
        """
        Remove unnecessary vertices from a path.
        A vertex is considered unnecessary if it lies on a straight line between its neighbors within a tolerance.
        (Returns a path, original stays unmodified)

        :param path: List of (x, y) coordinates representing vertices on a path
        :param tolerance: The maximum allowed distance from the line to consider the point unnecessary
        :param is_loop: If True, the first and last points are treated as neighbors
        """

        def distance_to_line(p: Tuple[float, float], a: Tuple[float, float], b: Tuple[float, float]) -> float:
            """
            Calculate the perpendicular distance from point p to the line defined by points a and b.
            """
            x1, y1 = p
            x2, y2 = a
            x3, y3 = b

            # Check if a and b are the same point, otherwise the whole thing would not work....?
            if x2 == x3 and y2 == y3:
                return float('inf')

            num = abs((x3 - x2) * (y1 - y2) - (x1 - x2) * (y3 - y2))
            den = math.sqrt((x3 - x2) ** 2 + (y3 - y2) ** 2)

            return num / den if den != 0 else 0

        # If the path has less than 3 points, no cleanup is needed
        if len(path) < 3:
            return path

        points = []

        for i in range(len(path)):
            if i == 0 or i == len(path) - 1:  # Keep the first and last points
                points.append(path[i])
            else:
                if distance_to_line(path[i], path[i - 1], path[i + 1]) > tolerance:
                    points.append(path[i])

        # If the path is a loop, check the first and last points also
        if is_loop:
            if distance_to_line(path[-1], path[-2], path[0]) > tolerance:
                points.append(path[-1])
            if distance_to_line(path[0], path[-1], path[1]) > tolerance:
                points.append(path[0])

        return points



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
