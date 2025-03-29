import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_inappwebview/flutter_inappwebview.dart';

class IndoorWebView extends StatefulWidget {
  const IndoorWebView({super.key});

  @override
  State<IndoorWebView> createState() => _IndoorWebViewState();
}

class _IndoorWebViewState extends State<IndoorWebView> {
  InAppWebViewController? _controller;

  int zoom = 20;
  double lat = 50.8099433;
  double lon = 8.8107979;
  double rotation = 60;
  double heightAngle = 20;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: InAppWebView(
        initialUrlRequest: URLRequest(
          url: WebUri(getUrl()),
        ),
        onWebViewCreated: (controller) {
          _controller = controller; // Speichere den Controller
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _executeJavaScript,
        child: const Icon(Icons.account_circle_rounded),
      ),
    );
  }


  String getUrl() {
    return 'https://www.informatik.uni-marburg.de/indoor/index.html?source=H04&umr_tr=03%2FA12#'
        '$zoom/$lat/$lon/$rotation/$heightAngle';
  }

  void _executeJavaScript() {
    if (_controller == null) return;

    var jsObject = createLineGeoJson(50.8099433, 8.8107979, 50.8199433, 8.8207979);

    String jsCode = '''
      setTimeout(function() {
        if (typeof my_openindoor !== 'undefined' && my_openindoor.map_) {
          my_openindoor.map_.addLayer(${json.encode(jsObject)});
        } else {
          console.error("my_openindoor is not defined.");
        }
      }, 0);
    ''';

    _controller!.evaluateJavascript(source: jsCode);
  }

  Map<String, dynamic> createLineGeoJson(double lat1, double lon1, double lat2, double lon2,
      {double height = 50.0, double width = 0.0001, String color = "#ff0000", double opacity = 0.6}) {
    return {
      "id": "custom-thick-line",
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
                "coordinates": [[
                  [lon1 - width, lat1], [lon2 - width, lat2], [lon2 + width, lat2],
                  [lon1 + width, lat1], [lon1 - width, lat1]
                ]]
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
