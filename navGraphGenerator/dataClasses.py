from __future__ import annotations
import math
from dataclasses import dataclass
from typing import Tuple, List


@dataclass
class PathVertex:
    # used for pathfinding the visual path from door to door in each room
    x: float
    y: float
    x_index: int
    y_index: int
    distance_to_wall: float # this is saved here, as it would otherwise be calculated multiple times (and that's an expensive one)

    def get_coordinates(self) -> Tuple[float, float]:
        """Get the (x, y) coordinates as a tuple"""
        return self.x, self.y

    def get_indices(self) -> Tuple[int, int]:
        """Get the grid indices as a tuple"""
        return self.y_index, self.x_index

    def distance(self, other: "PathVertex") -> float:
        """Calculate euclidian the distance to another PathVertex"""
        return math.sqrt((self.x - other.x) ** 2 + (self.y - other.y) ** 2)


@dataclass
class BoundingBox:
    # simple bounding box for rooms (to save some calculations, and not use the expensive room._is_inside method)
    #(min_x, min_y), (max_x, max_y)
    min_x: float
    min_y: float
    max_x: float
    max_y: float

    def get_center(self) -> Tuple[float, float]:
        """
        Calculates the center of the bounding box.
        """
        return (self.min_x + self.max_x) / 2, (self.min_y + self.max_y) / 2


    def is_inside(self, point_gps_pos: Tuple[float, float]) -> bool:
        """
        Checks if a given point is inside the bounding box.
        """
        x, y = point_gps_pos
        return self.min_x <= x <= self.max_x and self.min_y <= y <= self.max_y

    def diagonal_length(self) -> float:
        """
        Calculate the diagonal length of the bounding box.
        """
        return math.sqrt((self.max_x - self.min_x) ** 2 + (self.max_y - self.min_y) ** 2)


@dataclass
class NavigationPath:
    weight: float
    points: List[Tuple[float, float]]

    def __init__(self, weight: float, points: List[Tuple[float, float]]):
        self.weight = weight
        self.points = points

    def flip(self) -> "NavigationPath":
        """creates a new NavigationPath object with the points in reverse order."""
        flipped_points = list(reversed(self.points))
        return NavigationPath(weight=self.weight, points=flipped_points)


    def to_json(self) -> dict:
        """Convert the NavigationPath object to a dictionary."""
        return {
            "weight": self.weight,
            "points": [(lat,lon) for lon,lat in self.points]        # as geojson, which is not convention
        }
