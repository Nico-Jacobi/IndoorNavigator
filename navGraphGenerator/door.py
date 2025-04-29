from typing import Tuple, List, Any, Dict, TYPE_CHECKING
from graph import Vertex, Graph


class Door:

    def __init__(self, json: Dict[str, Any], graph: Graph):

        properties = json.get("properties", {})
        if not properties.get("door", "") == "yes":
            raise ValueError("non-door data passed to door constructor")

        self.level: int =  int(properties.get("level"))

        geometry = json.get("geometry", {})
        self.geometry_type: str = geometry.get("type", "")
        coordinates = geometry.get("coordinates", [])
        self.coordinates = (float(coordinates[1]), float(coordinates[0]))

        # cant really import Room as this would result in a circular import
        self.rooms: List["Room"] = []

        self.graph = graph

        self.vertex = Vertex("Door", self.coordinates[0], self.coordinates[1],self.level)
        self.graph.add_vertex(self.vertex)


    # just to be able to sort the doors
    def __lt__(self, other: "Door") -> bool:
        """Sorts by x-coordinate first, then y-coordinate."""
        return (self.coordinates[0], self.coordinates[1]) < (other.coordinates[0], other.coordinates[1])


    def add_room(self, room):
        """adds a room to the door"""
        self.rooms.append(room)
        self.vertex.add_room(room)

        # this happens if walls are overlapping (shouldn't happen)
        # or if the tolerance for linking doors to walls is too big
        if len(self.rooms) > 2:
            print(f"Warning: {self.vertex.name} has more than 2 rooms linked to it.")
            print([r.name for r in self.rooms])

