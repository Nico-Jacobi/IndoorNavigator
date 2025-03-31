import 'dart:convert';
import 'dart:math';
import 'package:flutter/services.dart';

class Vertex {
  int id;
  double lat, lon;
  int floor;
  String name;
  List<Edge> edges = [];

  Vertex({required this.id, required this.lat, required this.lon, required this.floor, required this.name});

  factory Vertex.fromJson(Map<String, dynamic> json) {
    return Vertex(
      id: json['id'],
      lat: json['lat'],
      lon: json['lon'],
      floor: json['floor'],
      name: json['name'] ?? '',
    );
  }

  (double, double) add((double, double) offset) {
    return (lat + offset.$1, lon + offset.$2);
  }

}


class Edge {
  Vertex vertex1;
  Vertex vertex2;
  double length;

  Edge({required this.vertex1, required this.vertex2}): length = sqrt(pow(vertex1.lon - vertex2.lon, 2) + pow(vertex1.lat - vertex2.lat, 2));



  /// uses lon lat format (Which is against ISO 19111, but used in geojson and openindoor)
  List<(double, double)> getOutline({double width = 0.000004}) {
    // Compute direction vector
    double lonDiff = vertex2.lon - vertex1.lon;
    double latDiff = vertex2.lat - vertex1.lat;

    // Calculate the length of the direction vector
    double directionLength = sqrt(lonDiff * lonDiff + latDiff * latDiff);

    // Convert latitude to radians for the cosine calculation
    double latRadians = vertex1.lat * (3.14159265359 / 180.0);

    // Compute orthogonal vector components with latitude correction
    double orthogonalLon = (latDiff/directionLength) * width / cos(latRadians);
    double orthogonalLat = -(lonDiff/directionLength) * width;

    // Define the four corners of the edge outline
    return [
      (vertex1.lon + orthogonalLon, vertex1.lat + orthogonalLat),
      (vertex1.lon - orthogonalLon, vertex1.lat - orthogonalLat),
      (vertex2.lon - orthogonalLon, vertex2.lat - orthogonalLat),
      (vertex2.lon + orthogonalLon, vertex2.lat + orthogonalLat),
    ];
  }

  Map<String, dynamic> toGeoJsonLine({double height = 1.0, String color = "#ff0000", double opacity = 0.6}) {
    List<(double, double)> vertecies = getOutline();

    String id = "Edge ${vertex1.id} -> ${vertex2.id}";

    return {
      "id": id,
      "type": "fill-extrusion",
      "source": {
        "type": "geojson",
        "data": {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  vertecies.map((v) => [v.$1, v.$2]).toList()
                ]
              },
              "properties": {}
            }
          ]
        }
      },
      "paint": {
        "fill-extrusion-color": color,
        "fill-extrusion-height": height,
        "fill-extrusion-opacity": opacity,
      }
    };
  }




}

class Graph {
  Map<int, Vertex> vertices = {};
  List<Edge> edges = [];

  Future<void> loadFromJson(String filePath) async {


    String jsonString = await rootBundle.loadString(filePath);

    Map<String, dynamic> jsonData = jsonDecode(jsonString);
    bool isBidirectional = jsonData['bidirectional'] ?? false;

    for (var v in jsonData['vertices']) {
      Vertex vertex = Vertex.fromJson(v);
      vertices[vertex.id] = vertex;
    }

    for (var e in jsonData['edges']) {
      Vertex v1 = vertices[e[0]]!;
      Vertex v2 = vertices[e[1]]!;
      Edge edge1 = Edge(vertex1: v1, vertex2: v2);
      v1.edges.add(edge1);
      edges.add(edge1);

      if (isBidirectional) {
        Edge edge2 = Edge(vertex1: v2, vertex2: v1);
        v2.edges.add(edge2);
        edges.add(edge2);
      }
    }

  }

  List<Edge> findShortestPathByName(Vertex start, String targetName) {
    Map<Vertex, double> distances = {};
    Map<Vertex, Vertex?> previous = {};
    Set<Vertex> visited = {};
    // Create a priority queue instead of constantly re-sorting
    List<Vertex> unvisited = vertices.values.toList();

    for (var v in vertices.values) {
      distances[v] = double.infinity;
      previous[v] = null;
    }
    distances[start] = 0;

    while (unvisited.isNotEmpty) {
      unvisited.sort((a, b) => distances[a]!.compareTo(distances[b]!));
      Vertex current = unvisited.removeAt(0);

      // If we've reached infinity, there's no path to remaining vertices
      if (distances[current] == double.infinity) {
        throw Exception('No path found from $start to $targetName');
      }

      if (current.name == targetName) {
        List<Edge> pathEdges = [];
        Vertex? currentVertex = current;

        while (previous[currentVertex] != null) {
          Vertex prev = previous[currentVertex]!;
          // Find the edge that connects 'prev' and 'currentVertex'
          Edge? connectingEdge;
          for (Edge edge in prev.edges) {
            if ((edge.vertex1 == currentVertex && edge.vertex2 == prev) ||
                (edge.vertex2 == currentVertex && edge.vertex1 == prev)) {
              connectingEdge = edge;
              break;
            }
          }

          if (connectingEdge == null) {
            throw Exception('Error reconstructing path: no connecting edge found');
          }

          pathEdges.insert(0, connectingEdge);
          currentVertex = prev;
        }

        return pathEdges;
      }

      visited.add(current);
      unvisited.remove(current); // This line was missing but is redundant with removeAt(0)

      for (Edge edge in current.edges) {
        Vertex neighbor = edge.vertex1 == current ? edge.vertex2 : edge.vertex1;
        if (!visited.contains(neighbor)) {
          double newDist = distances[current]! + edge.length;
          if (newDist < distances[neighbor]!) {
            distances[neighbor] = newDist;
            previous[neighbor] = current;
          }
        }
      }
    }

    throw Exception('No path found from $start to $targetName');
  }

}
