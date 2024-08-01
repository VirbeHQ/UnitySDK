using System;
using Virbe.Core.Api;

namespace Virbe.Core.Actions
{
    [Serializable]
    public struct UserAction
    {
        public string text;

        public UserAction(string text)
        {
            this.text = text;
        }
    }
}