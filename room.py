import heapq
import math
from typing import Tuple, List, Any, Dict, Optional

from matplotlib import pyplot as plt

from door import Door
from graph import Vertex, Edge


class Room:
    # Definiere die Größe eines Gitterschritts (abhängig von den verwendeten GPS-Koordinaten)
    grid_size_x = 0.00001
    grid_size_y = 0.00001

    def __init__(self, json: Dict[str, Any]):
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

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        # (compared to the ray-cast, but this cant be used in all cases)
        self.bounding_box: Tuple[Tuple[float, float], Tuple[float, float]] = self._compute_bounding_box()
        self.grid: List[List[Optional[Vertex]]] = self._generate_grid()


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

    def _generate_grid(self) -> List[List[Optional[Vertex]]]:
        min_x, min_y = self.bounding_box[0]
        max_x, max_y = self.bounding_box[1]

        grid: List[List[Optional[Vertex]]] = []

        y = min_y
        while y <= max_y:
            row = []
            x = min_x
            while x <= max_x:
                vertex = Vertex(self.name, x, y, self.distance_to_wall((x, y))) if self.is_walkable((x, y)) else None
                row.append(vertex)
                x += Room.grid_size_x
            grid.append(row)
            y += Room.grid_size_y

        # Setze Nachbarn
        for i in range(len(grid)):
            for j in range(len(grid[i])):
                if grid[i][j] is None:
                    continue

                neighbours = [
                    (i - 1, j),  # oben
                    (i + 1, j),  # unten
                    (i, j - 1),  # links
                    (i, j + 1),  # rechts
                    (i - 1, j - 1),  # oben links
                    (i - 1, j + 1),  # oben rechts
                    (i + 1, j - 1),  # unten links
                    (i + 1, j + 1)  # unten rechts
                ]

                for ni, nj in neighbours:
                    if 0 <= ni < len(grid) and 0 <= nj < len(grid[i]) and grid[ni][nj] is not None:
                        grid[i][j].neighbours.add(grid[ni][nj])
        return grid


    def get_closest_grid_position(self, position: Tuple[float, float]) -> Optional[Vertex]:
        """
        Gibt die nächstgelegene Position auf dem Gitter zurück, an der sich ein Vertex befindet.
        """
        x, y = position
        closest_vertex = None
        min_distance = float("inf")

        for row in self.grid:
            for vertex in row:
                if vertex:
                    dist = (vertex.x - x) ** 2 + (vertex.y - y) ** 2
                    if dist < min_distance:
                        min_distance = dist
                        closest_vertex = vertex

        return closest_vertex

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
        Erstellt einen Graphen, der für jedes Türpaar (A, B) den Pfad speichert.
        """

        for i, start in enumerate(self.doors):
            for j, goal in enumerate(self.doors):

                if i < j:  # Nur einmal berechnen, wenn i < j (vermeidet doppelte Berechnung)

                    closest_start = self.get_closest_grid_position(start.coordinates)
                    closest_goal = self.get_closest_grid_position(goal.coordinates)

                    path = self._a_star_pathfinding(closest_start, closest_goal)
                    full_path = [start.vertex] + path + [goal.vertex]

                    if full_path[0].distance_to(full_path[1]) < Room.grid_size_x:
                        full_path.pop(1)

                    if full_path[-1].distance_to(full_path[-2]) < Room.grid_size_x:
                        full_path.pop(-2)

                    # Pfad verketten
                    for k in range(0, len(path)-1):
                        path[k].add_edge_bidirectional(path[k + 1])

        print("setup graph for room", self.name)

    def _a_star_pathfinding(self, start: Vertex, goal: Vertex) -> List[Vertex]:
        """
        A* pathfinding from a start Vertex to the goal Vertex using vertex neighbors.
        """

        def heuristic(a: Vertex, b: Vertex) -> float:
            """Euclidean distance heuristic between vertices."""
            return math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2)

        # Use a counter to break ties and prevent comparing Vertex objects
        counter = 0
        open_set = []
        heapq.heappush(open_set, (0, counter, start))
        counter += 1

        # Convert to set for O(1) lookups
        open_set_hash = {start}

        # Tracking best paths
        came_from = {}
        g_score = {start: 0}
        f_score = {start: heuristic(start, goal)}

        while open_set:
            current_f, _, current = heapq.heappop(open_set)
            open_set_hash.remove(current)

            # If current position is the goal, reconstruct path
            if current == goal:
                path = []
                while current in came_from:
                    path.append(current)
                    current = came_from[current]
                path.append(start)
                path.reverse()
                return path

            # Check all neighbor vertices
            for neighbor in current.neighbours:
                # Calculate movement cost between vertices
                move_cost = heuristic(current, neighbor)

                # Add wall proximity penalty if needed
                if hasattr(self, 'distance_to_wall'):
                    distance_to_wall = neighbor.distance_to_wall
                    penalty = 2.0 - min(1.0, distance_to_wall * 100000)
                    move_cost *= penalty

                tentative_g_score = g_score[current] + move_cost

                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_value = tentative_g_score + heuristic(neighbor, goal)
                    f_score[neighbor] = f_value

                    if neighbor not in open_set_hash:
                        heapq.heappush(open_set, (f_value, counter, neighbor))
                        counter += 1
                        open_set_hash.add(neighbor)

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

    def plot(self, color="blue"):
        # Zeichne die Raumumrisse
        x, y = zip(*self.coordinates) if isinstance(self.coordinates[0], tuple) else ([], [])
        plt.plot(x, y, color=color, alpha=0.5)

        for door in self.doors:
            x, y = door.coordinates
            plt.scatter(x, y, color=color, alpha=0.8)

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

