namespace Virbe.Core
{
    internal static class CommunicationHandlerFactory
    {
        internal static ICommunicationHandler CreateNewHandler(VirbeBeing being, IApiBeingConfig config)
        {
            if (config.SttProtocol == SttConnectionProtocol.socket_io)
            {
                return new SocketCommunicationHandler(being);
            }
            else
            {
                return new RestCommunicationHandler(being, 500);
            }
        }
    }
}