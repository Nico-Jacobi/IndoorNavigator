from typing import Tuple, List, Any, Dict


class Stair:

    def __init__(self, json: Dict[str, Any]):
        """
        Erstellt ein Stair-Objekt.

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
