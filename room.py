import heapq
import math
from typing import Tuple, List, Any, Dict
from door import Door
from graph import Vertex, Edge


class Room:
    # Definiere die Größe eines Gitterschritts (abhängig von den verwendeten GPS-Koordinaten)
    grid_size_x = 0.00002
    grid_size_y = 0.00002

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
        self.graph = {}

        # assuming most rooms are rectangular precomputing this is more efficient to use in is_walkable()
        # (compared to the ray-cast, but this cant be used in all cases)
        self.bounding_box = self._compute_bounding_box()

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

        size = max(max_x - min_x, max_y - min_y)

        return (min_x, min_y), (min_x + size, min_y + size)

    def get_center(self) -> Tuple[float, float]:
        """
        Berechnet den Mittelpunkt der Bounding Box.
        """
        (min_x, min_y), (max_x, max_y) = self.bounding_box
        return (min_x + max_x) / 2, (min_y + max_y) / 2


    def is_point_on_outline(self, point: Tuple[float, float], tolerance: float = 0.00001) -> bool:
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
        self.graph = {}

        for i, start in enumerate(self.doors):
            self.graph[start] = {}

            for j, goal in enumerate(self.doors):
                if goal not in self.graph:
                    self.graph[goal] = {}

                if i < j:  # Nur einmal berechnen, wenn i < j (vermeidet doppelte Berechnung)
                    path = self._a_star_pathfinding(start, goal)
                    if path:
                        # todo fix, doesent work as this makes pickeling imposible due to shared data across whole graph
                        # Connect vertices in the path with bidirectional edges
                        #for v in range(len(path) - 1):  # Stop at the second-to-last element
                        #    path[v].add_edge(Edge(path[v], path[v + 1]))
                        #    path[v + 1].add_edge(Edge(path[v + 1], path[v]))

                        # add the path to the path-map for the room
                        self.graph[start][goal] = path
                        self.graph[goal][start] = path[::-1]  # Einfach umkehren

    def _a_star_pathfinding(self, start: Door, goal: Door) -> List[Vertex]:
        """
        Führt A* auf einem Raster aus, um einen Weg zwischen zwei Türen zu finden.
        Wandelt gps coordinated in grid-koordinaten um, wo gridsize = 1

        :param start: Die Start-Tür.
        :param goal: Die Ziel-Tür.
        :return: Eine Liste von Vertex-Objekten für den gefundenen Pfad.
        """

        # todo test different distance approach and its implications for the graph
        def heuristic(a: Tuple[int, int], b: Tuple[int, int]) -> float:
            """
            Berechnet die heuristische Distanz zwischen zwei Punkten.
            Hier wird die Euklidische Distanz verwendet, weil wir auch diagonale Bewegungen zulassen.
            """
            return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2)  # Euklidische Distanz

        # Wandle die echten Koordinaten in Raster-Koordinaten um
        start_grid = (int(start.coordinates[0] / Room.grid_size_x), int(start.coordinates[1] / Room.grid_size_y))
        goal_grid = (int(goal.coordinates[0] / Room.grid_size_x), int(goal.coordinates[1] / Room.grid_size_y))

        # Priority Queue für A* (beste Position zuerst)
        open_set = []
        heapq.heappush(open_set, (0, start_grid))

        # Speichert die Rückverfolgbarkeit für den besten Pfad
        came_from = {}

        # Speichert die aktuellen besten bekannten Kosten von Start -> Knoten
        g_score = {start_grid: 0}

        # Speichert die geschätzten Kosten von Start -> Ziel über den aktuellen Knoten
        f_score = {start_grid: heuristic(start_grid, goal_grid)}

        while open_set:
            _, current = heapq.heappop(open_set)  # Knoten mit den geringsten f-Kosten nehmen

            # Wenn das Ziel erreicht wurde, rekonstruiere den Pfad
            if current == goal_grid:
                path_grid = []
                while current in came_from:
                    path_grid.append(current)
                    current = came_from[current]
                path_grid.append(start_grid)
                path_grid.reverse()

                # Konvertiere den Pfad von Grid-Koordinaten zu Vertex-Objekten
                path_vertices = []
                for i, grid_pos in enumerate(path_grid):
                    # Konvertiere Grid-Koordinaten zurück zu echten Koordinaten
                    real_x = grid_pos[0] * Room.grid_size_x
                    real_y = grid_pos[1] * Room.grid_size_y

                    # Verwende die Vertices der Türen für Start und Ziel
                    if i == 0:
                        path_vertices.append(start.vertex)
                    elif i == len(path_grid) - 1:
                        path_vertices.append(goal.vertex)
                    else:
                        # Für Zwischenpunkte erstelle neue Vertex-Objekte mit Platzhalternamen
                        path_vertices.append(Vertex(self.name, real_x, real_y))

                return path_vertices  # Rückgabe des rekonstruierten Pfads als Vertex-Objekte

            # Nachbarn definieren (horizontale/vertikale und diagonale Bewegungen)
            neighbors = [
                (current[0] + 1, current[1]),  # rechts
                (current[0] - 1, current[1]),  # links
                (current[0], current[1] + 1),  # oben
                (current[0], current[1] - 1),  # unten
                (current[0] + 1, current[1] + 1),  # rechts oben
                (current[0] + 1, current[1] - 1),  # rechts unten
                (current[0] - 1, current[1] + 1),  # links oben
                (current[0] - 1, current[1] - 1)  # links unten
            ]

            for neighbor in neighbors:
                if not self.is_walkable((neighbor[0] * Room.grid_size_x, neighbor[1] * Room.grid_size_y)):
                    continue  # Skip this neighbor if it's not walkable

                # Berechne die Kosten für die Bewegung (1 für horizontal/vertikal, √2 für diagonal)
                is_diagonal = neighbor[0] != current[0] and neighbor[1] != current[1]
                move_cost = 1.414 if is_diagonal else 1  # quicker than actually calculating it

                tentative_g_score = g_score[current] + move_cost

                # Falls ein besserer Weg zum Nachbarn gefunden wurde, aktualisiere die Werte
                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_score[neighbor] = tentative_g_score + heuristic(neighbor, goal_grid)
                    heapq.heappush(open_set, (f_score[neighbor], neighbor))  # In Warteschlange einfügen

        return []  # Kein Pfad gefunden oder leere Liste zurückgeben, wenn kein Pfad gefunden wurde

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
