import math
from typing import Tuple, List, Any, Dict, Optional
from matplotlib import pyplot as plt
from shapely.geometry.point import Point

from door import Door
from dataclasses import dataclass
import heapq
from shapely.geometry import Polygon

from graph import Graph, NavigationPath


@dataclass
class PathVertex:
    x: float
    y: float
    x_index: int
    y_index: int
    distance_to_wall: float # this is saved here, as it would otherwise be calculated multiple times (and that's an expensive one)

    def get_coordinates(self) -> Tuple[float, float]:
        """Get the (x, y) coordinates as a tuple"""
        return self.x, self.y

    def get_indices(self) -> Tuple[int, int]:
        """Get the grid indices as a tuple"""
        return self.y_index, self.x_index  # Row, Column format

    def euclidean_distance(self, other: "PathVertex") -> float:
        """Calculate the Euclidean distance to another PathVertex"""
        return math.sqrt((self.x - other.x) ** 2 + (self.y - other.y) ** 2)

@dataclass
class BoundingBox:
    #(min_x, min_y), (max_x, max_y)
    min_x: float
    min_y: float

    max_x: float
    max_y: float

    def size(self) -> float:
        return (self.max_x - self.min_x) * (self.max_y - self.min_y)

    def get_center(self) -> Tuple[float, float]:
        """
        Berechnet den Mittelpunkt der Bounding Box.
        """
        return (self.min_x + self.max_x) / 2, (self.min_y + self.max_y) / 2

    def overlaps_with(self, other):
        return not (self.max_x < other.min_x or self.min_x > other.max_x or
                    self.max_y < other.min_y or self.min_y > other.max_y)


    def is_inside(self, point_gps_pos: Tuple[int, int]) -> bool:
        """
        Prüft, ob eine Position innerhalb der Bounding Box liegt.

        :param point_gps_pos: Tuple aus (x, y)-Koordinaten des Punktes.
        :return: True, wenn die Position innerhalb der Bounding Box liegt, sonst False.
        """
        x, y = point_gps_pos
        return self.min_x <= x <= self.max_x and self.min_y <= y <= self.max_y

    def diagonal_length(self) -> float:
        # Calculate the Euclidean distance between the bottom-left and top-right corners
        return math.sqrt((self.max_x - self.min_x) ** 2 + (self.max_y - self.min_y) ** 2)


class Room:
    # Definiere die Größe eines Gitterschritts (in gps coords)
    grid_size_x: float = 0.00001
    grid_size_y: float = 0.00001

    # used to remove extremely thin parts of the geometry and for linking doors with walls (in gps)
    wall_thickness: float = 0.000001

    def __init__(self, json: Dict[str, Any], graph: Graph):
        """
        Erstellt ein Room-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        properties = json.get("properties", {})
        self.level: int = int(properties.get("level"))
        self.name: str = properties.get("name", "")

        geometry = json.get("geometry", {})
        self.geometry_type: str = geometry.get("type", "")

        self.coordinates: List[Tuple[float, float]] = [
            (float(coord[0]), float(coord[1])) for coord in geometry.get("coordinates", [])
        ]
        self.holes: List[List[Tuple[float, float]]]= [] # this is more compatible and easier to troubleshoot as nested rooms, which would also be a valid solution

        self.doors: List[Door] = []
        self.graph = graph

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        # (compared to the ray-cast, but this cant be used in all cases)
        self.bounding_box: BoundingBox = self._compute_bounding_box() #(min_x, min_y), (max_x, max_y)
        self.grid: List[List[Optional[PathVertex]]] = []



    def __repr__(self):
        return f"Room (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"

    def _compute_bounding_box(self) -> BoundingBox:
        """
        Berechnet eine quadratische Bounding Box um die Raumgeometrie.

        :return: Ein Tupel mit zwei Punkten (min_x, min_y) und (max_x, max_y), die die Bounding Box definieren.
        """
        if not self.coordinates:
            return BoundingBox(0, 0, 0, 0)

        min_x = min(coord[0] for coord in self.coordinates)
        max_x = max(coord[0] for coord in self.coordinates)
        min_y = min(coord[1] for coord in self.coordinates)
        max_y = max(coord[1] for coord in self.coordinates)

        return BoundingBox(min_x, min_y, max_x, max_y)


    @staticmethod
    def setup_all_rooms(rooms: List["Room"]) -> None:

        print("setting up rooms...")

        # check each unique pair of rooms
        for i in range(len(rooms)):
            print("checking geometry of",  rooms[i].name)

            for j in range(i + 1, len(rooms)):
                rooms[i]._fix_intersections(rooms[j])


        for room in rooms:
            print("setting up grid for room", room.name)
            room.grid = room._generate_grid()



    def _fix_intersections(self, other: "Room") -> bool:
        """
        Subtracts another polygon area from this room's coordinates.

        Will remove overlapping areas from the bigger room.
        Will also handle holes in the room.

        :param other:
        :return: A new list of coordinates representing the room with the subtracted area
        """


        if self.level != other.level:
            return False

        # Check if the bounding boxes intersect (as this is much faster than checking the polygon)
        bounding_box_intersect = not (
            self.bounding_box.max_x < other.bounding_box.min_x or
            self.bounding_box.min_x > other.bounding_box.max_x or
            self.bounding_box.max_y < other.bounding_box.min_y or
            self.bounding_box.min_y > other.bounding_box.max_y
        )
        if not bounding_box_intersect:
            return False

        # Convert room coordinates to a Shapely polygon
        room_polygon = Polygon(self.coordinates)

        # Convert the other coordinates to a Shapely polygon
        other_polygon = Polygon(other.coordinates)

        # Check if the polygons intersect
        if not room_polygon.intersects(other_polygon):
            # If there's no intersection, return the original coordinates
            return False

        if other_polygon.area > room_polygon.area:
            # if this room is contained in the other one, remove parts from the other
            return other._fix_intersections(self)

        # Perform the subtraction (difference operation)
        result_polygon = (room_polygon - other_polygon)
        for hole in self.holes:
            result_polygon = result_polygon - Polygon(hole)

        # buffer in and then out, to keep the geometry but remove extremely thin parts, which happen due to the -
        # this causes rounded geometry, wich is not directly a problem, but leads to longer calculations
        result_polygon = result_polygon.buffer(-Room.wall_thickness).buffer(Room.wall_thickness)

        # Convert the result back to a list of coordinates
        if result_polygon.geom_type == 'Polygon':
            # For simple polygons
            self.coordinates = list(result_polygon.exterior.coords)[:-1]  # Remove the last point as it's the same as the first
            self.holes = [list(interior.coords)[:-1] for interior in result_polygon.interiors]  # Hole coordinates
            return True

        elif result_polygon.geom_type == 'MultiPolygon':

            # this is not a problem, as the room is not a simple polygon in this case
            largest_polygon = max(result_polygon.geoms, key=lambda p: p.area)
            self.coordinates = list(largest_polygon.exterior.coords)[:-1]
            self.holes = [list(interior.coords)[:-1] for interior in largest_polygon.interiors]  # Hole coordinates
            return True
        else:
            # If the result is not a polygon (unlikely), return the original
            return False



    def _generate_grid(self) -> List[List[Optional[PathVertex]]]:

        grid: List[List[Optional[PathVertex]]] = []

        has_vertex = False  # Flag to check if any vertex was added
        y = self.bounding_box.min_y
        y_index = 0

        while y <= self.bounding_box.max_y:
            row = []
            x = self.bounding_box.min_x
            x_index = 0

            while x <= self.bounding_box.max_x:
                if self.is_walkable((x, y)):
                    # Create PathVertex with coordinates and indices
                    vertex = PathVertex(x=x, y=y, x_index=x_index, y_index=y_index, distance_to_wall=self.distance_to_wall((x,y)))
                    row.append(vertex)
                    has_vertex = True
                else:
                    # This is needed so the indices are correct in the grid
                    row.append(None)

                x += Room.grid_size_x
                x_index += 1

            grid.append(row)
            y += Room.grid_size_y
            y_index += 1

        if not has_vertex:
            center: Tuple[float, float] = self.bounding_box.get_center()
            # Create a single PathVertex at the center
            center_vertex = PathVertex(x=center[0], y=center[1], x_index=0, y_index=0, distance_to_wall=self.distance_to_wall((center[0],center[1])))
            grid = [[center_vertex]]

        return grid

    def get_closest_grid_position(self, position: Tuple[float, float]) -> PathVertex:
        """
        Returns the closest position on the grid that contains a point.
        """
        x, y = position
        closest_point = None
        min_distance = float("inf")

        for row in self.grid:
            for point in row:
                if point:
                    px, py = point.get_coordinates()
                    dist = (px - x) ** 2 + (py - y) ** 2
                    if dist < min_distance:
                        min_distance = dist
                        closest_point = point

        if closest_point is None:
            raise ValueError(f"No point found in grid for the given position. Grid {self}")

        return closest_point



    def is_door_on_outline(self, door: Door, tolerance: float = wall_thickness) -> bool:
        """
        Überprüft, ob ein gegebener Punkt auf der Umrandung des Raums liegt (innerhalb der Toleranz).
        for gps (non-planar) coordinates not perfectly accurate, but close enough as long as the distances stay short

        :param door:
        :param tolerance: Die maximale Abweichung, um als "auf der Linie" zu gelten.
        :return: True, wenn der Punkt auf der Umrandung liegt, sonst False.
        """
        if len(self.coordinates) < 2 or self.level != door.level:
            return False  # Ein einzelner Punkt oder leere Geometrie kann keine Umrandung haben

        def distance_to_segment(point, a, b):
            """ Berechnet den minimalen Abstand zwischen Punkt p und dem Liniensegment (a, b). """
            ax, ay = a
            bx, by = b
            point_x, point_y = point

            # Vektor a -> b
            abx, aby = bx - ax, by - ay
            ab_length_sq = abx ** 2 + aby ** 2

            if ab_length_sq == 0:
                # a und b sind der gleiche Punkt
                return ((point_x - ax) ** 2 + (point_y - ay) ** 2) ** 0.5

            # Projektion des Punktes p auf die Linie
            t = max(0, min(1, ((point_x - ax) * abx + (point_y - ay) * aby) / ab_length_sq))
            closest_x = ax + t * abx
            closest_y = ay + t * aby

            # Distanz vom Punkt p zur nächsten Position auf der Linie
            return ((point_x - closest_x) ** 2 + (point_y - closest_y) ** 2) ** 0.5

        for i in range(len(self.coordinates) - 1):
            if distance_to_segment(door.coordinates, self.coordinates[i], self.coordinates[i + 1]) <= tolerance:
                return True

        # das letzte Segment prüfen (letzter zu erster Punkt)
        if distance_to_segment(door.coordinates, self.coordinates[-1], self.coordinates[0]) <= tolerance:
            return True

        for hole in self.holes:
            for i in range(len(hole) - 1):
                if distance_to_segment(door.coordinates, hole[i], hole[i + 1]) <= tolerance:
                    return True

            if distance_to_segment(door.coordinates, hole[-1], hole[0]) <= tolerance:
                return True


        return False

    def setup_paths(self):
        """
        Creates a graph that connects each door pair (A, B) with a direct edge
        weighted by the path length. Stores paths in self.paths dictionary.
        """

        if len(self.doors) == 1:
            # If there's only one door, nothing to do
            return

        for i, start_door in enumerate(self.doors):
            for j, goal_door in enumerate(self.doors):
                if i >= j:  # Only compute once (avoids duplicate calculations)
                    continue

                # Get closest grid points to doors
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

                    # Add distance from doors to closest grid points
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
                    # Create direct edge between door vertices with weight
                    self.graph.add_edge_bidirectional(start_door.vertex, goal_door.vertex, NavigationPath(weight=total_length, points=path))


        print("Setup graph for room", self.name)



    @staticmethod
    def cleanup_path(path: List[Tuple[float, float]]) -> List[Tuple[float, float]]:
        """
        Remove unnecessary vertices from a path.
        A vertex is considered unnecessary if it lies on a straight line between its neighbors.

        Args:
            path: List of (x, y) coordinates representing vertices on a path

        Returns:
            Cleaned path with unnecessary vertices removed
        """

        points = path.copy()

        for i in range(1, len(path) - 1):
            prev_point = path[i - 1]
            curr_point = path[i]
            next_point = path[i + 1]

            # Check if the current point is collinear with its neighbors
            if (curr_point[0] - prev_point[0]) * (next_point[1] - curr_point[1]) == (curr_point[1] - prev_point[1]) * (next_point[0] - curr_point[0]):
                # Remove the current point
                points[i] = None

        return [item for item in points if item is not None]





    def _a_star_pathfinding(self, start_vertex: PathVertex, goal_vertex: PathVertex) -> List[Tuple[float, float]]:
        """
        A* pathfinding from a start point to the goal point.
        Returns a list of coordinate tuples representing the path.
        """

        # Helper function to get neighbors of a PathVertex
        def get_neighbors(vertex: PathVertex) -> List[PathVertex]:
            neighbors = []
            y_index, x_index = vertex.get_indices()  # Row, Column format

            # Check all 8 adjacent cells
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
        f_score = {id(start_vertex): start_vertex.euclidean_distance( goal_vertex)}

        while open_set:
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

            # Check all neighbors
            for neighbor in get_neighbors(current):
                # Calculate movement cost
                move_cost = current.euclidean_distance(neighbor)

                # Add wall proximity penalty
                distance_to_wall = neighbor.distance_to_wall
                penalty = 2.0 - min(1.0, distance_to_wall * 100000)
                move_cost *= penalty

                neighbor_id = id(neighbor)
                tentative_g_score = g_score[id(current)] + move_cost

                if neighbor_id not in g_score or tentative_g_score < g_score[neighbor_id]:
                    came_from[neighbor_id] = current
                    g_score[neighbor_id] = tentative_g_score
                    f_value = tentative_g_score + neighbor.euclidean_distance(goal_vertex)
                    f_score[neighbor_id] = f_value

                    if neighbor_id not in open_set_hash:
                        heapq.heappush(open_set, (f_value, counter, neighbor))
                        counter += 1
                        open_set_hash.add(neighbor_id)

        return []  # No path found

    def is_walkable(self, point_gps_pos: Tuple[int, int]) -> bool:
        """
        Determines whether a position is walkable by checking if it is within the room's polygon
        but outside any holes.

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



    def plot(self, color=None, scale=1.0):
        import random

        # Generate a random color if none provided
        if color is None:
            color = (random.random(), random.random(), random.random())

        # Plot room outline slightly scaled toward the center
        if len(self.coordinates) > 0 and isinstance(self.coordinates[0], tuple):
            cx, cy = self.bounding_box.get_center()
            scaled_coords = [
                (
                    cx + scale * (x - cx),
                    cy + scale * (y - cy)
                )
                for x, y in self.coordinates
            ]
            scaled_coords.append(scaled_coords[0])  # close the loop
            x, y = zip(*scaled_coords)
        else:
            x, y = [], []

        plt.plot(x, y, color=color, alpha=0.5)

        # Plot the doors
        for door in self.doors:
            x, y = door.coordinates
            if len(door.rooms) == 1:
                plt.scatter(x, y, color="orange", alpha=0.8, s=20)
            if len(door.rooms) == 2:
                plt.scatter(x, y, color="green", alpha=0.8, s=20)
            if len(door.rooms) == 3:
                plt.scatter(x, y, color="red", alpha=0.8, s=20)
            if len(door.rooms) > 3:
                plt.scatter(x, y, color="blue", alpha=0.8, s=20)

    # Plot the holes
    def distance_to_wall(self, point: Tuple[float, float]) -> float:
        """
        Calculates the minimum distance from a point to any wall (edge) of the room, considering holes.

        :param point: The point (x, y) to check distance from
        :return: The minimum distance to any wall
        """
        if not self.coordinates or len(self.coordinates) < 2:
            return 0.0  # No walls to measure distance from

        min_distance = float('inf')

        # Helper function to calculate distance from point to line segment
        def distance_to_segment(p, a, b):
            """Calculate distance from point p to line segment (a, b)"""
            x, y = p
            x1, y1 = a
            x2, y2 = b

            # Vector from a to b
            dx = x2 - x1
            dy = y2 - y1

            # If segment is just a point, return distance to that point
            if dx == 0 and dy == 0:
                return math.sqrt((x - x1) ** 2 + (y - y1) ** 2)

            # Calculate projection of point onto line
            t = ((x - x1) * dx + (y - y1) * dy) / (dx ** 2 + dy ** 2)

            # Constrain t to line segment
            t = max(0, min(1, t))

            # Calculate closest point on segment
            closest_x = x1 + t * dx
            closest_y = y1 + t * dy

            # Return distance to closest point
            return math.sqrt((x - closest_x) ** 2 + (y - closest_y) ** 2)

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


