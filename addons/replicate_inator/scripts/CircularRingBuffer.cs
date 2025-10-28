using System;
using System.Collections;
using System.Collections.Generic;

public sealed class CircularRingBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private readonly int _mask; // power-of-two optimization
    private int _head; // next to dequeue
    private int _tail; // next to enqueue
    private int _count;

    public int Capacity { get; }
    public int Count => _count;
    public bool IsFull => _count == Capacity;
    public bool IsEmpty => _count == 0;

    public CircularRingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        // normalize capacity to next power of 2 for bit masking
        int pow2 = 1;
        while (pow2 < capacity) pow2 <<= 1;

        Capacity = pow2;
        _mask = Capacity - 1;
        _buffer = new T[Capacity];
    }

    /// <summary>
    /// Enfileira sobrescrevendo o mais antigo se estiver cheio.
    /// </summary>
    public void Enqueue(T item)
    {
        _buffer[_tail] = item;
        _tail = (_tail + 1) & _mask;

        if (IsFull)
            _head = (_head + 1) & _mask; // overwrite oldest
        else
            _count++;
    }

    /// <summary>
    /// Remove e retorna o item mais antigo.
    /// </summary>
    public T Dequeue()
    {
        if (IsEmpty) throw new InvalidOperationException("Buffer is empty.");
        var item = _buffer[_head];
        _buffer[_head] = default!;
        _head = (_head + 1) & _mask;
        _count--;
        return item;
    }

    public bool TryDequeue(out T? item)
    {
        if (IsEmpty)
        {
            item = default;
            return false;
        }

        item = _buffer[_head];
        _buffer[_head] = default!;
        _head = (_head + 1) & _mask;
        _count--;
        return true;
    }

    public T Peek()
    {
        if (IsEmpty) throw new InvalidOperationException("Buffer is empty.");
        return _buffer[_head];
    }

    /// <summary>
    /// Indexador circular (0 = mais antigo, 1 = próximo, etc.)
    /// Se ultrapassar Count, ele dá wrap automaticamente.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
            int actual = (_head + (index % Capacity + Capacity) % Capacity) & _mask;
            return _buffer[actual];
        }
        set
        {
            if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
            int actual = (_head + (index % Capacity + Capacity) % Capacity) & _mask;
            _buffer[actual] = value;
        }
    }
    
    public bool TryGetAt(int index, out T? value)
    {
        if (IsEmpty)
        {
            value = default;
            return false;
        }

        // Aplica wrap circular
        int actual = (_head + (index % Capacity + Capacity) % Capacity) & _mask;
        value = _buffer[actual];
        return true;
    }
    
    public bool TrySet(int index, T value)
    {
        if (IsEmpty)
        {
            Enqueue(value);
            return true;
        }
        
        int actual = (_head + (index % Capacity + Capacity) % Capacity) & _mask;
        _buffer[actual] = value;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = _tail = _count = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return _buffer[(_head + i) & _mask];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
