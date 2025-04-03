import 'dart:convert';
import 'dart:math';
import 'package:flutter/services.dart';
import 'dart:collection' show PriorityQueue, HeapPriorityQueue;

class Vertex {
  int id;
  double lat, lon;
  int floor;
  String name;
  List<Edge> edges = [];
  List<String> rooms = [];

  Vertex({required this.id, required this.lat, required this.lon, required this.floor, required this.name, required this.rooms});

  factory Vertex.fromJson(Map<String, dynamic> json) {
    return Vertex(
      id: json['id'],
      lat: json['lat'],
      lon: json['lon'],
      floor: json['floor'],
      name: json['name'] ?? '',
      rooms: List<String>.from(json["rooms"] ?? []),
    );
  }


  // todo delete
  Map<String, dynamic> toGeoJsonPoint({
    double radius = 0.000008,
    double height = 1.5,
    String color = "#0000ff",
    double opacity = 0.8
  }) {
    String jsn_id = "Vertex $id";

    // Create a simple circle approximation with points
    List<List<double>> circleCoordinates = [];
    int segments = 16;

    for (int i = 0; i <= segments; i++) {
      double angle = 2 * 3.14159265359 * i / segments;
      // Adjust longitude based on latitude to account for Earth's curvature
      double latRadians = lat * (3.14159265359 / 180.0);
      double lonOffset = radius * cos(angle) / cos(latRadians);
      double latOffset = radius * sin(angle);

      circleCoordinates.add([lon + lonOffset, lat + latOffset]);
    }

    return {
      "id": jsn_id,
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
                "coordinates": [circleCoordinates]
              },
              "properties": {
                "name": name,
                "floor": floor
              }
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


class Point {
  final double lat;
  final double lon;

  const Point(this.lat, this.lon);

  factory Point.fromJson(List<dynamic> json) {
    return Point(json[0] as double, json[1] as double);
  }
}

class NavigationPath {
  final double weight;
  final Iterable<Point> _points; // Store the iterable

  const NavigationPath({
    required this.weight,
    required Iterable<Point> points, // Accepts an iterable
  }) : _points = points;


  // Method to return a new NavigationPath with reversed points
  NavigationPath reverseCopy() {
    return NavigationPath(
      weight: weight, // Keep the same weight
      points: _points.toList().reversed, // Reverse the points iterable
    );
  }



}

class Edge {
  Vertex vertex1;
  Vertex vertex2;
  NavigationPath navigationPath; //if this doesn't exist, the reverse edge has one

  Edge({required this.vertex1, required this.vertex2, required this.navigationPath});

  Edge reverseCopy(){
    return Edge(vertex1: vertex2, vertex2: vertex1, navigationPath: navigationPath.reverseCopy());
  }

  /// uses lon lat format (Which is against ISO 19111, but used in geojson and openindoor)
  List<(double, double)> getOutline({double width = 0.000001}) {
    // Get all points from navigation path
    List<Point> pathPoints = navigationPath._points.toList();

    pathPoints = [Point(vertex1.lat, vertex1.lon), ...pathPoints, Point(vertex2.lat, vertex2.lon)];


    List<(double, double)> rightSide = [];
    List<(double, double)> leftSide = [];

    // Process each segment of the path
    for (int i = 0; i < pathPoints.length - 1; i++) {
      Point current = pathPoints[i];
      Point next = pathPoints[i + 1];

      // Compute direction vector for this segment
      double lonDiff = next.lon - current.lon;
      double latDiff = next.lat - current.lat;

      // Calculate the length of the direction vector
      double directionLength = sqrt(lonDiff * lonDiff + latDiff * latDiff);

      // Skip if points are identical (zero length)
      if (directionLength < 1e-10) continue;

      // Normalize the direction vector
      double normLonDiff = lonDiff / directionLength;
      double normLatDiff = latDiff / directionLength;

      // Convert latitude to radians for the cosine calculation
      double latRadians = current.lat * (3.14159265359 / 180.0);

      // Compute orthogonal vector components with latitude correction
      double orthogonalLon = normLatDiff * width / cos(latRadians);
      double orthogonalLat = -normLonDiff * width;

      // Add points to right and left side lists
      rightSide.add((current.lon + orthogonalLon, current.lat + orthogonalLat));
      leftSide.add((current.lon - orthogonalLon, current.lat - orthogonalLat));

      // If this is the last segment, add the endpoint on both sides
      if (i == pathPoints.length - 2) {
        rightSide.add((next.lon + orthogonalLon, next.lat + orthogonalLat));
        leftSide.add((next.lon - orthogonalLon, next.lat - orthogonalLat));
      }
    }

    // Combine the sides to form a closed polygon (right side + reversed left side)
    List<(double, double)> outline = [...rightSide, ...leftSide.reversed.toList()];
    return outline;
  }

  Map<String, dynamic> toGeoJsonLine({double height = 1.0, String color = "#ff0000", double opacity = 0.6}) {
    List<(double, double)> vertices = getOutline();

    // Close the polygon by adding the first point at the end
    if (vertices.isNotEmpty) {
      vertices.add(vertices.first);
    }

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
                  vertices.map((v) => [v.$1, v.$2]).toList()
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

    for (var json_edge in jsonData['edges']) {
      Vertex v1 = vertices[json_edge["v1"]]!;
      Vertex v2 = vertices[json_edge["v2"]]!;

      Map<String, dynamic> jsonPath = json_edge["path"];
      Iterable<Point> points = (jsonPath['points'] as List).map((p) => Point.fromJson(p as List<dynamic>));
      NavigationPath path = NavigationPath(weight: jsonPath["weight"] as double, points: points);

      Edge edge1 = Edge(vertex1: v1, vertex2: v2, navigationPath: path);
      v1.edges.add(edge1);
      edges.add(edge1);

      if (isBidirectional) {
        Edge edge2 = edge1.reverseCopy();
        v2.edges.add(edge2);
        edges.add(edge2);
      }
    }

  }

  List<Edge> findShortestPathByName(Vertex start, String targetName) {
    Map<Vertex, double> distances = {};
    Map<Vertex, Vertex?> previous = {};
    Map<Vertex, Edge?> connectingEdges = {}; // Track edges for path reconstruction
    Set<Vertex> visited = {};

    // Initialize distances
    for (var v in vertices.values) {
      distances[v] = double.infinity;
      previous[v] = null;
      connectingEdges[v] = null;
    }
    distances[start] = 0;

    while (true) {
      Vertex? current;
      double minDistance = double.infinity;

      for (var v in vertices.values) {
        if (!visited.contains(v) && distances[v]! < minDistance) {
          current = v;
          minDistance = distances[v]!;
        }
      }

      //no vertex found
      if (current == null) {
        print('No path found from ${start.name} to $targetName');
        throw Exception('No path found from $start to $targetName');
      }

      print('Selected vertex ${current.name} with distance $minDistance');


      if (current.rooms.contains(targetName)) {
        List<Edge> pathEdges = [];
        Vertex? currentVertex = current;

        while (previous[currentVertex] != null) {
          Vertex prevVertex = previous[currentVertex]!;
          // Find the connecting edge between current and previous
          Edge? edge = connectingEdges[currentVertex];

          if (edge == null) {
            print('Error reconstructing path: no connecting edge found');
            throw Exception(
                'Error reconstructing path: no connecting edge found');
          }

          pathEdges.insert(0, edge);
          currentVertex = prevVertex;
        }

        print('Path reconstruction complete');


        List<Edge> cleanedEdges = [];
        // clean poath of vertices that can be skipped
        for (int i = 0; i < pathEdges.length - 1; i++) {
          Vertex v1 = pathEdges[i].vertex1;
          //Vertex v2 = pathEdges[i].vertex2;
          Vertex v3 = pathEdges[i + 1].vertex2;

          bool foundAlternative = false;
          for (String r in v3.rooms) {
            if (v1.rooms.contains(r)) {
              // found a vertex that's not needed (can happen, as distance is not a perfect indicator for natural ways)
              for (Edge e in v1.edges) {
                if (e.vertex2 == v3) {
                  // found alternative e
                  cleanedEdges.add(e);
                  foundAlternative=true;
                  i++;
                  break;
                }
              }
            }
          }

          if (!foundAlternative) {
            // first edge is not replaceable
            cleanedEdges.add(pathEdges[i]);

            if (i == pathEdges.length - 1){
              cleanedEdges.add(pathEdges[i+1]);
            }
          }
        }
        return cleanedEdges;
      }

      // Mark as visited
      visited.add(current);

      // If we've reached infinity distance, there's no path to remaining vertices
      if (distances[current] == double.infinity) {
        print('No path found from ${start.name} to $targetName (infinity reached)');
        throw Exception('No path found from $start to $targetName');
      }

      // Update distances to neighbors
      for (Edge edge in current.edges) {
        if (visited.contains(edge.vertex2)) {
          continue;
        }

        double newDist = distances[current]! + edge.navigationPath.weight;
        if (newDist < distances[edge.vertex2]!) {
          distances[edge.vertex2] = newDist;
          previous[edge.vertex2] = current;
          connectingEdges[edge.vertex2] = edge; // Store the edge directly
        }
      }
    }
  }
}
