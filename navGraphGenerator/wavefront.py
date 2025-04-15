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

    def remove_redundant_faces(self):
        """Cleans the object of overlapping faces"""

        print("Removing unnecessary faces")

        oriented_x = {}
        oriented_y = {}
        oriented_z = {}

        # For each face, store the original face indices along with the transformed data
        for face_idx, face in enumerate(self.faces):
            orientation = self.get_orientation(face)
            face_vertices = [self.vertices[i] for i in face]  # Get the actual vertices

            match orientation:
                case "x":
                    x_value = face_vertices[0][0]  # Get x from first point
                    poly = Polygon([(y, z) for x, y, z in face_vertices])
                    oriented_x[face_idx] = (poly, poly.area, x_value, face)  # Store original face indices
                case "y":
                    y_value = face_vertices[0][1]  # Get y from first point
                    poly = Polygon([(x, z) for x, y, z in face_vertices])
                    oriented_y[face_idx] = (poly, poly.area, y_value, face)  # Store original face indices
                case "z":
                    z_value = face_vertices[0][2]  # Get z from first point
                    poly = Polygon([(x, y) for x, y, z in face_vertices])
                    oriented_z[face_idx] = (poly, poly.area, z_value, face)  # Store original face indices

        def remove_redundant_from_dict(face_dict):
            to_remove = set()
            items = list(face_dict.items())
            for i in range(len(items)):
                face1_idx, (poly1, area1, height1, _) = items[i]
                if face1_idx in to_remove:  # Skip if already marked for removal
                    continue

                for j in range(len(items)):
                    if i == j:
                        continue
                    face2_idx, (poly2, area2, height2, _) = items[j]

                    # Only consider if heights are similar
                    if abs(height1 - height2) < self.merge_threshold:
                        # Case 1: If areas are significantly different, remove the smaller one
                        if area1 < area2 and poly2.contains(poly1):
                            to_remove.add(face1_idx)
                            break  # Once marked for removal, no need to check other faces
                        # Case 2: If areas are equal (or very close), keep the first one we encounter
                        elif abs(area1 - area2) < 1e-6 and poly2.contains(poly1) and poly1.contains(poly2):
                            # Arbitrarily keep face1 and remove face2
                            to_remove.add(face2_idx)

            for face_idx in to_remove:
                del face_dict[face_idx]

        remove_redundant_from_dict(oriented_x)
        remove_redundant_from_dict(oriented_y)
        remove_redundant_from_dict(oriented_z)

        # Rebuild self.faces with the remaining faces
        # We preserve the original face structure (lists of vertex indices)
        remaining_faces = []
        for face_dict in [oriented_x, oriented_y, oriented_z]:
            for _, (_, _, _, original_face) in face_dict.items():
                remaining_faces.append(original_face)

        self.faces = remaining_faces

    def get_orientation(self, face1):
        """returns the orientation of a given face as string"""
        x_values = [self.vertices[vertex][0] for vertex in face1]
        if (max(x_values) - min(x_values)) < self.merge_threshold:
            return "x"

        y_values = [self.vertices[vertex][1] for vertex in face1]
        if (max(y_values) - min(y_values)) < self.merge_threshold:
            return "y"

        z_values = [self.vertices[vertex][2] for vertex in face1]
        if (max(z_values) - min(z_values)) < self.merge_threshold:
            return "z"

        return "none"


    def __str__(self):
        self.remove_redundant_faces()
        lines = []
        for v in self.vertices:
            lines.append(f"v {v[0]} {v[1]} {v[2]}")
        for f in self.faces:
            lines.append("f " + " ".join(str(idx + 1) for idx in f))  # OBJ format uses 1-based indices
        return "\n".join(lines)

