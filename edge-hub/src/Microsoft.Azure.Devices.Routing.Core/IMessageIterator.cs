
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageIterator
    {
        Task<IEnumerable<IMessage>> GetNext(int batchSize);
    }
}
