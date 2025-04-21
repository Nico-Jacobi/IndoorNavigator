import math
import statistics
from typing import Tuple, List

import numpy as np

from room import Room
from shapely.geometry.polygon import Polygon


class Wavefront:

    # the merge_threshold is 0.2 here, as meter-coordinates are used for the object (0.2 = 20cm) instead of gps.coordinates
    def __init__(self, merge_threshold=0.02):
        self.vertex_normal_pairs: List[Tuple[Tuple[float, float, float], Tuple[float, float, float]]] = []
        self.faces: List[List[int]] = []
        self.merge_threshold: float = merge_threshold


    def _is_close(self, v1: Tuple[float, float, float], v2: Tuple[float, float, float]) -> bool:
        return math.dist(v1, v2) < self.merge_threshold


    def _find_or_add_vertex(self, vertex: Tuple[float, float, float],
                            normal: Tuple[float, float, float]) -> int:
        """Find the index of a vertex or add it if it doesn't exist."""
        if normal is None:
            raise ValueError("Cannot add a vertex without a normal")

        for idx, (v, _) in enumerate(self.vertex_normal_pairs):
            if self._is_close(v, vertex):
                return idx
        self.vertex_normal_pairs.append((vertex, normal))
        return len(self.vertex_normal_pairs) - 1


    def add_face(self, vertex_list: List[Tuple[float, float, float]],
                 normal_list: List[Tuple[float, float, float]]):
        """Adds a face to the model, reusing existing vertices when close enough."""
        # Make sure we have the same number of vertices and normals
        if normal_list is None or len(vertex_list) != len(normal_list):
            raise ValueError("The number of vertices and normals must match, and normals cannot be None")

        indices = [self._find_or_add_vertex(v, n) for v, n in zip(vertex_list, normal_list)]
        self.faces.append(indices)

    def add_face_indices(self, vertex_indices: List[int]):
        """Adds a face to the model, reusing existing vertices when close enough."""
        self.faces.append(vertex_indices)


    def remove_redundant_faces(self):
        """
            Removes overlapping faces that lie along the dimension axis (X, Y, Z).
            Actually not really needed, but nice to have, in retrospect
        """


        def fix_overlaps(faces: List[List[int]], axis_index: int, depth_tolerance=self.merge_threshold) -> List[List[int]]:
            # Sort faces by depth

            len_before = len(faces)

            polygons = []
            depths = []

            for face in faces:
                vertices = [self.vertex_normal_pairs[i][0] for i in face]

                # Project to 2D by removing axis_index
                projected = [tuple(coord for j, coord in enumerate(v) if j != axis_index) for v in vertices]

                # Calculate average of removed axis
                avg_depth = sum(v[axis_index] for v in vertices) / len(vertices)

                polygons.append(Polygon(projected))
                depths.append(avg_depth)

            # sort by depth
            sorted_pairs = sorted(zip(depths, polygons), key=lambda x: x[0])

            if sorted_pairs:
                depths, polygons = zip(*sorted_pairs)
                depths = list(depths)
                polygons = list(polygons)
            else:
                depths = []
                polygons = []

            buffered_polygons = [p.buffer(depth_tolerance, join_style="mitre") for p in polygons]

            to_delete = set()

            for i in range(len(polygons)):
                if i in to_delete:
                    continue


                #remove all contained before face1
                j = i-1
                while j > 0:
                    face2 = polygons[j]

                    orthogonal_distance = abs(depths[i] - depths[j])
                    if orthogonal_distance > depth_tolerance:
                        break

                    if j in to_delete:
                        j -= 1
                        continue

                    if buffered_polygons[i].contains(face2):
                        to_delete.add(j)

                    j -= 1


                #remove all contained behind face1
                j = i+1
                while j < len(faces):
                    face2 = polygons[j]

                    orthogonal_distance = abs(depths[i] - depths[j])
                    if orthogonal_distance > depth_tolerance:
                        break

                    if j in to_delete:
                        j += 1
                        continue

                    if buffered_polygons[i].contains(face2):
                        to_delete.add(j)

                    j += 1


            # Remove completely contained faces
            faces = [face for i, face in enumerate(faces) if i not in to_delete]

            print(f"removed {len_before - len(faces)} unnecessary faces")
            return faces


        print("Removing unnecessary faces...")

        self.faces = [face for face in self.faces if len(set(face)) == len(face)]   #removing all where 2 or more indices are the same

        oriented_x : List[List[int]] = []
        oriented_y : List[List[int]] = []
        oriented_z : List[List[int]] = []
        non_aligned = []

        # For each face, store the original face indices along with the transformed data
        for face_idx, face in enumerate(self.faces):
            orientation = self.get_orientation(face)

            match orientation:
                case "x":
                    oriented_x.append(face)
                case "y":
                    oriented_y.append(face)
                case "z":
                    oriented_z.append(face)
                case _:
                    non_aligned.append(face)

        self.faces = non_aligned

        oriented_x = fix_overlaps(oriented_x, 0)
        oriented_y = fix_overlaps(oriented_y, 1)
        oriented_z = fix_overlaps(oriented_z, 2)

        for face in oriented_x:
            self.add_face_indices(face)

        for face in oriented_y:
            self.add_face_indices(face)

        for face in oriented_z:
            self.add_face_indices(face)


    def get_orientation(self, face1):
        """Returns the orientation axis ('x', 'y', 'z') the face is flattest in, or 'none' if all extents are above threshold."""

        x_values = [self.vertex_normal_pairs[vertex][0][0] for vertex in face1]
        y_values = [self.vertex_normal_pairs[vertex][0][1] for vertex in face1]
        z_values = [self.vertex_normal_pairs[vertex][0][2] for vertex in face1]

        extents = {
            "x": max(x_values) - min(x_values),
            "y": max(y_values) - min(y_values),
            "z": max(z_values) - min(z_values)
        }

        smallest_axis = min(extents, key=extents.get)
        smallest_extent = extents[smallest_axis]

        if smallest_extent < self.merge_threshold:
            return smallest_axis
        else:
            return "none"


    def __str__(self):
        #self.remove_redundant_faces()

        lines = []
        # Write vertices
        for vertex, normal in self.vertex_normal_pairs:
            lines.append(f"v {vertex[0]} {vertex[1]} {vertex[2]}")
            lines.append(f"vn {normal[0]} {normal[1]} {normal[2]}")

        for f in self.faces:
            # OBJ format: f v1//vn1 v2//vn2 v3//vn3
            face_line = "f " + " ".join(f"{idx + 1}//{idx + 1}" for idx in f)
            lines.append(face_line)

        return "\n".join(lines)




@staticmethod
def visualize_shapely_polygon(polygon, figsize=(10, 8), fill_color='lightblue',
                              edge_color='blue', alpha=0.5, title='Shapely Polygon'):
    """
    Visualize a Shapely polygon or MultiPolygon using matplotlib.

    Parameters:
    -----------
    polygon : shapely.geometry.Polygon or shapely.geometry.MultiPolygon
        The Shapely polygon or MultiPolygon to visualize
    figsize : tuple, optional
        Figure size as (width, height)
    fill_color : str or list of str, optional
        Color(s) to fill the polygons (if MultiPolygon, a list of colors is used)
    edge_color : str, optional
        Color for the polygon edge
    alpha : float, optional
        Transparency of the fill (0 to 1)
    title : str, optional
        Title for the plot

    Returns:
    --------
    fig, ax : matplotlib figure and axes objects
    """
    import matplotlib.pyplot as plt
    from matplotlib.patches import Polygon as MplPolygon
    import numpy as np
    from shapely.geometry import MultiPolygon

    # Create figure and axis
    fig, ax = plt.subplots(figsize=figsize)

    # If polygon is a MultiPolygon, plot each individual polygon
    if isinstance(polygon, MultiPolygon):
        # If the fill_color is a single color, use it for all polygons
        if isinstance(fill_color, str):
            fill_color = [fill_color] * len(polygon.geoms)

        for i, poly in enumerate(polygon.geoms):
            # Extract polygon exterior coordinates
            x, y = poly.exterior.xy

            # Create matplotlib polygon patch
            patch = MplPolygon(np.array([x, y]).T,
                               closed=True,
                               facecolor=fill_color[i] if i < len(fill_color) else fill_color[-1],
                               edgecolor=edge_color,
                               alpha=alpha)
            ax.add_patch(patch)

            # If polygon has holes (interior rings)
            for interior in poly.interiors:
                x_int, y_int = interior.xy
                hole_patch = MplPolygon(np.array([x_int, y_int]).T,
                                        closed=True,
                                        facecolor='white',
                                        edgecolor=edge_color,
                                        alpha=alpha)
                ax.add_patch(hole_patch)
    else:
        # Extract polygon exterior coordinates
        x, y = polygon.exterior.xy

        # Create matplotlib polygon patch
        patch = MplPolygon(np.array([x, y]).T,
                           closed=True,
                           facecolor=fill_color,
                           edgecolor=edge_color,
                           alpha=alpha)
        ax.add_patch(patch)

        # If polygon has holes (interior rings)
        for interior in polygon.interiors:
            x_int, y_int = interior.xy
            hole_patch = MplPolygon(np.array([x_int, y_int]).T,
                                    closed=True,
                                    facecolor='white',
                                    edgecolor=edge_color,
                                    alpha=alpha)
            ax.add_patch(hole_patch)

    # Set axis limits with a small margin
    if isinstance(polygon, MultiPolygon):
        minx = min([p.bounds[0] for p in polygon.geoms])
        miny = min([p.bounds[1] for p in polygon.geoms])
        maxx = max([p.bounds[2] for p in polygon.geoms])
        maxy = max([p.bounds[3] for p in polygon.geoms])
    else:
        minx, miny, maxx, maxy = polygon.bounds

    margin = max((maxx - minx), (maxy - miny)) * 0.05
    ax.set_xlim(minx - margin, maxx + margin)
    ax.set_ylim(miny - margin, maxy + margin)

    # Set equal aspect ratio
    ax.set_aspect('equal')

    # Add title and labels
    ax.set_title(title)
    ax.set_xlabel('X')
    ax.set_ylabel('Y')

    # Add grid
    ax.grid(True, linestyle='--', alpha=0.7)

    plt.tight_layout()
    plt.plot()
    plt.show()
    return fig, ax