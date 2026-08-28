using System.Threading;
using System.Threading.Tasks;

namespace Qapptia.Core.Abstractions;

public interface IShutterSoundService
{
    Task PlayAsync(CancellationToken ct = default);
}
