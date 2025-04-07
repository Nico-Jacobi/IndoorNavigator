from typing import List, Any, Dict, Optional
from graph import Vertex, Graph, NavigationPath
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

        self.graph = graph
        self.vertex = Vertex(self.name, x,y, self.level)
        self.graph.add_vertex(self.vertex)

    def __repr__(self):
        return f"Stair (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"


    def setup_paths(self):
        super().setup_paths()   # setting up the rooms connections

        print("setting up stairs graph...")
        # connecting stairs to the level above
        # below not needed, as this stair calls this method also
        if not self.above or len(self.doors) == 0 or len(self.above.doors) == 0:
            return

        for door in self.doors:
            self.graph.add_edge_bidirectional(self.vertex, door.vertex, NavigationPath(0.0, []))

        self.graph.add_edge_bidirectional(self.vertex, self.above.vertex, NavigationPath(0.0, []))

    @staticmethod
    def link_stairs(stairs: List['Stair']):
        """
        Verknüpft eine Liste von Treppen nach ihrer tatsächlichen Position.

        :param stairs: Liste von Stair-Objekten
        """
        for stair1 in stairs:
            for stair2 in stairs:

                # Prüfe, ob es eine Treppe auf dem Level darüber gibt, deren Bounding Box überlappt
                if stair1.level +1 == stair2.level:
                    if stair1.bounding_box.overlaps_with(stair2.bounding_box):
                        stair1.above = stair2
                        stair2.below = stair1
                        break
