from typing import Tuple, List, Any, Dict

from room import Room


class Stair(Room):

    def __init__(self, json: Dict[str, Any]):
        """
        Erstellt ein Stair-Objekt.

        :param json: Das JSON-Objekt, das die Raumdaten enthält.
        """
        super().__init__(json)
