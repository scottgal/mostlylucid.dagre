namespace Dagre
{
    public struct DagrePoint
    {
        public float X;
        public float Y;

        public DagrePoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public DagrePoint(double x, double y)
        {
            X = (float)x;
            Y = (float)y;
        }
    }
}
