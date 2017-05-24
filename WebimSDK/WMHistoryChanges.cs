using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebimSDK
{
    public class WMHistoryChanges
    {
        public List<WMChat> NewChats { get; set; }
        public List<WMChat> ModifiedChats { get; set; }
        public List<WMMessage> Messages { get; set; }

        public WMHistoryChanges()
        {
            NewChats = new List<WMChat>();
            ModifiedChats = new List<WMChat>();
            Messages = new List<WMMessage>();
        }

        public bool HasChanges()
        {
            return NewChats.Count > 0 || ModifiedChats.Count > 0 || Messages.Count > 0;
        }
    }
}
