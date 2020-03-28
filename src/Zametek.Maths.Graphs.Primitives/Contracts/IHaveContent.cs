namespace Zametek.Maths.Graphs
{
    public interface IHaveContent<out T>
    {
        T Content { get; }
    }
}
