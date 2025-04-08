from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Set, Dict, Any, Optional, List, Tuple


class Vertex:
    def __init__(self, name: str, x: float, y: float, floor: int):
        self.name: str = name
        self.rooms: List[str] = []
        self.x: float = x
        self.y: float = y
        self.floor: int = floor
        self.edges: Set[Edge] = set()
        self.neighbours: Set[Vertex] = set()

    def add_edge(self, vertex: Vertex, path: NavigationPath) -> Edge:
        """Create an edge from this vertex to another vertex"""
        edge = Edge(self, vertex, path)
        self.edges.add(edge)
        return edge

    def distance_to(self, other: Vertex) -> float:
        """Calculate Euclidean distance between two vertices"""
        return ((self.x - other.x) ** 2 + (self.y - other.y) ** 2) ** 0.5

    def to_json(self, vertex_id=None) -> Dict[str, Any]:
        """Convert vertex to JSON serializable dictionary"""
        if vertex_id is not None:
            return {
                "id": vertex_id,
                "lat": self.y,
                "lon": self.x,
                "floor": self.floor,
                "name": self.name,
                "rooms": self.rooms
            }
        else:
            return {
                "lat": self.y,
                "lon": self.x,
                "floor": self.floor,
                "name": self.name,
                "rooms": self.rooms
            }

    def add_room(self, room: "Room") -> None:
        """Add a room to this vertex"""
        self.rooms.append(room.name)

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Vertex):
            return NotImplemented
        return self.name == other.name and self.x == other.x and self.y == other.y and self.floor == other.floor

    def __hash__(self) -> int:
        return hash((self.x, self.y, self.floor))

    def __repr__(self) -> str:
        return f"Vertex({self.name}, x={self.x}, y={self.y}, floor={self.floor})"

    def __lt__(self, other: Vertex) -> bool:
        """Sorts by x-coordinate first, then y-coordinate."""
        return (self.x, self.y, self.floor) < (other.x, other.y, other.floor)

@dataclass
class NavigationPath:
    weight: float
    points: List[Tuple[float, float]]

    def __init__(self, weight: float, points: List[Tuple[float, float]]):
        # Flip each point (swap x and y)
        self.weight = weight
        self.points = [(y, x) for x, y in points]

    def flip(self) -> "NavigationPath":
        # Create a flipped version of the path by reversing the points
        flipped_points = list(reversed(self.points))
        return NavigationPath(weight=self.weight, points=flipped_points)

    def to_json(self, low_res=False) -> dict:
        """Convert the NavigationPath object to a dictionary."""

        #this will greatly reduce the file size, but ultimately is not needed
        def truncate_decimal(value: float) -> int:
            """Truncate the decimal values between the 2nd and 5th decimal place and return as an integer."""
            value_str = str(value)
            if '.' in value_str:
                decimal_part = value_str.split('.')[1][:5]  # Capture the 2nd to 5th digits after the decimal
                truncated_value = decimal_part.ljust(5, '0')  # Add zeros if there are fewer than 4 digits
                return int(truncated_value)
            return 0  # In case there is no decimal point, return 0

        if low_res:
            points = [(truncate_decimal(x), truncate_decimal(y)) for x, y in self.points]
        else:
            points = self.points

        return {
            "weight": self.weight,
            "points": points
        }


class Edge:
    def __init__(self, vertex1: Vertex, vertex2: Vertex, path: NavigationPath):
        self.vertex1: Vertex = vertex1
        self.vertex2: Vertex = vertex2
        self.navigation_path: NavigationPath = path

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Edge):
            return NotImplemented
        return {self.vertex1, self.vertex2} == {other.vertex1, other.vertex2}

    def __hash__(self) -> int:
        return hash(frozenset((self.vertex1, self.vertex2)))

    def __repr__(self) -> str:
        return f"Edge({self.vertex1.name}, {self.vertex2.name}, weight={self.navigation_path.weight})"

    def to_json(self, vert1_id=None, ver2_id=None) -> dict:
        # Convert the Edge object to a dictionary
        if vert1_id is not None and ver2_id is not None:
            return {
                "v1": vert1_id,
                "v2": ver2_id,
                "path": self.navigation_path.to_json()
            }
        else:
            return {
                "vertex1": self.vertex1.to_json(),
                "vertex2": self.vertex2.to_json(),
                "path": self.navigation_path.to_json()
            }


class Graph:
    def __init__(self):
        self.vertices: Dict[Tuple[float, float, int], Vertex] = {}
        self.edges: Set[Edge] = set()

    def add_vertex(self, vertex: Vertex) -> Vertex:
        """Add a vertex to the graph"""
        # Store vertices using (x, y, floor) tuple as the key
        self.vertices[(vertex.x, vertex.y, vertex.floor)] = vertex
        return vertex

    def get_vertex(self, x: float, y: float, floor: int) -> Optional[Vertex]:
        """Get vertex by position and floor"""
        return self.vertices.get((x, y, floor))

    def add_edge_bidirectional(self, vertex1: Vertex, vertex2: Vertex, path: NavigationPath) -> None:
        """Add a bidirectional edge between two vertices"""
        edge1 = vertex1.add_edge(vertex2, path)
        edge2 = vertex2.add_edge(vertex1, path.flip())
        self.edges.add(edge1)
        self.edges.add(edge2)
        vertex1.neighbours.add(vertex2)
        vertex2.neighbours.add(vertex1)

    def remove_edge_bidirectional(self, vertex1: Vertex, vertex2: Vertex) -> None:
        """Remove bidirectional edges between two vertices"""
        edges_to_remove = []
        for edge in self.edges:
            if {edge.vertex1, edge.vertex2} == {vertex1, vertex2}:
                edges_to_remove.append(edge)

        for edge in edges_to_remove:
            if edge in self.edges:
                self.edges.remove(edge)

            if edge.vertex1 == vertex1 and edge in vertex1.edges:
                vertex1.edges.remove(edge)
            elif edge.vertex1 == vertex2 and edge in vertex2.edges:
                vertex2.edges.remove(edge)

        if vertex2 in vertex1.neighbours:
            vertex1.neighbours.remove(vertex2)
        if vertex1 in vertex2.neighbours:
            vertex2.neighbours.remove(vertex1)

    def export_json(self, filter_bidirectional: bool = True) -> str:
        """Export the graph as a JSON string"""

        # Helper function to remove reverse edges
        def remove_reverse_edges(edges: Set[Edge]) -> Set[Edge]:
            seen = set()  # Set to keep track of seen edges
            unique_edges = set()

            for edge in edges:
                # Create a frozenset of the vertices to handle both directions equivalently
                edge_tuple = frozenset([edge.vertex1, edge.vertex2])

                if edge_tuple not in seen:
                    seen.add(edge_tuple)
                    unique_edges.add(edge)

            return unique_edges

        edges = list(self.edges)
        if filter_bidirectional:
            edges = remove_reverse_edges(self.edges)

        # Use indices for vertices in the JSON output
        vertices_list = []
        for index, vertex in enumerate(self.vertices.values()):
            vertices_list.append(vertex.to_json(index))

        # Use indices for edges in the JSON output
        edges_list = []
        for edge in edges:
            # Look up vertex indices in the vertices list
            # this can be done more efficiently (maybe by storing the index in the vertex object? but that's not very nice)
            vertex1_index = list(self.vertices.values()).index(edge.vertex1)
            vertex2_index = list(self.vertices.values()).index(edge.vertex2)
            edges_list.append(edge.to_json(vertex1_index, vertex2_index))

        # Construct and return JSON
        return json.dumps({
            "bidirectional": filter_bidirectional,
            "vertices": vertices_list,
            "edges": edges_list
        })

    def get_disconnected_vertices(self) -> List[Vertex]:
        """
        Returns a list of vertices that have no connections to any other vertices.

        Returns:
            List[Vertex]: List of vertices with no connections
        """
        disconnected = []
        for vertex in self.vertices.values():
            if len(vertex.neighbours) == 0:
                disconnected.append(vertex)
        return disconnected

    def get_disconnected_components(self) -> List[Set[Vertex]]:
        """
        Returns a list of disconnected components in the graph.
        Each component is a set of vertices that are connected to each other,
        but not to vertices in other components.

        Returns:
            List[Set[Vertex]]: List of disconnected components
        """
        # Set of all unvisited vertices
        unvisited = set(self.vertices.values())
        components = []

        # Keep exploring until we've visited all vertices
        while unvisited:
            # Start a new component with an arbitrary unvisited vertex
            start_vertex = next(iter(unvisited))
            component = set()

            # Use BFS to find all vertices in this component
            queue = [start_vertex]
            visited = {start_vertex}

            while queue:
                current = queue.pop(0)
                component.add(current)

                for neighbor in current.neighbours:
                    if neighbor not in visited:
                        visited.add(neighbor)
                        queue.append(neighbor)

            # Add the component to our list and remove its vertices from unvisited
            components.append(component)
            unvisited -= component

        return components

    def print_disconnected_info(self) -> None:
        """
        Prints information about disconnected vertices and components in the graph.
        """
        # Check for isolated vertices
        isolated_vertices = self.get_disconnected_vertices()

        print("=== Graph Connectivity Analysis ===")

        # Print isolated vertices
        if isolated_vertices:
            print(f"\nFound {len(isolated_vertices)} isolated vertices:")
            for i, vertex in enumerate(isolated_vertices, 1):
                print(f"  {i}. Vertex at position ({vertex.x}, {vertex.y}, floor {vertex.floor}, {vertex.rooms})")
        else:
            print("\nNo isolated vertices found.")

        # Print disconnected components
        components = self.get_disconnected_components()

        if len(components) > 1:
            print(f"\nFound {len(components)} disconnected components:")
            for i, component in enumerate(components, 1):
                print(f"  Component {i}: {len(component)} vertices")
                # Print a few vertices from each component as examples
                examples = list(component)[:3]  # Show up to 3 vertices as examples
                print(f"    Examples: " + ", ".join([f"({v.x}, {v.y}, floor {v.floor}, {v.rooms})" for v in examples]))
                if len(component) > 3:
                    print(f"    ... and {len(component) - 3} more vertices")
        else:
            print("\nThe graph is fully connected (1 component).")

        print("\n=== End of Analysis ===")



    def keep_largest_component(self) -> None:
        """
        Modifies the graph to keep only the largest connected component,
        removing all other vertices and edges.

        Returns:
            None: Modifies the graph in-place
        """
        # Find all connected components
        components = self.get_disconnected_components()

        # If there are no components or only one component, nothing to do
        if len(components) <= 1:
            print("Graph already consists of a single component. No changes made.")
            return

        # Find the largest component
        largest_component = max(components, key=len)

        # Identify vertices to remove (all vertices not in the largest component)
        vertices_to_remove = []
        for position, vertex in self.vertices.items():
            if vertex not in largest_component:
                vertices_to_remove.append(position)

        # Remove edges first (to avoid issues with missing vertices)
        edges_to_remove = []
        for edge in self.edges:
            if edge.vertex1 not in largest_component or edge.vertex2 not in largest_component:
                edges_to_remove.append(edge)

        for edge in edges_to_remove:
            self.edges.remove(edge)

            # Also remove the edge from the vertex edge lists
            if edge in edge.vertex1.edges:
                edge.vertex1.edges.remove(edge)
            if edge in edge.vertex2.edges:
                edge.vertex2.edges.remove(edge)

        # Now remove vertices
        for position in vertices_to_remove:
            del self.vertices[position]

        # Update the neighbors lists for all remaining vertices
        for vertex in self.vertices.values():
            vertex.neighbours = {n for n in vertex.neighbours if n in largest_component}

        print(
            f"Kept largest component with {len(largest_component)} vertices. Removed {len(vertices_to_remove)} vertices and {len(edges_to_remove)} edges.")