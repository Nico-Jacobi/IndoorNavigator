from __future__ import annotations

import json
from typing import Set, Dict, Any


class Vertex:
    all_vertices: Set[Vertex] = set()

    def __init__(self, name: str, x: float, y: float, floor: int):
        self.name: str = name
        self.rooms: Set["Room"] = set()
        self.x: float = x
        self.y: float = y
        self.floor = floor
        self.edges: Set[Edge] = set()
        self.neighbours: Set[Vertex] = set()    # these are the vertices next to it in the grid, not necessarily connected by an edge

        Vertex.all_vertices.add(self)

    def add_edge_bidirectional(self, vertex: Vertex, weight: float = float('inf')) -> None:
        self.edges.add(Edge(self, vertex, weight))
        vertex.edges.add(Edge(vertex, self, weight))    #dont use the method, this would be circular

    def remove_edge_bidirectional(self, vertex: Vertex) -> None:
        e1 = Edge(self, vertex)
        e2 = Edge(vertex, self)

        self.edges.remove(e1)
        vertex.edges.remove(e2)

        Edge.all_edges.remove(e1)
        Edge.all_edges.remove(e2)


    def distance_to(self, other: Vertex) -> float:
        return ((self.x - other.x) ** 2 + (self.y - other.y) ** 2) ** 0.5

    def to_json(self) -> Dict[str, Any]:
        return {"lat": self.y, "lon": self.x, "floor": self.floor, "name": self.name, "rooms": [room.name for room in self.rooms]}

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Vertex):
            return NotImplemented
        return self.name == other.name and self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return hash((self.name, self.x, self.y))

    def __repr__(self) -> str:
        return f"Vertex({self.name}, x={self.x}, y={self.y})"

    def __lt__(self, other: Vertex) -> bool:
        """Sorts by x-coordinate first, then y-coordinate."""
        return (self.x, self.y) < (other.x, other.y)


    def add_room(self, room):
        self.floor = room.level
        self.rooms.add(room.name)


class Edge:
    all_edges: Set["Edge"] = set()

    def __init__(self, vertex1: "Vertex", vertex2: "Vertex", weight: float = float('inf')):
        self.vertex1: "Vertex" = vertex1
        self.vertex2: "Vertex" = vertex2
        self.weight: float = weight
        Edge.all_edges.add(self)

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Edge):
            return NotImplemented
        return {self.vertex1, self.vertex2} == {other.vertex1, other.vertex2}

    def __hash__(self) -> int:
        return hash(frozenset((self.vertex1, self.vertex2)))

    def __repr__(self) -> str:
        return f"Edge({self.vertex1.name}, {self.vertex2.name}, weight={self.weight})"

    def to_json(self) -> Dict[str, Any]:
        return {"vertex1": self.vertex1.name, "vertex2": self.vertex2.name, "weight": self.weight}



def export_json(filter_bidirectional: bool = True) -> str:
    # Set aller Knoten, die in einer Kante vorkommen
    connected_vertices = {e.vertex1 for e in Edge.all_edges} | {e.vertex2 for e in Edge.all_edges}

    # Nummeriere nur die Knoten, die in einer Kante vorkommen
    vertex_map = {v: i for i, v in enumerate(connected_vertices)}

    # Filtere Knoten ohne Kanten
    # lat lon bei geojson "vertauscht" zu standard, daher hier so herum
    vertices_list = [
        {"id": vertex_map[v], "lat": v.y, "lon": v.x, "floor": v.floor, "name": v.name, "rooms": [r.name for r in v.rooms]}
        for v in connected_vertices
    ]

    if filter_bidirectional:
        seen_edges = set()
        edges_list = []
        for e in Edge.all_edges:
            edge_key = tuple(sorted([vertex_map[e.vertex1], vertex_map[e.vertex2]]))
            if edge_key not in seen_edges:
                seen_edges.add(edge_key)
                edges_list.append(list(edge_key))
    else:
        edges_list = [
            {"vertex1": vertex_map[e.vertex1], "vertex2": vertex_map[e.vertex2]}
            for e in Edge.all_edges
        ]

    return json.dumps({"bidirectional": filter_bidirectional, "vertices": vertices_list, "edges": edges_list})
