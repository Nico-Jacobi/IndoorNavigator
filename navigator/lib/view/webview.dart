import 'dart:convert';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';

import '../model/graph.dart';

class IndoorWebView extends StatefulWidget {
  const IndoorWebView({super.key});

  @override
  State<IndoorWebView> createState() => _IndoorWebViewState();
}

class _IndoorWebViewState extends State<IndoorWebView> {
  InAppWebViewController? _controller;

  Graph current;
  int zoom = 20;
  double lat = 50.8099433;
  double lon = 8.8107979;
  double rotation = 60;
  double heightAngle = 20;

  // Constructor for _IndoorWebViewState
  _IndoorWebViewState() : current = Graph() {
    current.loadFromJson('lib/resources/graph.json');
  }


  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: InAppWebView(
        initialUrlRequest: URLRequest(
          url: WebUri(getUrl()),
        ),
        onWebViewCreated: (controller) {
          _controller = controller;
        },
      ),
      floatingActionButton: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          FloatingActionButton(
            onPressed: () => _onPressed(),
            child: const Icon(Icons.account_circle_rounded),
          ),
          const SizedBox(height: 10), // Abstand zwischen den Buttons
          FloatingActionButton(
            onPressed: () => _removeLayer(),
            child: const Icon(Icons.settings),
          ),
        ],
      ),
    );
  }


  String getUrl() {
    return 'https://www.informatik.uni-marburg.de/indoor/index.html?source=H04&umr_tr=03%2FA12#'
        '$zoom/'
        '$lat/'
        '$lon/'
        '$rotation/'
        '$heightAngle';
  }


  void _plotEdgesOnSingleLayer(List<Edge> edges, {String color = "#ff0000"}) {
    if (_controller == null) return;

    String layerId = "all-edges-layer";
    String sourceId = "all-edges-source";

    // First, ensure we remove any existing layer and source
    _removeLayerAndSource(layerId, sourceId);

    // Create a single GeoJSON layer with multiple features
    Map<String, dynamic> geoJson = {
      "id": layerId,
      "type": "fill-extrusion",
      "source": sourceId,
      "paint": {
        "fill-extrusion-color": color,
        "fill-extrusion-height": 1.0,
        "fill-extrusion-opacity": 0.6,
      }
    };

    // Collect all features for the GeoJSON source
    List<Map<String, dynamic>> features = [];
    for (Edge edge in edges) {
      List<(double, double)> vertices = edge.getOutline();

      Map<String, dynamic> feature = {
        "type": "Feature",
        "geometry": {
          "type": "Polygon",
          "coordinates": [
            vertices.map((v) => [v.$1, v.$2]).toList()
          ]
        },
        "properties": {
          "from": edge.vertex1.id,
          "to": edge.vertex2.id
        }
      };

      features.add(feature);
    }


    // Add the source first, then the layer that references it
    String jsCode = '''
    setTimeout(function() {
      if (typeof my_openindoor !== 'undefined' && my_openindoor.map_) {
        // Add the source
        my_openindoor.map_.addSource("$sourceId", {
          "type": "geojson",
          "data": {
            "type": "FeatureCollection",
            "features": ${json.encode(features)}
          }
        });
        
        // Then add the layer that references the source
        my_openindoor.map_.addLayer(${json.encode(geoJson)});
      } else {
        console.error("my_openindoor is not defined.");
      }
    }, 0);
  ''';

    _controller!.evaluateJavascript(source: jsCode);
  }

  void _plotVerticesOnSingleLayer(List<Vertex> vertices, {String color = "#0000ff"}) {
    if (_controller == null) return;

    String layerId = "all-vertices-layer";
    String sourceId = "all-vertices-source";

    // First, ensure we remove any existing layer and source
    _removeLayerAndSource(layerId, sourceId);

    // Create a single GeoJSON layer with multiple features
    Map<String, dynamic> geoJson = {
      "id": layerId,
      "type": "fill-extrusion",
      "source": sourceId,
      "paint": {
        "fill-extrusion-color": color,
        "fill-extrusion-height": 0.5,
        "fill-extrusion-opacity": 0.8,
      }
    };

    // Collect all features for the GeoJSON source
    List<Map<String, dynamic>> features = [];
    for (Vertex vertex in vertices) {
      // Create a circle approximation with points
      List<List<double>> circleCoordinates = [];
      int segments = 16;
      double radius = 0.000008;

      for (int i = 0; i <= segments; i++) {
        double angle = 2 * 3.14159265359 * i / segments;
        // Adjust longitude based on latitude to account for Earth's curvature
        double latRadians = vertex.lat * (3.14159265359 / 180.0);
        double lonOffset = radius * cos(angle) / cos(latRadians);
        double latOffset = radius * sin(angle);

        circleCoordinates.add([vertex.lon + lonOffset, vertex.lat + latOffset]);
      }

      Map<String, dynamic> feature = {
        "type": "Feature",
        "geometry": {
          "type": "Polygon",
          "coordinates": [circleCoordinates]
        },
        "properties": {
          "id": vertex.id,
          "name": vertex.name,
          "floor": vertex.floor
        }
      };

      features.add(feature);
    }


    // Add the source first, then the layer that references it
    String jsCode = '''
  setTimeout(function() {
    if (typeof my_openindoor !== 'undefined' && my_openindoor.map_) {
      // Add the source
      my_openindoor.map_.addSource("$sourceId", {
        "type": "geojson",
        "data": {
          "type": "FeatureCollection",
          "features": ${json.encode(features)}
        }
      });
      
      // Then add the layer that references the source
      my_openindoor.map_.addLayer(${json.encode(geoJson)});
    } else {
      console.error("my_openindoor is not defined.");
    }
  }, 0);
''';

    _controller!.evaluateJavascript(source: jsCode);
  }

  void _removeLayerAndSource(String layerId, String sourceId) {
    if (_controller == null) return;

    String jsCode = '''
    setTimeout(function() {
      if (typeof my_openindoor !== 'undefined' && my_openindoor.map_) {
        // Check if the layer exists before removing
        if (my_openindoor.map_.getLayer("$layerId")) {
          my_openindoor.map_.removeLayer("$layerId");
        }
        
        // Check if the source exists before removing
        if (my_openindoor.map_.getSource("$sourceId")) {
          my_openindoor.map_.removeSource("$sourceId");
        }
      } else {
        console.error("my_openindoor is not defined.");
      }
    }, 0);
  ''';

    _controller!.evaluateJavascript(source: jsCode);
  }

// Update the existing removeLayer method to also remove the source
  void _removeLayer() {
    print("removed Layer");
    if (_controller == null) return;

    _removeLayerAndSource( "all-edges-layer", "all-edges-source");
    _removeLayerAndSource( "all-vertices-layer", "all-vertices-source");
  }

// Updated onPressed method to use the single layer function
  void _onPressed() {
    Vertex? startVert = current.vertices[127];

    print(startVert?.name);
    print(startVert?.floor);
    print(startVert?.rooms);


    List<Edge> path = current.findShortestPathByName(startVert!, "Büro (04B09a)"); //current.findShortestPathByName(startVert!, goalName!);

    print(path);
    List<Vertex> vertsToDisplayPath = [];
    List<Edge> edgesToDisplay = [];

    for (Edge edge in path) {

        edgesToDisplay.add(edge);
        vertsToDisplayPath.add(edge.vertex1);
        //todo problem: orthogonal is not really orthogonal as latitude and longitude is stretched
    }
    vertsToDisplayPath.add(path.last.vertex2);


    _plotVerticesOnSingleLayer(vertsToDisplayPath);
    _plotEdgesOnSingleLayer(edgesToDisplay);
  }


}
