import 'dart:convert';
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
  String? _currentLayerId;

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

    // Store the layer ID for later removal
    _currentLayerId = layerId;

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
    print("removeLayer");
    if (_controller == null || _currentLayerId == null) return;

    _removeLayerAndSource(_currentLayerId!, "all-edges-source");
    _currentLayerId = null;
  }

// Updated onPressed method to use the single layer function
  void _onPressed() {
    Vertex? startVert = current.vertices[2];
    String? goalName = current.vertices[70]?.name;

    List<Edge> path = current.edges; //current.findShortestPathByName(startVert!, goalName!);
    List<Edge> edgesToDisplay = [];
    int i = 0;

    for (Edge edge in path) {
      if (edge.vertex1.floor == 3 && i < 5000) {
        i++;
        edgesToDisplay.add(edge);
      }
    }

    // Plot all collected edges as a single layer
    _plotEdgesOnSingleLayer(edgesToDisplay);
  }


}
