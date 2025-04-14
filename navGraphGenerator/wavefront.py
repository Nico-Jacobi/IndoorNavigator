import math
from typing import Tuple, List

from room import Room


class Wavefront:
    def __init__(self, merge_threshold=Room.wall_thickness[0]/2):
        self.vertices: List[Tuple[float, float]] = []
        self.faces: List[List[int]] = []
        self.merge_threshold: float = merge_threshold

    def _is_close(self, v1, v2):
        return math.dist(v1, v2) < self.merge_threshold

    def _add_vertex(self, vertex):
        """add the vertex"""
        self.vertices.append(vertex)

    def add_face(self, vertex_list: List[Tuple[float, float, float]]):
        """Takes a list of (x, y, z) tuples and adds a face by referring to vertex indices."""
        indices: List[int] = []  # Use a tuple instead of a list
        for v in vertex_list:
            idx = self._find_or_add_vertex(v)
            indices .append(idx)
        self.faces.append(indices)

    def _find_or_add_vertex(self, vertex) -> int:
        """Find the index of a vertex or add it if it doesn't exist."""
        for idx, v in enumerate(self.vertices):
            if self._is_close(v, vertex):
                return idx
        self._add_vertex(vertex)
        return len(self.vertices) - 1

    def __str__(self):
        lines = []
        for v in self.vertices:
            lines.append(f"v {v[0]} {v[1]} {v[2]}")
        for f in self.faces:
            lines.append("f " + " ".join(str(idx + 1) for idx in f))  # Obj format is 1-based index
        return "\n".join(lines)


    def merge_vertices(self):
        """Merge vertices that are close to each other."""
        pass
