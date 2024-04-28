namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

internal sealed class PooledBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    private ArrayPool<T>? _arrayPool; // null = disposed
    private T[]? _buffer;
    private int _bytesWritten;

    public PooledBufferWriter()
        : this(ArrayPool<T>.Shared)
    {
    }

    public PooledBufferWriter(ArrayPool<T> arrayPool)
    {
        ArgumentNullException.ThrowIfNull(arrayPool);

        _arrayPool = arrayPool;
    }

    public int Capacity => _buffer is null ? 0 : _buffer.Length;

    public int WrittenCount
    {
        get
        {
            EnsureNotDisposed();
            return _bytesWritten;
        }
    }

    public ReadOnlyMemory<T> WrittenMemory
    {
        get
        {
            EnsureNotDisposed();
            return _buffer is null ? default : _buffer.AsMemory(0, _bytesWritten);
        }
    }

    public ArraySegment<T> WrittenSegment
    {
        get
        {
            EnsureNotDisposed();

            return _buffer is null
                ? ArraySegment<T>.Empty
                : new ArraySegment<T>(_buffer, 0, _bytesWritten);
        }
    }

    public ReadOnlySpan<T> WrittenSpan => WrittenMemory.Span;

    /// <inheritdoc/>
    public void Advance(int count)
    {
        EnsureNotDisposed();

        if (_buffer is null)
        {
            throw new InvalidOperationException("No buffer was allocated for this buffer writer.");
        }

        // TODO: more checks
        _bytesWritten += count;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_arrayPool is null)
        {
            return;
        }

        var buffer = Interlocked.Exchange(ref _buffer, null);

        if (buffer is not null)
        {
            _arrayPool.Return(buffer);
        }

        _arrayPool = null;
    }

    /// <inheritdoc/>
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureNotDisposed();

        if (sizeHint is 0)
        {
            sizeHint = 1;
        }

        _buffer ??= _arrayPool.Rent(sizeHint);

        if (_buffer.Length - _bytesWritten < sizeHint)
        {
            var newBuffer = _arrayPool.Rent(sizeHint + _bytesWritten);
            _buffer.AsSpan(0, _bytesWritten).CopyTo(newBuffer);
            _arrayPool.Return(_buffer);
            _buffer = newBuffer;
        }

        return _buffer.AsMemory(_bytesWritten);
    }

    public void EnsureCapacity(int capacity)
    {
        EnsureNotDisposed();

        _buffer ??= _arrayPool.Rent(capacity);

        if (capacity > _buffer.Length)
        {
            var newBuffer = _arrayPool.Rent(capacity);
            _buffer.AsSpan(0, _bytesWritten).CopyTo(newBuffer);
            _arrayPool.Return(_buffer);
            _buffer = newBuffer;
        }
    }

    /// <inheritdoc/>
    public Span<T> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    public void Reset()
    {
        EnsureNotDisposed();

        var buffer = Interlocked.Exchange(ref _buffer, null);

        if (buffer is not null)
        {
            _arrayPool.Return(buffer);
        }

        _bytesWritten = 0;
    }

    [MemberNotNull(nameof(_arrayPool))]
    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_arrayPool is null, this);
}