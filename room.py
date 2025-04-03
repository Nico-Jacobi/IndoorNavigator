import math
from typing import Tuple, List, Any, Dict, Optional
from matplotlib import pyplot as plt
from door import Door
from dataclasses import dataclass
import heapq

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


class Room:
    # Definiere die Größe eines Gitterschritts (abhängig von den verwendeten GPS-Koordinaten)
    grid_size_x = 0.00001
    grid_size_y = 0.00001

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

        self.doors: List[Door] = []

        self.graph = graph

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        # (compared to the ray-cast, but this cant be used in all cases)
        self.bounding_box: Tuple[Tuple[float, float], Tuple[float, float]] = self._compute_bounding_box()
        self.grid: List[List[Optional[PathVertex]]] = self._generate_grid()



    def __repr__(self):
        return f"Room (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"

    def _compute_bounding_box(self) -> Tuple[Tuple[float, float], Tuple[float, float]]:
        """
        Berechnet eine quadratische Bounding Box um die Raumgeometrie.

        :return: Ein Tupel mit zwei Punkten (min_x, min_y) und (max_x, max_y), die die Bounding Box definieren.
        """
        if not self.coordinates:
            return (0, 0), (0, 0)

        min_x = min(coord[0] for coord in self.coordinates)
        max_x = max(coord[0] for coord in self.coordinates)
        min_y = min(coord[1] for coord in self.coordinates)
        max_y = max(coord[1] for coord in self.coordinates)

        return (min_x, min_y), (max_x, max_y)

    def _generate_grid(self) -> List[List[Optional[PathVertex]]]:
        min_x, min_y = self.bounding_box[0]
        max_x, max_y = self.bounding_box[1]

        grid: List[List[Optional[PathVertex]]] = []

        has_vertex = False  # Flag to check if any vertex was added
        y = min_y
        y_index = 0

        while y <= max_y:
            row = []
            x = min_x
            x_index = 0

            while x <= max_x:
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
            center: Tuple[float, float] = self.get_center()
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

    def get_center(self) -> Tuple[float, float]:
        """
        Berechnet den Mittelpunkt der Bounding Box.
        """
        (min_x, min_y), (max_x, max_y) = self.bounding_box
        return (min_x + max_x) / 2, (min_y + max_y) / 2


    def is_point_on_outline(self, point: Tuple[float, float], tolerance: float = 0.000001) -> bool:
        """
        Überprüft, ob ein gegebener Punkt auf der Umrandung des Raums liegt (innerhalb der Toleranz).
        for gps (non-planar) coordinates not perfectly accurate, but close enough as long as the distances stay short

        :param point: Der zu überprüfende Punkt als (x, y)-Tupel.
        :param tolerance: Die maximale Abweichung, um als "auf der Linie" zu gelten.
        :return: True, wenn der Punkt auf der Umrandung liegt, sonst False.
        """
        if len(self.coordinates) < 2:
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
            if distance_to_segment(point, self.coordinates[i], self.coordinates[i + 1]) <= tolerance:
                return True

        # das letzte Segment prüfen (letzter zu erster Punkt)
        if self.coordinates[0] == self.coordinates[-1]:
            if distance_to_segment(point, self.coordinates[-1], self.coordinates[0]) <= tolerance:
                return True

        return False

    def setup_graph(self):
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

                    # Create direct edge between door vertices with weight
                    self.graph.add_edge_bidirectional(start_door.vertex, goal_door.vertex, NavigationPath(weight=total_length, points=path))


        print("Setup graph for room", self.name)

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


    def is_in_bounding_box(self, point_gps_pos: Tuple[int, int]) -> bool:
        """
        Prüft, ob eine Position innerhalb der Bounding Box liegt.

        :param point_gps_pos: Tuple aus (x, y)-Koordinaten des Punktes.
        :return: True, wenn die Position innerhalb der Bounding Box liegt, sonst False.
        """
        x, y = point_gps_pos
        (min_x, min_y), (max_x, max_y) = self.bounding_box
        return min_x <= x <= max_x and min_y <= y <= max_y

    def is_walkable(self, point_gps_pos: Tuple[int, int]) -> bool:
        """
        Bestimmt, ob eine Position begehbar ist, indem überprüft wird, ob sie sich innerhalb des Raum-Polygons befindet.

        :param point_gps_pos: Tuple aus (x, y)-Koordinaten des Punktes.
        :return: True, wenn die Position innerhalb des Raumpolygons liegt, sonst False.
        """
        if not self.is_in_bounding_box(point_gps_pos):
            return False

        x, y = point_gps_pos
        # Raycasting-Algorithmus mit Bounding-Box-Begrenzung
        inside = False
        j = len(self.coordinates) - 1

        for i in range(len(self.coordinates)):
            xi, yi = self.coordinates[i]
            xj, yj = self.coordinates[j]

            # Prüft, ob der Strahl eine Kante schneidet
            if (yi > y) != (yj > y):
                x_intersect = (xj - xi) * (y - yi) / (yj - yi) + xi
                if x <= x_intersect <= self.bounding_box[1][0]:  # Begrenzt den Strahl auf die Bounding Box
                    inside = not inside

            j = i

        return inside

    def plot(self, color=None):
        import random

        # Generate a random color if none provided
        if color is None:
            color = (random.random(), random.random(), random.random())

        # Plot room outline
        x, y = zip(*self.coordinates) if isinstance(self.coordinates[0], tuple) else ([], [])
        plt.plot(x, y, color=color, alpha=0.5)

        # Plot all points in the grid
        for row in self.grid:
            for point in row:
                if point is not None:
                    x,y = point.get_coordinates()
                    plt.scatter(x,y, color=color, s=1, alpha=0.5)

        # Plot the doors
        for door in self.doors:
            x, y = door.coordinates
            plt.scatter(x, y, color=color, alpha=0.8, s=20)  # Doors are plotted slightly larger



    def distance_to_wall(self, point: Tuple[float, float]) -> float:
        """
        Calculates the minimum distance from a point to any wall (edge) of the room.

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

        # Check distance to each wall segment
        for i in range(len(self.coordinates)):
            j = (i + 1) % len(self.coordinates)  # Next point, wrapping around to first point

            # Calculate distance to this wall segment
            distance = distance_to_segment(point, self.coordinates[i], self.coordinates[j])

            # Update minimum distance if this is smaller
            min_distance = min(min_distance, distance)

        return min_distance

