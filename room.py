from typing import Tuple, List, Any, Dict


class Room:

    def __init__(self, json: Dict[str, Any]):
        """
        Erstellt ein Room-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        properties = json.get("properties", {})
        self.level: int = properties.get("level")
        self.name: str = properties.get("name", "")

        geometry = json.get("geometry", {})
        self.geometry_type: str = geometry.get("type", "")

        self.coordinates: List[Tuple[float, float]] = [
            (float(coord[0]), float(coord[1])) for coord in geometry.get("coordinates", [])
        ]

        self.doors: List[Any] = []


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
