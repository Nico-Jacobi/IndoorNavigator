import 'dart:convert';
import 'dart:io';

import 'package:flutter/services.dart';

class Vertex {
  int id;
  double x, y;
  int floor;
  String name;
  List<Edge> edges = [];

  Vertex({required this.id, required this.x, required this.y, required this.floor, required this.name});

  factory Vertex.fromJson(Map<String, dynamic> json) {
    return Vertex(
      id: json['id'],
      x: json['x'],
      y: json['y'],
      floor: json['floor'],
      name: json['name'] ?? '',
    );
  }
}

class Edge {
  Vertex vertex1;
  Vertex vertex2;

  Edge({required this.vertex1, required this.vertex2});
}

class Graph {
  Map<int, Vertex> vertices = {};
  List<Edge> edges = [];

  Future<void> loadFromJson(String filePath) async {
    String jsonString = await rootBundle.loadString(filePath);
    Map<String, dynamic> jsonData = jsonDecode(jsonString);
    bool isBidirectional = jsonData['bidirectional'] ?? false;

    // Parse vertices
    for (var v in jsonData['vertices']) {
      Vertex vertex = Vertex.fromJson(v);
      vertices[vertex.id] = vertex;
    }

    // Parse edges
    for (var e in jsonData['edges']) {
      Vertex v1 = vertices[e[0]]!;
      Vertex v2 = vertices[e[1]]!;
      Edge edge1 = Edge(vertex1: v1, vertex2: v2);
      v1.edges.add(edge1);
      edges.add(edge1);

      // If bidirectional, add the reverse edge also
      if (isBidirectional) {
        Edge edge2 = Edge(vertex1: v2, vertex2: v1);
        v2.edges.add(edge2);
        edges.add(edge2);
      }
    }
  }
}
