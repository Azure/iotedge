
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IMessageConverter<T>
    {
        IMessage ToMessage(T sourceMessage);

        T FromMessage(IMessage message);
    }
}
