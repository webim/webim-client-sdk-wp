using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK
{
    public sealed class WMSyncBlob
    {
        public string Previous { get; set; }
        public string Current { get; set; }

        public WMSyncBlob() { }
        internal WMSyncBlob(string p, string c)
        {
            Previous = p;
            Current = c;
        }
    }

    public sealed class WMSyncChanges
    {
        public List<WMSyncBlob> SentChats { get; set; }
        public List<WMSyncBlob> SentMessages { get; set; }
        public List<string> RejectedChats { get; set; }
        public List<string> RejectedMessages { get; set; }

        public WMSyncChanges()
        {
            SentChats = new List<WMSyncBlob>();
            SentMessages = new List<WMSyncBlob>();
            RejectedChats = new List<string>();
            RejectedMessages = new List<string>();
        }

        internal WMSyncChanges(WMSyncBlob chat, WMSyncBlob message)
            : this()
        {
            if (chat != null)
            {
                SentChats.Add(chat);
            }
            if (message != null)
            {
                SentMessages.Add(message);
            }
        }

        internal void Add(WMSyncChanges changes)
        {
            if (changes.SentChats.Count > 0)
            {
                SentChats.AddRange(changes.SentChats);
            }
            if (changes.SentMessages.Count > 0)
            {
                SentMessages.AddRange(changes.SentMessages);
            }
            if (changes.RejectedChats.Count > 0)
            {
                RejectedChats.AddRange(changes.RejectedChats);
            }
            if (changes.RejectedMessages.Count > 0)
            {
                RejectedMessages.AddRange(changes.RejectedMessages);
            }
        }
    }
}
