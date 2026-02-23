namespace Mostlylucid.Dagre;

public class DagreInputNode
{
    public List<DagreInputNode> Childs = new();
    public DagreInputNode Group;
    public float Height = 100;
    public List<DagreInputNode> Parents = new();
    public object Tag;
    public float Width = 300;
    public float X;
    public float Y;
}