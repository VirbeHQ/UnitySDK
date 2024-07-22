using System;

namespace Virbe.Core.Exceptions
{
    public static class VirbeException
    {
        public class PermissionError : Exception
        {
            public PermissionError(string error) : base(error)
            {
            }
        }

        public class UnauthorizedAccessError : Exception
        {
            public UnauthorizedAccessError(string error) : base(error)
            {
            }
        }
        
        public class BadRequestError : Exception
        {
            public BadRequestError(string error) : base(error)
            {
            }
        }
        
        public class SpeechRecognitionError : Exception
        {
            public SpeechRecognitionError(string error) : base(error)
            {
            }
        }
        
        public class NetworkError : Exception
        {
            public NetworkError(string error) : base(error)
            {
            }
        }
        public class ServerError : Exception
        {
            public ServerError(string error) : base(error)
            {
            }
        }

        public class DeviceError : Exception
        {
            public DeviceError(string error) : base(error)
            {
            }
        }
    }
}