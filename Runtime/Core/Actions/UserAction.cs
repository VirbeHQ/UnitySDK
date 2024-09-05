using System;

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