from typing import Tuple, List, Any, Dict, Optional
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

        x,y = self.get_center()

        self.graph = graph
        self.vertex = Vertex(self.name, x,y, self.level)
        self.graph.add_vertex(self.vertex)

    def __repr__(self):
        return f"Stair (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"


    def setup_graph(self):
        super().setup_graph()   # setting up the rooms connections

        # connecting stairs to the level above
        # below not needed, as this stair calls this method also
        if not self.above or len(self.doors) == 0 or len(self.above.doors) == 0:
            return

        self.graph.add_edge_bidirectional(self.vertex, self.above.vertex, NavigationPath(0, []))


    @staticmethod
    def link_stairs(stairs: List['Stair']):
        """
        Verknüpft eine Liste von Treppen nach ihrer tatsächlichen Position.

        :param stairs: Liste von Stair-Objekten
        """
        stairs_by_level = {}
        for stair in stairs:
            stairs_by_level.setdefault(stair.level, []).append(stair)

        for stair in stairs:
            center = stair.get_center()

            # Prüfe, ob es eine Treppe auf dem Level darüber gibt, die den Mittelpunkt enthält
            if stair.level + 1 in stairs_by_level:
                for candidate in stairs_by_level[stair.level + 1]:
                    if candidate.is_in_bounding_box(center):
                        stair.above = candidate
                        candidate.below = stair
                        break