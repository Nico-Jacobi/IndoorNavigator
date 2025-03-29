import 'package:flutter/material.dart';
import 'package:navigator/view/webview.dart';
import 'model/graph.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  Graph graph = Graph();
  await graph.loadFromJson('lib/resources/graph.json');

  runApp(const MyApp());
}




class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Flutter WebView Test',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.green),
      ),
      home: const MyHomePage(title: 'WebView Test'),
    );
  }
}

class MyHomePage extends StatelessWidget {
  const MyHomePage({super.key, required this.title});
  final String title;

  @override
  Widget build(BuildContext context) {
    print('${context.hashCode}');
    print("efef");

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
      ),
      body: const IndoorWebView(), // Hier die WebView-Komponente einfügen
    );
  }
}
