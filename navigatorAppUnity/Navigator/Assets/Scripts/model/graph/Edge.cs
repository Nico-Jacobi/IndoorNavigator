namespace model.graph
{

    public class Edge
    {
        public Vertex source;
        public Vertex target;
        public PathData path;

        public Edge(Vertex source, Vertex target, PathData path)
        {
            this.source = source;
            this.target = target;
            this.path = path;
            this.path.AddStartAndEndPoints(new Point(source.lat, source.lon), new Point(target.lat, target.lon));
        }
    }

}