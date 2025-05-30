namespace T3.Editor.Gui.Interaction.Timing;

internal sealed class SlidingAverage
{
    public SlidingAverage(int maxLength)
    {
        _length = maxLength;
        _queue = new Queue<double>(maxLength);
    }
        
    public  double UpdateAndCompute(double current)
    {
        _queue.Enqueue(current);
        _currentSum += current;

        var tailValue = current;
        
        if (_queue.Count > _length)
        {
            tailValue = _queue.Dequeue();
            _currentSum -= tailValue;
        }

        var delta = (current - tailValue);

        var averageStrength = 0.0;
        if (_queue.Count > 0)
        {
            averageStrength = _currentSum / _queue.Count;
        }

        return averageStrength + Math.Max(0,delta /2);
    }

    private readonly int _length;
    private readonly Queue<double> _queue;
    private double _currentSum;
}