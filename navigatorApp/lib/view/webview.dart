import 'dart:convert';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';
import 'package:flutter/services.dart' show rootBundle;
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
            onPressed: () => _injectJS(),
            child: const Icon(Icons.settings),
          ),
        ],
      ),
    );
  }


  String getUrl() {
    // http://localhost:3081/?source=H04#
    // https://www.informatik.uni-marburg.de/indoor/index.html?source=H04&umr_tr=03%2FA12#
    return 'http://192.168.0.46:3081/?source=H04#'
        '$zoom/'
        '$lat/'
        '$lon/'
        '$rotation/'
        '$heightAngle';
  }


  void _plotEdgesAsLines(List<Edge> edges, {String color = "#ff0000", double width = 5.0}) {
    if (_controller == null) return;

    String layerId = "all-edges-line-layer";
    String sourceId = "all-edges-line-source";

    // First, ensure we remove any existing layer and source
    _removeLayerAndSource(layerId, sourceId);

    // Create a LineString layer
    Map<String, dynamic> lineLayer = {
      "id": layerId,
      "type": "line",
      "source": sourceId,
       //"layout": {
       //  "z-order": 10000000
      //},
      "paint": {
        "line-color": color,
        "line-width": width,
        "line-opacity": 0.8,
      }
    };

    // Collect all features for the GeoJSON source
    List<Map<String, dynamic>> features = [];
    for (Edge edge in edges) {
      List<Point> pathPoints = [
        Point(edge.vertex1.lat, edge.vertex1.lon),
        ...edge.navigationPath.points,
        Point(edge.vertex2.lat, edge.vertex2.lon)
      ];

      List<List<double>> coordinates = pathPoints.map((p) => [p.lon, p.lat]).toList();

      Map<String, dynamic> feature = {
        "type": "Feature",
        "geometry": {
          "type": "LineString",
          "coordinates": coordinates
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

      // Add the layer after? indoor-anchor-fill (a layer in the website), to have the ordering correct
      // (walls and doors should be drawn afterwards so they are on top, floor first)
      
      my_openindoor.map_.addLayer(${json.encode(lineLayer)}, 'indoor-anchor-fill'); 

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
          "floor": vertex.floor,
          "rooms": vertex.rooms
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
  void _removeLayer() {
    print("removed Layer");
    if (_controller == null) return;

    _removeLayerAndSource( "all-edges-layer", "all-edges-source");
    _removeLayerAndSource( "all-vertices-layer", "all-vertices-source");
  }


  Future<void> _onPressed() async {

      // Now try getting the current level
      final currentLevel = await _controller!.evaluateJavascript(
          source: 'window.indoorControls.getLevel()'
      );
      print("Current level: $currentLevel");


      // Navigate down one level (uncomment to test)
      final downResult = await _controller!.evaluateJavascript(
          source: 'window.indoorControls.actions.down()'
      );
      print("Down action result: $downResult");

  }



  Future<void> _injectJS() async {
    final js = await rootBundle.loadString('lib/resources/webview_helper.js');
    await _controller!.evaluateJavascript(source: js);

  }


  Future<void> _onPressed2() async {
    // Load JS from file (this should be loaded and available before use)
    final js = await rootBundle.loadString('lib/resources/webview_helper.js');
    await _controller!.evaluateJavascript(source: js);  // Load the JS into the WebView

    // Ensure the page is fully loaded
    await _controller!.evaluateJavascript(source: 'window.indoorControls.init();');

    // Hide buttons from the DOM (completely)
    await _controller!.evaluateJavascript(source: 'window.indoorControls.hide();');

    // Programmatically click down button
    await _controller!.evaluateJavascript(source: 'window.indoorControls.clickDown();');

    // Get the current level
    String level = await _controller!.evaluateJavascript(source: 'window.indoorControls.getLevel();');

    // Print the level
    print('Current level: $level');

    // Click the "Up" button
    await _controller!.evaluateJavascript(source: 'window.clickIndoorUp();');

    // Optionally log or handle the result
    print(level);

    return;


    Vertex? startVert = current.vertices[113];// 113 -> Büro (03C33),255
    Vertex? goalVert = current.vertices[34];

    //List<Edge> path = current.findShortestPathByName(startVert!, goalVert.rooms[0]); //current.findShortestPathByName(startVert!, goalName!);

    List<Vertex> vertsToDisplayPath = [];
    List<Edge> edgesToDisplay = [];

    for (Edge edge in current.edges) {

        if (edge.vertex2.floor != 2){
          continue;
        }

        edgesToDisplay.add(edge);
        vertsToDisplayPath.add(edge.vertex1);
        vertsToDisplayPath.add(edge.vertex2);

    }
    //vertsToDisplayPath.add(path.last.vertex2);


    _plotVerticesOnSingleLayer(vertsToDisplayPath);
    _plotEdgesAsLines(edgesToDisplay);
  }


}
