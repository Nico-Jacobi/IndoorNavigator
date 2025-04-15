import math
from typing import Tuple, List

from shapely.geometry.polygon import Polygon


class Wavefront:

    # the merge_threshold is 0.2 here, as meter-coordinates are used for the object (0.2 = 20cm) instead of gps.coordinates
    def __init__(self, merge_threshold=0.05):
        self.vertices: List[Tuple[float, float, float]] = []
        self.faces: List[List[int]] = []
        self.merge_threshold: float = merge_threshold

    def _is_close(self, v1: Tuple[float, float, float], v2: Tuple[float, float, float]) -> bool:
        return math.dist(v1, v2) < self.merge_threshold

    def _find_or_add_vertex(self, vertex: Tuple[float, float, float]) -> int:
        """Find the index of a vertex or add it if it doesn't exist."""
        for idx, v in enumerate(self.vertices):
            if self._is_close(v, vertex):
                return idx
        self.vertices.append(vertex)
        return len(self.vertices) - 1

    def add_face(self, vertex_list: List[Tuple[float, float, float]]):
        """Adds a face to the model, reusing existing vertices when close enough."""
        indices = [self._find_or_add_vertex(v) for v in vertex_list]
        self.faces.append(indices)


    def __str__(self):
        lines = []
        for v in self.vertices:
            lines.append(f"v {v[0]} {v[1]} {v[2]}")
        for f in self.faces:
            lines.append("f " + " ".join(str(idx + 1) for idx in f))  # OBJ format uses 1-based indices
        return "\n".join(lines)

