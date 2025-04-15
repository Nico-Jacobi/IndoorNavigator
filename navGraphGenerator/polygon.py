import math
from typing import List, Tuple, Optional, Set
import networkx as nx
from dataclasses import dataclass
from typing import Set
from shapely.geometry.point import Point
from shapely.geometry.polygon import Polygon
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import Polygon as MplPolygon
from matplotlib.collections import PatchCollection
from wavefront import Wavefront


class Polygon2D:
    """
    A 2D polygon class with support for holes and triangulation.

    Attributes:
        vertices (List[Tuple[float, float]]): List of (x, y) vertex coordinates defining the outer boundary.
        holes (List[List[Tuple[float, float]]]): List of holes, where each hole is a list of (x, y) vertex coordinates.
    """

    #todo add function to split, to split big vertecies in the to triangles method, to run faster as it is n2 (or even n3?)
    def __init__(self, vertices: List[Tuple[float, float]], holes: Optional[List[List[Tuple[float, float]]]] = None):
        """
        Initialize a polygon with vertices and optional holes.

        Args:
            vertices: List of (x, y) coordinate pairs defining the outer boundary.
            holes: Optional list of holes, where each hole is a list of (x, y) coordinate pairs.
        """
        if len(vertices) < 3:
            raise ValueError("A polygon needs at least 3 vertecies")

        self.vertices = vertices
        self.holes = holes if holes else []
        self.polygon = Polygon(vertices, holes=self.holes)

        # Validate that vertices form a closed loop
        if len(vertices) < 3:
            raise ValueError("A polygon must have at least 3 vertices")

        # Validate each hole
        for hole in self.holes:
            if len(hole) < 3:
                raise ValueError("Each hole must have at least 3 vertices")


    @classmethod
    def from_shapely(cls, shapely_poly: Polygon) -> 'Polygon2D':
        """
        Create a PolygonWrapper from a shapely Polygon.

        Args:
            shapely_poly: A shapely.geometry.Polygon object.

        Returns:
            A PolygonWrapper instance.
        """

        # duplicates have to be removes as the geojson is shitty sometimes and has double vertices, carrying over to the shapely polygon
        def remove_duplicates(seq, threshold=0.01):
            seen = []
            result = []
            for item in seq:
                # Check if the distance between item and any seen item is below the threshold
                if not any(math.dist(item, seen_item) < threshold for seen_item in seen):
                    seen.append(item)
                    result.append(item)
            return result

        vertices = remove_duplicates(list(shapely_poly.exterior.coords)[:-1])
        holes = [remove_duplicates(list(interior.coords)[:-1]) for interior in shapely_poly.interiors]
        return cls(vertices, holes)

    def __str__(self) -> str:
        """String representation of the polygon."""
        result = f"Polygon with {len(self.vertices)} vertices"
        if self.holes:
            result += f" and {len(self.holes)} holes"
        return result

    def __repr__(self) -> str:
        """Detailed representation of the polygon."""
        return f"Polygon2D(vertices={self.vertices}, holes={self.holes})"


    def to_wavefront(self, wavefront_to_add: Wavefront) -> None:
        """triangulates the face and adds it to the given wavefront"""

        print(f"adding {self} to wavefront")

        if not self.holes and len(self.vertices) == 4:
            face = [(self.vertices[0][0], 0.0, self.vertices[0][1]),
                    (self.vertices[1][0], 0.0, self.vertices[1][1]),
                    (self.vertices[2][0], 0.0, self.vertices[2][1]),
                    (self.vertices[3][0], 0.0, self.vertices[3][1])]
            wavefront_to_add.add_face(face)
            return


        vertices_points = []
        for vertex in self.vertices:
            vertices_points.append(PolygonVertex(vertex[0], vertex[1]))

        for i in range(len(vertices_points)):
            vertices_points[i].link(vertices_points[(i + 1) % len(vertices_points)])


        # link all holes
        for hole in self.holes:
            vertices_hole_points = []

            for vertex in hole:
                vertices_hole_points.append(PolygonVertex(vertex[0], vertex[1]))

            for i in range(len(vertices_hole_points)):
                next_vert = vertices_hole_points[(i + 1) % len(vertices_hole_points)]
                vertices_hole_points[i].link(next_vert)

            vertices_points.extend(vertices_hole_points)


        # now all vertices (including the holes to the outside) should be linked, and triangulation can start
        already_linked = set()

        # do this in a random order (for nicer geometry)
        for v1 in vertices_points:
            for v2 in vertices_points:
                if v2 in already_linked:
                    continue

                if v1 != v2 and not v2 in v1.neighbours and not self.would_cause_intersection(v1, v2, vertices_points):
                    v1.link(v2)

            already_linked.add(v1)

        #visualize_polygon_vertices(vertices_points)

        faces = self.find_faces(vertices_points)
        for face in faces:
            face.to_obj(wavefront_to_add)

        return


    def find_faces(self, vertices: List["PolygonVertex"]) -> List["Triangle"]:
        """assumes the faces are all triangular"""
        def calculate_angle(center, point):
            """
            Calculate the angle in radians between the positive x-axis and the line connecting center to point.

            Args:
                center (tuple): A tuple of two floats (x, y) representing the center point
                point (tuple): A tuple of two floats (x, y) representing the target point

            Returns:
                float: The angle in radians, in the range [-π, π]
            """
            import math

            # Extract coordinates
            center_x, center_y = center
            point_x, point_y = point

            # Calculate the differences
            dx = point_x - center_x
            dy = point_y - center_y

            # Calculate the angle using atan2
            angle = math.atan2(dy, dx)

            return angle

        faces: List[Triangle] = []

        for vertex in vertices:
            current_neighbours: List[(PolygonVertex, float)] = []

            for neighbour in vertex.neighbours:
                current_neighbours.append((neighbour, calculate_angle((vertex.lat, vertex.lon), (neighbour.lat, neighbour.lon))))

            current_neighbours.sort(key=lambda x: x[1])

            for i in range(len(current_neighbours) -1):
                v1 = current_neighbours[i]
                v2 = current_neighbours[i+1]

                # if angle bigger than 180 degrees, it cant be a valid triangle  (either we already got it, or its outside)
                if abs(v1[1] - v2[1]) > math.pi:
                    continue

                faces.append(Triangle(v1[0], vertex, v2[0]))

            # also add the one from last to first
            if current_neighbours:
                if abs(current_neighbours[-1][1] - current_neighbours[0][1]) > math.pi:
                    continue
                #faces.append(Triangle(current_neighbours[-1][0], vertex, current_neighbours[0][0]))

            vertex.remove_all_links()

        #plot_triangles(faces)

        faces.sort(key=lambda x : x.area())

        final_faces: List[Triangle] = []
        invalid_face = set()
        for i, face in enumerate(faces):
            if face in invalid_face:
                continue

            # if it is inside the geometry the next smallest hast to be part of it
            if self.polygon.covers(Point(face.center())):
                final_faces.append(face)


        return final_faces

    @staticmethod
    def find_closest_point(point: "PolygonVertex", points: List["PolygonVertex"]) -> "PolygonVertex":
        """
        Find the closest PolygonVertex in the list to the given PolygonVertex.

        Args:
            point: The reference PolygonVertex.
            points: List of PolygonVertex objects to search.

        Returns:
            The closest PolygonVertex from the list.
        """
        return min(points, key=lambda p: (p.lat - point.lat) ** 2 + (p.lon - point.lon) ** 2)


    def would_cause_intersection(self, v1: "PolygonVertex", v2: "PolygonVertex", all_vertices: List["PolygonVertex"]) -> bool:
        """
        Check whether adding an edge between v1 and v2 would intersect existing edges.

        Args:
            v1: One end of the proposed connection.
            v2: The other end of the proposed connection.
            all_vertices: All PolygonVertex instances forming the polygon and holes.

        Returns:
            True if the connection would intersect an existing connection, False otherwise.
        """
        # just a rough check, that isn't needed, but good for performance + looks nicer
        if not self.polygon.contains(Point((v1.lat + v2.lat)/2, (v1.lon + v2.lon)/2)):
            return True

        def segments_intersect(p1, p2, q1, q2):
            def ccw(a, b, c):
                return (c.lon - a.lon) * (b.lat - a.lat) > (b.lon - a.lon) * (c.lat - a.lat)

            return (
                ccw(p1, q1, q2) != ccw(p2, q1, q2)
                and ccw(p1, p2, q1) != ccw(p1, p2, q2)
            )

        for vertex in all_vertices:
            for neighbour in vertex.neighbours:
                # Skip if the edge shares a vertex with the proposed one
                if vertex in (v1, v2) or neighbour in (v1, v2):
                    continue

                if segments_intersect(v1, v2, vertex, neighbour):
                    return True

        return False




@dataclass
class PolygonVertex:
    def __init__(
        self,
        lat: float,
        lon: float,
        neighbours: Set["PolygonVertex"] = None,
    ):
        self.lat = lat
        self.lon = lon
        self.neighbours = neighbours if neighbours is not None else set()

    def __repr__(self):
        return f"PolygonVertex(lat={self.lat}, lon={self.lon}, neighbours={len(self.neighbours)})"

    def __eq__(self, other):
        if not isinstance(other, PolygonVertex):
            return False
        return (self.lat == other.lat) and (self.lon == other.lon)

    def __hash__(self):
        return hash((self.lat, self.lon))

    def angle_to(self, other: "PolygonVertex") -> float:
        dx = other.lon - self.lon
        dy = other.lat - self.lat
        return math.atan2(dy, dx)


    def link(self, other: "PolygonVertex"):
        """
        Link this vertex to another vertex.
        """
        if other == self:
            raise ValueError("cant be linked to itsef")

        if other not in self.neighbours:
            self.neighbours.add(other)
        if self not in other.neighbours:
            other.neighbours.add(self)


    def unlink(self, other: "PolygonVertex"):
        other.neighbours.remove(self)
        self.neighbours.remove(other)

    def remove_all_links(self):
        for neighbour in list(self.neighbours):  # make a copy
            self.unlink(neighbour)



@dataclass
class Triangle:
    a: PolygonVertex
    b: PolygonVertex
    c: PolygonVertex

    def __init__(self, a: PolygonVertex, b: PolygonVertex, c: PolygonVertex):
        self.a = a
        self.b = b
        self.c = c
        self.polygon = Polygon(self.outline())

    def __hash__(self, a=None):
        return hash(frozenset((self.a, self.b, self.c)))    # reihenfolge egal

    def area(self) -> float:
        """
        Calculate the area using the Shoelace formula.
        """
        x1, y1 = self.a.lon, self.a.lat
        x2, y2 = self.b.lon, self.b.lat
        x3, y3 = self.c.lon, self.c.lat
        return abs((x1*(y2 - y3) + x2*(y3 - y1) + x3*(y1 - y2)) / 2)

    def contains_vertex(self, vertex: PolygonVertex) -> bool:
        return vertex == self.a or vertex == self.b or vertex == self.c

    def edges(self) -> Tuple[Tuple[PolygonVertex, PolygonVertex], ...]:
        return (
            (self.a, self.b),
            (self.b, self.c),
            (self.c, self.a),
        )

    def center(self) -> (float, float):
        lat = (self.a.lat + self.b.lat + self.c.lat) / 3
        lon = (self.a.lon + self.b.lon + self.c.lon) / 3
        return lat, lon

    def outline(self) -> List[Tuple[float, float]]:
        return [(self.a.lat, self.a.lon), (self.b.lat, self.b.lon), (self.c.lat, self.c.lon)]

    def to_obj(self, wavefront_to_add: Wavefront) -> None:
        """triangulates the face and adds it to the given wavefront"""

        face = [(self.a.lat, 0.0, self.a.lon),
                (self.b.lat, 0.0, self.b.lon),
                (self.c.lat, 0.0, self.c.lon)]
        wavefront_to_add.add_face(face)
        return


def visualize_polygon_vertices(vertices: list[PolygonVertex], title: str = "Polygon Vertices and Connections",
                               figsize=(10, 8), node_size=300, node_color='skyblue',
                               edge_color='gray', with_labels=True):
    """
    Visualize a list of PolygonVertex objects and their connections.

    Parameters:
    - vertices: List of PolygonVertex objects
    - title: Title of the plot
    - figsize: Figure size as tuple (width, height)
    - node_size: Size of the vertex nodes in the plot
    - node_color: Color of the nodes
    - edge_color: Color of the edges
    - with_labels: Whether to show vertex indices as labels

    Returns:
    - matplotlib figure and axis objects
    """
    # Create a graph
    G = nx.Graph()

    # Add vertices to the graph
    for i, vertex in enumerate(vertices):
        G.add_node(i, pos=(vertex.lon, vertex.lat))

    # Add edges based on neighbour relationships
    for i, vertex in enumerate(vertices):
        for neighbour in vertex.neighbours:
            # Find the index of the neighbour in the vertices list
            try:
                j = vertices.index(neighbour)
                G.add_edge(i, j)
            except ValueError:
                print(f"Warning: Neighbour not found in vertex list")

    # Get positions for all nodes
    pos = nx.get_node_attributes(G, 'pos')

    # Create figure and plot
    fig, ax = plt.subplots(figsize=figsize)

    # Draw the graph
    nx.draw(G, pos, with_labels=with_labels, node_size=node_size,
            node_color=node_color, edge_color=edge_color, ax=ax,
            font_weight='bold', font_size=10)

    # Set title and adjust layout
    plt.title(title)
    plt.tight_layout()
    plt.show()
    return fig, ax


@staticmethod
def plot_triangles(triangles: List['Triangle'], figsize=(10, 8), show_vertices=True,
                   triangle_alpha=0.5, vertex_size=20):
    """
    Plot a list of triangles using matplotlib with flipped axes (longitude -> X, latitude -> Y).

    Args:
        triangles: List of Triangle objects to plot
        figsize: Figure size as a tuple (width, height)
        show_vertices: Whether to show vertex points
        triangle_alpha: Transparency of triangle fills
        vertex_size: Size of vertex points if shown
    """


    fig, ax = plt.subplots(figsize=figsize)

    # Use different colors for each triangle
    colors = plt.cm.viridis(np.linspace(0, 1, len(triangles)))
    patches = []

    all_vertices = set()

    for i, triangle in enumerate(triangles):
        # Get (lat, lon) coordinates, but flip them: lat -> Y, lon -> X
        vertices = [(v.lon, v.lat) for v in [triangle.a, triangle.b, triangle.c]]
        polygon = MplPolygon(vertices, closed=True, alpha=triangle_alpha,
                             edgecolor='black', facecolor=colors[i])
        patches.append(polygon)

        all_vertices.update([triangle.a, triangle.b, triangle.c])

        # Add center label with flipped coordinates
        center_lon, center_lat = triangle.center()
        ax.text(center_lat, center_lon, f"{i}", ha='center', va='center')

    # Add all triangle patches
    collection = PatchCollection(patches, match_original=True)
    ax.add_collection(collection)

    if show_vertices:
        # Plot vertices: lon -> X, lat -> Y
        lons = [v.lon for v in all_vertices]
        lats = [v.lat for v in all_vertices]
        ax.scatter(lons, lats, s=vertex_size, c='red', zorder=5)
        for v in all_vertices:
            ax.text(v.lon, v.lat, f"({v.lon:.2f}, {v.lat:.2f})",
                    fontsize=8, ha='right', va='bottom')

    # Set correct limits: lon = X-axis, lat = Y-axis
    if all_vertices:
        lons = [v.lon for v in all_vertices]
        lats = [v.lat for v in all_vertices]

        lon_min, lon_max = min(lons), max(lons)
        lat_min, lat_max = min(lats), max(lats)

        lon_margin = (lon_max - lon_min) * 0.05 or 1
        lat_margin = (lat_max - lat_min) * 0.05 or 1

        ax.set_xlim(lon_min - lon_margin, lon_max + lon_margin)  # lon -> X
        ax.set_ylim(lat_min - lat_margin, lat_max + lat_margin)  # lat -> Y

    ax.set_xlabel('Longitude')
    ax.set_ylabel('Latitude')
    ax.set_title(f'Plot of {len(triangles)} Triangles')
    ax.set_aspect('equal')  # Keep scale consistent

    plt.tight_layout()
    plt.show()
    return fig, ax

