from typing import Tuple, List, Any, Dict


class Door:
    def __init__(self, json: Dict[str, Any]):
        """
        Erstellt ein Room-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        properties = json.get("properties", {})
        if not properties.get("door", "") == "yes":
            raise ValueError("non-door data passed to door constructor")

        self.level: int = properties.get("level")

        geometry = json.get("geometry", {})
        self.geometry_type: str = geometry.get("type", "")
        coordinates = geometry.get("coordinates", [])
        self.coordinates = (float(coordinates[0]), float(coordinates[1]))

        self.rooms: List[Any] = []
