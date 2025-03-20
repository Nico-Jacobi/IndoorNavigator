from typing import Dict, List, Set


class Vertex:
    def __init__(self, name: str, x: float, y: float):
        self.name = name
        self.x = x
        self.y = y
        self.edges: Set[Edge] = set()

    def add_edge(self, edge: "Edge"):
        self.edges.add(edge)

    def __repr__(self):
        return f"Vertex({self.name}, x={self.x}, y={self.y})"


class Edge:
    def __init__(self, vertex1: Vertex, vertex2: Vertex):
        self.vertex1 = vertex1
        self.vertex2 = vertex2
        vertex1.add_edge(self)
        vertex2.add_edge(self)

    def __repr__(self):
        return f"Edge({self.vertex1.name}, {self.vertex2.name})"
