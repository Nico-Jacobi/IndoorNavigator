import math
import re
from typing import List, Any, Dict, Optional
from graph import Vertex, Graph
from dataClasses import NavigationPath
from room import Room


class Stair(Room):

    def __init__(self, json: Dict[str, Any],graph: Graph):
        """
        Erstellt ein Stair-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        super().__init__(json, graph)
        self.above: Optional[Stair] = None
        self.below: Optional[Stair] = None

        x,y = self.bounding_box.get_center()

        # each stair has its own vertex, for linking it to the stairs above and below
        self.vertex = Vertex(self.name, x, y, self.level)
        self.graph.add_vertex(self.vertex)


    def __repr__(self):
        return f"Stair (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"


    @staticmethod
    def link_stairs(stairs: List['Stair']):
        """
        Connects above and below stairs by checking if their bounding boxes overlap. + name comparison
        """
        for stair1 in stairs:
            for stair2 in stairs:

                # Prüfe, ob es eine Treppe auf dem Level darüber gibt, deren Bounding Box überlappt
                if stair1.level +1 == stair2.level:

                    # this isnt very nice, but the geojson isnt aligned very well sometimes
                    # and in this case bounding_box1.overlaps(bounding_box2) is not enough
                    center1 = stair1.bounding_box.get_center()  # (x1, y1)
                    center2 = stair2.bounding_box.get_center()  # (x2, y2)

                    # Calculate the Euclidean distance
                    distance = math.sqrt((center2[0] - center1[0]) ** 2 + (center2[1] - center1[1]) ** 2)

                    if distance/2 < max(stair1.bounding_box.diagonal_length(), stair2.bounding_box.diagonal_length())\
                            or Stair.link_stairs_by_namen(stair1, stair2):   # for inaccuracies in the geojson

                        stair1.above = stair2
                        stair2.below = stair1
                        break

        print("linked stairs")


    @staticmethod
    def link_stairs_by_namen(stair1, stair2) -> bool:
        """
        Checks if two stairs can be linked by their names. (eg "Stairs (03A99)" and "Stairs (04A99)")
        This is a fallback if the bounding boxes are not overlapping, as the geojson sometimes has inaccuracies.
        """

        def extract_floor(tag) -> tuple[int, str] | None:
            match = re.search(r'\((\d+)(.*)\)', tag)
            if match:
                floor = int(match.group(1))
                remaining_text = match.group(2).strip()
                return floor, remaining_text
            return None  # Return None if the regex does not match

        # Extract floor and room number
        result1 = extract_floor(stair1.name)
        result2 = extract_floor(stair2.name)

        # Ensure both extractions are successful (not None)
        if result1 and result2:
            stair1_floor, stair1_tag = result1
            stair2_floor, stair2_tag = result2

            # Check the conditions for linking the stairs
            if  stair1_tag == stair2_tag and stair1_floor + 1 == stair2_floor:

                print("linked stairs by name", stair1.name, "and", stair2.name)
                return True
        return False



    def _setup_paths(self):
        """
        Connects stairs vertices to the level above and below. + standard room connections
        """

        super()._setup_paths()  # setting up the rooms connections

        # connecting stairs to the level above
        # below not needed, as this stair calls this method also
        for door in self.doors:
            self.graph.add_edge_bidirectional(self.vertex, door.vertex, NavigationPath(0.0, []))

        # connect the vertices
        if self.above and len(self.doors) != 0 and len(self.above.doors) != 0:
            self.graph.add_edge_bidirectional(self.vertex, self.above.vertex, NavigationPath(0.0, []))