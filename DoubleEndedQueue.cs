namespace Blockgame_VulkanTests;

public class DoubleEndedQueue<T>
{
    
    private LinkedList<T> _queue = new();

    public void EnqueueLast(T value)
    {

        _queue.AddLast(value);

    }

    public void EnqueueFirst(T value)
    {

        _queue.AddFirst(value);

    }

    public bool TryDequeueFirst(out T dequeuedValue)
    {

        if (_queue.First != null)
        {
            dequeuedValue = _queue.First.Value;
            _queue.RemoveFirst();
            return true;
        }

        dequeuedValue = default(T);
        return false;

    }

    public bool TryDequeueLast(out T dequeuedValue)
    {

        if (_queue.Last != null)
        {
            
            dequeuedValue = _queue.Last.Value;
            _queue.RemoveLast();
            return true;
            
        }
        
        dequeuedValue = default(T);
        return false;

    }
    
}