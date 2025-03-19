from typing import Tuple, List, Any, Dict

from room import Room


class Stair(Room):

    def __init__(self, json: Dict[str, Any]):
        """
        Erstellt ein Stair-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        super().__init__(json)
        self.above: Stair
        self.below: Stair


    def __repr__(self):
        return f"Stair (name={self.name!r}, level={self.level}, bounding_box={self.bounding_box})"
