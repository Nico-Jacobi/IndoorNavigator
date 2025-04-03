from typing import Tuple, List, Any, Dict, TYPE_CHECKING

from graph import Vertex, Graph


class Door:
    def __init__(self, json: Dict[str, Any], graph: Graph):
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

        # cant really import Room as this would result in a circular import
        self.rooms: List["Room"] = []

        self.graph = graph

        self.vertex = Vertex("Door", coordinates[0], coordinates[1],self.level)
        self.graph.add_vertex(self.vertex)

    def __lt__(self, other: "Door") -> bool:
        """Sorts by x-coordinate first, then y-coordinate."""
        return (self.coordinates[0], self.coordinates[1]) < (other.coordinates[0], other.coordinates[1])

    def add_room(self, room):
        self.rooms.append(room)
        self.vertex.add_room(room)

