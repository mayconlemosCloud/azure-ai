using System.Collections.Concurrent;

namespace TraducaoRealtime.Utils;

/// <summary>
/// Pool thread-safe de MemoryStream para reuso e melhor performance
/// Reduz coletas de lixo (GC) e realocações de memória
/// </summary>
public class AudioPool
{
    private readonly ConcurrentBag<MemoryStream> _pool = new ConcurrentBag<MemoryStream>();
    private readonly int _poolSize;
    private const int DefaultBufferSize = 65536; // 64KB

    public AudioPool(int poolSize = 10)
    {
        _poolSize = poolSize;
    }

    /// <summary>
    /// Retorna um MemoryStream do pool ou cria um novo
    /// </summary>
    public MemoryStream Rent()
    {
        if (_pool.TryTake(out var stream))
        {
            stream.Position = 0;
            stream.SetLength(0);
            return stream;
        }

        return new MemoryStream(DefaultBufferSize);
    }

    /// <summary>
    /// Devolve um MemoryStream ao pool para reuso
    /// </summary>
    public void Return(MemoryStream stream)
    {
        if (_pool.Count < _poolSize)
        {
            stream.Position = 0;
            stream.SetLength(0);
            _pool.Add(stream);
        }
        else
        {
            stream?.Dispose();
        }
    }
}
