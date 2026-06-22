namespace Zametek.Maths.Graphs
{
    public interface ICanBeRemoved
    {
        bool CanBeRemoved { get; }

        void SetAsReadOnly();

        void SetAsRemovable();
    }
}
